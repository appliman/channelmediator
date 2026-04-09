using System.Text;
using System.Text.Json;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace ChannelMediator.RabbitMQ;

/// <summary>
/// A dedicated reader for a specific queue that handles a single request type.
/// </summary>
internal sealed class QueueReader : IAsyncDisposable
{
    private readonly IConnection _connection;
    private readonly RabbitMqEntityManager _entityManager;
    private readonly QueueReaderOptions _options;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger _logger;
    private readonly JsonSerializerOptions _jsonOptions;
    private IChannel? _channel;
    private bool _disposed;

    public QueueReader(
        IConnection connection,
        RabbitMqEntityManager entityManager,
        QueueReaderOptions options,
        IServiceProvider serviceProvider,
        ILogger logger)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
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
    /// Gets the queue name this reader is listening to.
    /// </summary>
    public string QueueName => _options.QueueName;

    /// <summary>
    /// Gets the request type this reader handles.
    /// </summary>
    public Type RequestType => _options.RequestType;

    /// <summary>
    /// Ensures the queue exists in RabbitMQ.
    /// </summary>
    public async Task EnsureQueueExistAsync(CancellationToken cancellationToken)
    {
        await _entityManager.EnsureQueueExistsAsync(_options.QueueName, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Starts listening for messages.
    /// </summary>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        await EnsureQueueExistAsync(cancellationToken).ConfigureAwait(false);

        _channel = await _connection.CreateChannelAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        await _channel.BasicQosAsync(prefetchSize: 0, prefetchCount: _options.PrefetchCount, global: false, cancellationToken: cancellationToken).ConfigureAwait(false);

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.ReceivedAsync += ProcessMessageAsync;

        _logger.LogInformation("Starting queue reader for {QueueName} handling messages of type {MessageType}.",
            _options.QueueName, _options.RequestType.FullName);

        await _channel.BasicConsumeAsync(
            queue: _options.QueueName,
            autoAck: true,
            consumer: consumer,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Stops listening for messages.
    /// </summary>
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_channel is { IsOpen: true })
        {
            await _channel.CloseAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task ProcessMessageAsync(object sender, BasicDeliverEventArgs args)
    {
        try
        {
            string? messageTypeName = null;
            if (args.BasicProperties.Headers is not null &&
                args.BasicProperties.Headers.TryGetValue("messagetype", out var typeNameObj))
            {
                messageTypeName = typeNameObj is byte[] bytes
                    ? Encoding.UTF8.GetString(bytes)
                    : typeNameObj as string;
            }

            if (string.IsNullOrWhiteSpace(messageTypeName))
            {
                _logger.LogWarning("Message received without 'messagetype' header on queue {QueueName}. DeliveryTag: {DeliveryTag}",
                    _options.QueueName, args.DeliveryTag);
                return;
            }

            var messageType = Type.GetType(messageTypeName);
            if (messageType is null)
            {
                _logger.LogWarning("Unknown message type '{MessageTypeName}' received on queue {QueueName}. DeliveryTag: {DeliveryTag}",
                    messageTypeName, _options.QueueName, args.DeliveryTag);
                return;
            }

            if (!typeof(IRequest).IsAssignableFrom(messageType)
                && !messageType.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IRequest<>)))
            {
                await ProcessRequestWrapperMessageAsync(args).ConfigureAwait(false);
                return;
            }

            var request = JsonSerializer.Deserialize(args.Body.Span, messageType, _jsonOptions);
            if (request is null)
            {
                _logger.LogWarning("Failed to deserialize message of type {MessageType} on queue {QueueName}. DeliveryTag: {DeliveryTag}",
                    messageType.FullName, _options.QueueName, args.DeliveryTag);
                return;
            }

            var mediator = _serviceProvider.GetService(typeof(IMediator)) as IMediator;
            if (mediator is null)
            {
                _logger.LogError("IMediator is not registered in the service provider while processing message on queue {QueueName}.",
                    _options.QueueName);
                throw new InvalidOperationException("IMediator is not registered in the service provider.");
            }

            _logger.LogTrace("Processing message of type {MessageType} on queue {QueueName}. DeliveryTag: {DeliveryTag}",
                messageType.FullName, _options.QueueName, args.DeliveryTag);

            if (request is IRequest nonGenericRequest)
            {
                await mediator.Send(nonGenericRequest).ConfigureAwait(false);
            }
			else if (request.GetType().GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IRequest<>)))
			{
                dynamic genericRequest = request;
				await mediator.Send(genericRequest).ConfigureAwait(false);
			}
            else
            {
                _logger.LogWarning("Received message of type {MessageType} does not implement IRequest on queue {QueueName}. DeliveryTag: {DeliveryTag}",
                    messageType.FullName, _options.QueueName, args.DeliveryTag);
			}
		}
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing message on queue {QueueName}. DeliveryTag: {DeliveryTag}",
                _options.QueueName, args.DeliveryTag);
        }
    }

    private async Task ProcessRequestWrapperMessageAsync(BasicDeliverEventArgs args)
    {
        if (_options.Handler is null)
        {
            _logger.LogWarning("No handler configured for queue {QueueName}. DeliveryTag: {DeliveryTag}",
                _options.QueueName, args.DeliveryTag);
            return;
        }

        var request = JsonSerializer.Deserialize(args.Body.Span, _options.RequestType, _jsonOptions);
        if (request is null)
        {
            _logger.LogWarning("Failed to deserialize wrapper message of type {RequestType} on queue {QueueName}. DeliveryTag: {DeliveryTag}",
                _options.RequestType.FullName, _options.QueueName, args.DeliveryTag);
            return;
        }

        var mediator = _serviceProvider.GetRequiredService<IMediator>();

        try
        {
            var handlerType = _options.Handler.GetType();
            var invokeMethod = handlerType.GetMethod("Invoke");
            if (invokeMethod is not null)
            {
                var result = invokeMethod.Invoke(_options.Handler, [mediator, request]);
                if (result is Task task)
                {
                    await task.ConfigureAwait(false);
                }
            }
            else
            {
                _logger.LogError("Handler for queue {QueueName} does not expose an Invoke method. DeliveryTag: {DeliveryTag}",
                    _options.QueueName, args.DeliveryTag);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error invoking handler for message on queue {QueueName}. DeliveryTag: {DeliveryTag}",
                _options.QueueName, args.DeliveryTag);
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (_channel is not null)
        {
            if (_channel.IsOpen)
            {
                await _channel.CloseAsync().ConfigureAwait(false);
            }

            _channel.Dispose();
        }
    }
}
