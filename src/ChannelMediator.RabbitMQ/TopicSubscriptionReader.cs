using System.Text;
using System.Text.Json;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace ChannelMediator.RabbitMQ;

/// <summary>
/// A dedicated reader for a specific exchange subscription that handles a single notification type.
/// </summary>
internal sealed class TopicSubscriptionReader : IAsyncDisposable
{
    private readonly IConnection _connection;
    private readonly RabbitMqEntityManager _entityManager;
    private readonly TopicSubscriptionReaderOptions _options;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger _logger;
    private readonly JsonSerializerOptions _jsonOptions;
    private IChannel? _channel;
    private bool _disposed;

    public TopicSubscriptionReader(
        IConnection connection,
        RabbitMqEntityManager entityManager,
        TopicSubscriptionReaderOptions options,
        IServiceProvider serviceProvider,
        ILogger<TopicSubscriptionReader> logger)
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
    /// Gets the exchange name this reader is listening to.
    /// </summary>
    public string ExchangeName => _options.ExchangeName;

    /// <summary>
    /// Gets the notification type this reader handles.
    /// </summary>
    public Type NotificationType => _options.MessageType;

    /// <summary>
    /// Ensures the exchange, queue, and binding exist in RabbitMQ.
    /// </summary>
    public async Task EnsureExchangeAndQueueExistAsync(CancellationToken cancellationToken)
    {
        await _entityManager.EnsureExchangeExistsAsync(_options.ExchangeName, cancellationToken).ConfigureAwait(false);

        var queueName = $"{_options.ExchangeName}.{_options.SubscriptionName}";
        await _entityManager.EnsureQueueExistsAsync(queueName, cancellationToken).ConfigureAwait(false);
        await _entityManager.EnsureBindingExistsAsync(_options.ExchangeName, queueName, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Starts listening for messages.
    /// </summary>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        await EnsureExchangeAndQueueExistAsync(cancellationToken).ConfigureAwait(false);

        _channel = await _connection.CreateChannelAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        await _channel.BasicQosAsync(prefetchSize: 0, prefetchCount: _options.PrefetchCount, global: false, cancellationToken: cancellationToken).ConfigureAwait(false);

        var queueName = $"{_options.ExchangeName}.{_options.SubscriptionName}";

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.ReceivedAsync += ProcessMessageAsync;

        _logger.LogInformation("Start reading messages from exchange {Exchange} with subscription queue {Queue}.", _options.ExchangeName, queueName);

        await _channel.BasicConsumeAsync(
            queue: queueName,
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
                _logger.LogWarning("Message received without 'messagetype' header on exchange {Exchange}/{Subscription}. DeliveryTag: {DeliveryTag}",
                    _options.ExchangeName, _options.SubscriptionName, args.DeliveryTag);
                return;
            }

            var messageType = Type.GetType(messageTypeName);
            if (messageType is null)
            {
                _logger.LogWarning("Unknown message type '{MessageTypeName}' received on exchange {Exchange}/{Subscription}. DeliveryTag: {DeliveryTag}",
                    messageTypeName, _options.ExchangeName, _options.SubscriptionName, args.DeliveryTag);
                return;
            }

            if (!typeof(INotification).IsAssignableFrom(messageType))
            {
                _logger.LogWarning("Message type '{MessageTypeName}' does not implement INotification on exchange {Exchange}/{Subscription}. DeliveryTag: {DeliveryTag}",
                    messageTypeName, _options.ExchangeName, _options.SubscriptionName, args.DeliveryTag);
                return;
            }

            var notification = JsonSerializer.Deserialize(args.Body.Span, messageType, _jsonOptions);
            if (notification is null)
            {
                _logger.LogWarning("Failed to deserialize notification of type {MessageType} on exchange {Exchange}/{Subscription}. DeliveryTag: {DeliveryTag}",
                    messageType.FullName, _options.ExchangeName, _options.SubscriptionName, args.DeliveryTag);
                return;
            }

            await DispatchNotificationAsync(notification, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing message on exchange {Exchange}/{Subscription}. DeliveryTag: {DeliveryTag}",
                _options.ExchangeName, _options.SubscriptionName, args.DeliveryTag);
        }
    }

    private async Task DispatchNotificationAsync(object notification, CancellationToken cancellationToken)
    {
        var mediator = _serviceProvider.GetService(typeof(IMediator)) as IMediator;
        if (mediator is null)
        {
            _logger.LogError("IMediator is not registered in the service provider while publishing notification on {Exchange}/{Subscription}.",
                _options.ExchangeName, _options.SubscriptionName);
            throw new InvalidOperationException("IMediator is not registered in the service provider.");
        }

        _logger.LogTrace("Publishing notification of type {NotificationType} from exchange {Exchange}/{Subscription}.",
            notification.GetType().FullName, _options.ExchangeName, _options.SubscriptionName);

        await mediator.Publish((INotification)notification, cancellationToken).ConfigureAwait(false);
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
