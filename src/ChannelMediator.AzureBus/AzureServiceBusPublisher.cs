using System.Collections.Concurrent;
using System.Text.Json;
using Azure.Messaging.ServiceBus;

namespace ChannelMediator.AzureBus;

/// <summary>
/// Azure Service Bus implementation of <see cref="IMessageBusPublisher"/>.
/// </summary>
internal sealed class AzureServiceBusPublisher : IGlobalPublisher, IAsyncDisposable
{
    private readonly ServiceBusClient _client;
    private readonly AzureServiceBusEntityManager _entityManager;
    private readonly AzureServiceBusOptions _options;
    private readonly ConcurrentDictionary<string, ServiceBusSender> _senders = new();
    private readonly JsonSerializerOptions _jsonOptions;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="AzureServiceBusPublisher"/> class.
    /// </summary>
    /// <param name="client">The Service Bus client.</param>
    /// <param name="entityManager">The entity manager for creating topics.</param>
    /// <param name="options">The configuration options.</param>
    public AzureServiceBusPublisher(
        ServiceBusClient client, 
        AzureServiceBusEntityManager entityManager, 
        AzureServiceBusOptions options)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _entityManager = entityManager ?? throw new ArgumentNullException(nameof(entityManager));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };
    }

    /// <inheritdoc />
    public async Task PublishAsync<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
        where TNotification : INotification
    {
        var queueOrTopicName = $"channel-mediator-{typeof(TNotification).Name}".ToLower();
        if (!string.IsNullOrWhiteSpace(_options.Prefix))
        {
            queueOrTopicName = $"{_options.Prefix}-{queueOrTopicName}".ToLower();
        }
        if (_options.CreateTopicIfNotExists)
        {
            await _entityManager.EnsureTopicExistsAsync(queueOrTopicName, cancellationToken).ConfigureAwait(false);
        }

        var sender = GetOrCreateSender(queueOrTopicName);
        var message = CreateMessage(notification);

        await sender.SendMessageAsync(message, cancellationToken);
    }

    private ServiceBusSender GetOrCreateSender(string queueOrTopicName)
    {
        return _senders.GetOrAdd(queueOrTopicName, name => _client.CreateSender(name));
    }

    private ServiceBusMessage CreateMessage<TNotification>(TNotification notification)
        where TNotification : INotification
    {
        var notificationType = notification!.GetType();
        return CreateServiceBusMessage(notification, notificationType);
    }

    private ServiceBusMessage CreateServiceBusMessage(object notification, Type notificationType)
    {
        var body = JsonSerializer.SerializeToUtf8Bytes(notification, notificationType, _jsonOptions);
        
        var message = new ServiceBusMessage(body)
        {
            ContentType = "application/json",
            Subject = notificationType.Name,
            ApplicationProperties =
            {
                ["NotificationType"] = notificationType.AssemblyQualifiedName
            }
        };

        return message;
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        var disposeTasks = _senders.Values.Select(sender => sender.DisposeAsync().AsTask());
        await Task.WhenAll(disposeTasks).ConfigureAwait(false);
        _senders.Clear();
    }
}
