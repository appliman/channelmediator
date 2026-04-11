using System.Text.Json;

using Azure.Messaging.ServiceBus;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ChannelMediator.AzureBus;

/// <summary>
/// A dedicated reader for a specific topic subscription that handles a single notification type.
/// Each instance is a singleton per topic/subscription combination.
/// </summary>
internal sealed class QueueReader : IAsyncDisposable
{
	private readonly ServiceBusClient _client;
	private readonly AzureServiceBusEntityManager _entityManager;
	private readonly QueueReaderOptions _options;
	private readonly IServiceProvider _serviceProvider;
	private readonly ILogger _logger;
	private readonly JsonSerializerOptions _jsonOptions;
	private ServiceBusProcessor? _processor;
	private bool _disposed;
	private bool _initialized;

	/// <summary>
	/// Initializes a new instance of the <see cref="TopicSubscriptionReader"/> class.
	/// </summary>
	/// <param name="client">The Service Bus client.</param>
	/// <param name="entityManager">The entity manager for creating topics/subscriptions.</param>
	/// <param name="options">The reader options.</param>
	/// <param name="serviceProvider">The service provider for resolving handlers.</param>
	/// <param name="logger">The logger used to record queue reader activity.</param>
	public QueueReader(
		ServiceBusClient client,
		AzureServiceBusEntityManager entityManager,
		QueueReaderOptions options,
		IServiceProvider serviceProvider,
		ILogger logger)
	{
		_client = client ?? throw new ArgumentNullException(nameof(client));
		_entityManager = entityManager ?? throw new ArgumentNullException(nameof(entityManager));
		_options = options ?? throw new ArgumentNullException(nameof(options));
		_serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		_jsonOptions = new JsonSerializerOptions
		{
			PropertyNamingPolicy = JsonNamingPolicy.CamelCase
		};
	}

	/// <summary>
	/// Gets the topic name this reader is listening to.
	/// </summary>
	public string QueueName => _options.QueueName;

	/// <summary>
	/// Gets the notification type this reader handles.
	/// </summary>
	public Type RequestType => _options.RequestType;

	/// <summary>
	/// Ensures the topic and subscription exist in Azure Service Bus.
	/// </summary>
	/// <param name="cancellationToken">Cancellation token.</param>
	public async Task EnsureQueueExistAsync(CancellationToken cancellationToken)
	{
		await _entityManager.EnsureQueueExistsAsync(_options.QueueName, cancellationToken);
	}

	/// <summary>
	/// Starts listening for messages.
	/// </summary>
	/// <param name="cancellationToken">Cancellation token.</param>
	public async Task StartAsync(CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);

		if (!_initialized)
		{
			await EnsureQueueExistAsync(cancellationToken);
			_initialized = true;
		}

		var processorOptions = new ServiceBusProcessorOptions
		{
			MaxConcurrentCalls = _options.MaxConcurrentCalls,
			AutoCompleteMessages = _options.AutoCompleteMessages,
			MaxAutoLockRenewalDuration = _options.MaxAutoLockRenewalDuration,
			PrefetchCount = _options.PrefetchCount,
			ReceiveMode = ServiceBusReceiveMode.ReceiveAndDelete
		};

		_processor = _client.CreateProcessor(_options.QueueName, processorOptions);
		_processor.ProcessMessageAsync += ProcessMessageAsync;
		_processor.ProcessErrorAsync += ProcessErrorAsync;

		_logger.LogInformation("Starting queue reader for {QueueName} handling messages of type {MessageType}.", _options.QueueName, _options.RequestType.FullName);

