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
        await _entityManager.EnsureQueueExistsAsync(_options.QueueName, cancellationToken).ConfigureAwait(false);
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
            await EnsureQueueExistAsync(cancellationToken).ConfigureAwait(false);
            _initialized = true;
        }

        var processorOptions = new ServiceBusProcessorOptions
        {
            MaxConcurrentCalls = _options.MaxConcurrentCalls,
            AutoCompleteMessages = _options.AutoCompleteMessages,
            MaxAutoLockRenewalDuration = _options.MaxAutoLockRenewalDuration,
            ReceiveMode = ServiceBusReceiveMode.ReceiveAndDelete
        };

        _processor = _client.CreateProcessor(_options.QueueName, processorOptions);
        _processor.ProcessMessageAsync += ProcessMessageAsync;
        _processor.ProcessErrorAsync += ProcessErrorAsync;

        await _processor.StartProcessingAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Stops listening for messages.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_processor is not null)
        {
            await _processor.StopProcessingAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task ProcessMessageAsync(ProcessMessageEventArgs args)
    {
        var requestTypeName = args.Message.ApplicationProperties.TryGetValue("RequestType", out var typeNameObj)
            ? typeNameObj as string
            : null;

        if (!string.IsNullOrWhiteSpace(requestTypeName))
        {
            await ProcessRequestMessageAsync(requestTypeName, args);
        }

        await ProcessRequestWrapperMessageAsync(args.Message);
    }

    private async Task ProcessRequestMessageAsync(string requestTypeName, ProcessMessageEventArgs args)
    { 
        var requestType = Type.GetType(requestTypeName);
        if (requestType is null)
        {
            // Log warning: Unknown notification type
            return;
        }

        // Verify this message is for our expected notification type
        if (!_options.RequestType.IsAssignableFrom(requestType))
        {
            // Log warning: Unexpected notification type for this reader
            return;
        }

        var request = JsonSerializer.Deserialize(args.Message.Body.ToArray(), requestType, _jsonOptions);
        if (request is null)
        {
            // Log warning: Failed to deserialize notification
            return;
        }

        var mediator = _serviceProvider.GetService(typeof(IMediator)) as IMediator;
        if (mediator is null)
        {
            throw new InvalidOperationException("IMediator is not registered in the service provider.");
        }

        await mediator.Send(request).ConfigureAwait(false);
    }

    private async Task ProcessRequestWrapperMessageAsync(ServiceBusReceivedMessage message)
    {
        if (_options.Handler is null)
        {
            return;
        }

        var request = JsonSerializer.Deserialize(message.Body.ToArray(), _options.RequestType, _jsonOptions);
        if (request is null)
        {
            // Log warning: Failed to deserialize notification
            return;
        }

        var mediator = _serviceProvider.GetRequiredService<IMediator>();

        // Invoke the handler using reflection to call the generic method
        var handlerType = _options.Handler.GetType();
        var invokeMethod = handlerType.GetMethod("Invoke");
        if (invokeMethod is not null)
        {
            var task = invokeMethod.Invoke(_options.Handler, [mediator, request]) as Task;
            if (task is not null)
            {
                await task.ConfigureAwait(false);
            }
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
            await _processor.DisposeAsync().ConfigureAwait(false);
        }
    }
}
