using System.Text.Json;

using RabbitMQ.Client;

namespace ChannelMediator.RabbitMQ;

/// <summary>
/// RabbitMQ implementation that publishes notifications to exchanges and enqueues requests to queues.
/// </summary>
internal sealed class RabbitMqPublisher : IRabbitMqPublisher, IAsyncDisposable
{
    private readonly IConnection _connection;
    private readonly RabbitMqEntityManager _entityManager;
    private readonly RabbitMqOptions _options;
    private readonly JsonSerializerOptions _jsonOptions;
    private IChannel? _publishChannel;
    private readonly SemaphoreSlim _channelLock = new(1, 1);
    private bool _disposed;

    public RabbitMqPublisher(
        IConnection connection,
        RabbitMqEntityManager entityManager,
        RabbitMqOptions options)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _entityManager = entityManager ?? throw new ArgumentNullException(nameof(entityManager));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };
    }

    /// <inheritdoc />
    public async Task Notify<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
        where TNotification : INotification
    {
        var exchangeName = RabbitMqNameBuilder.Build(_options.Prefix, typeof(TNotification).Name);
        await _entityManager.EnsureExchangeExistsAsync(exchangeName, cancellationToken).ConfigureAwait(false);

        var channel = await GetOrCreateChannelAsync(cancellationToken).ConfigureAwait(false);
        var body = JsonSerializer.SerializeToUtf8Bytes(notification, _jsonOptions);

        var properties = new BasicProperties
        {
            ContentType = "application/json",
            DeliveryMode = DeliveryModes.Persistent,
            Type = notification.GetType().Name,
            Headers = new Dictionary<string, object?>
            {
                ["messagetype"] = notification.GetType().AssemblyQualifiedName
            }
        };

        await channel.BasicPublishAsync(
            exchange: exchangeName,
            routingKey: string.Empty,
            mandatory: false,
            basicProperties: properties,
            body: body,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task EnqueueRequest<R>(R request, CancellationToken cancellationToken = default)
        where R : IRequest
    {
        var queueName = RabbitMqNameBuilder.Build(_options.Prefix, request.GetType().Name);
        await _entityManager.EnsureQueueExistsAsync(queueName, cancellationToken).ConfigureAwait(false);

        var channel = await GetOrCreateChannelAsync(cancellationToken).ConfigureAwait(false);
        var body = JsonSerializer.SerializeToUtf8Bytes(request, _jsonOptions);

        var properties = new BasicProperties
        {
            ContentType = "application/json",
            DeliveryMode = DeliveryModes.Persistent,
            Type = request.GetType().Name,
            Headers = new Dictionary<string, object?>
            {
                ["messagetype"] = request.GetType().AssemblyQualifiedName
            }
        };

        await channel.BasicPublishAsync(
            exchange: string.Empty,
            routingKey: queueName,
            mandatory: false,
            basicProperties: properties,
            body: body,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    private async Task<IChannel> GetOrCreateChannelAsync(CancellationToken cancellationToken)
    {
        if (_publishChannel is { IsOpen: true })
        {
            return _publishChannel;
        }

        await _channelLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_publishChannel is { IsOpen: true })
            {
                return _publishChannel;
            }

            _publishChannel = await _connection.CreateChannelAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
            return _publishChannel;
        }
        finally
        {
            _channelLock.Release();
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

        if (_publishChannel is not null)
        {
            await _publishChannel.CloseAsync().ConfigureAwait(false);
            _publishChannel.Dispose();
        }

        _channelLock.Dispose();
    }
}