		await _processor.StartProcessingAsync(cancellationToken);
	}

	/// <summary>
	/// Stops listening for messages.
	/// </summary>
	/// <param name="cancellationToken">Cancellation token.</param>
	public async Task StopAsync(CancellationToken cancellationToken)
	{
		if (_processor is not null)
		{
			await _processor.StopProcessingAsync(cancellationToken);
		}
	}

	private async Task ProcessMessageAsync(ProcessMessageEventArgs args)
	{
		try
		{
			var messageTypeName = args.Message.ApplicationProperties.TryGetValue("messagetype", out var typeNameObj)
				? typeNameObj as string
				: null;

			if (string.IsNullOrWhiteSpace(messageTypeName))
			{
				_logger.LogWarning("Message received without 'messagetype' property on queue {QueueName}. MessageId: {MessageId}", _options.QueueName, args.Message.MessageId);
				return;
			}

			var messageType = Type.GetType(messageTypeName);
			if (messageType is null)
			{
				_logger.LogWarning("Unknown message type '{MessageTypeName}' received on queue {QueueName}. MessageId: {MessageId}", messageTypeName, _options.QueueName, args.Message.MessageId);
				return;
			}

			// Verify if this message implements IRequest or IRequest<TResponse>
			var implementsRequest = typeof(IRequest).IsAssignableFrom(messageType)
				|| messageType.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IRequest<>));

			if (!implementsRequest)
			{
				await ProcessRequestWrapperMessageAsync(args.Message);
				return;
			}

			var request = JsonSerializer.Deserialize(args.Message.Body.ToMemory().Span, messageType, _jsonOptions);
			if (request is null)
			{
				_logger.LogWarning("Failed to deserialize message of type {MessageType} on queue {QueueName}. MessageId: {MessageId}", messageType.FullName, _options.QueueName, args.Message.MessageId);
				return;
			}

			var mediator = _serviceProvider.GetRequiredService<IMediator>();

			_logger.LogTrace("Processing message {MessageId} of type {MessageType} on queue {QueueName}.", args.Message.MessageId, messageType.FullName, _options.QueueName);
			if (request is IRequest simpleRequest)
			{
				await mediator.Send(simpleRequest);
			}
			else if (request.GetType().GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IRequest<>)))
			{
				dynamic genericRequest = request;
				await mediator.Send(genericRequest);
			}
			else
			{
				_logger.LogWarning("Deserialized message does not implement IRequest or IRequest<TResponse> as expected for message {MessageId} on queue {QueueName}.", args.Message.MessageId, _options.QueueName);
			}
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error processing message on queue {QueueName}. MessageId: {MessageId}", _options.QueueName, args.Message?.MessageId);
		}
	}

	private async Task ProcessRequestWrapperMessageAsync(ServiceBusReceivedMessage message)
	{
		if (_options.Handler is null)
		{
			_logger.LogWarning("No handler configured for queue {QueueName}. MessageId: {MessageId}", _options.QueueName, message.MessageId);
			return;
		}

		var request = JsonSerializer.Deserialize(message.Body.ToMemory().Span, _options.RequestType, _jsonOptions);
		if (request is null)
		{
			_logger.LogWarning("Failed to deserialize wrapper message of type {RequestType} on queue {QueueName}. MessageId: {MessageId}", _options.RequestType.FullName, _options.QueueName, message.MessageId);
			return;
		}

		var mediator = _serviceProvider.GetRequiredService<IMediator>();

		// Invoke the handler using reflection to call the delegate
		try
		{
			var handlerType = _options.Handler.GetType();
			var invokeMethod = handlerType.GetMethod("Invoke");
			if (invokeMethod is not null)
			{
				var result = invokeMethod.Invoke(_options.Handler, new object[] { mediator, request });
				if (result is Task task)
				{
					await task;
				}
			}
			else
			{
				_logger.LogError("Handler for queue {QueueName} does not expose an Invoke method. MessageId: {MessageId}", _options.QueueName, message.MessageId);
			}
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error invoking handler for message {MessageId} on queue {QueueName}", message.MessageId, _options.QueueName);
		}
	}

	private Task ProcessErrorAsync(ProcessErrorEventArgs args)
	{
		_logger.LogError("Service Bus error on {QueueName}: {Message}", _options.QueueName, args.Exception.Message);
		_logger.LogError("Error source: {ErrorSource}", args.ErrorSource);
		_logger.LogError("Entity path: {EntityPath}", args.EntityPath);

		return Task.CompletedTask;
	}

	/// <inheritdoc />
	public async ValueTask DisposeAsync()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;

		if (_processor is not null)
		{
			_processor.ProcessMessageAsync -= ProcessMessageAsync;
			_processor.ProcessErrorAsync -= ProcessErrorAsync;
			await _processor.DisposeAsync();
		}
	}
}
