using System.Collections.Concurrent;
using System.Text.Json;
using Azure.Messaging.ServiceBus;

namespace ChannelMediator.AzureBus;

/// <summary>
/// Azure Service Bus implementation of <see cref="IMessageBusPublisher"/>.
/// </summary>
internal sealed class AzureServiceBusPublisher : IAzurePublisher, IAsyncDisposable
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
        await _entityManager.EnsureTopicExistsAsync(queueOrTopicName, cancellationToken).ConfigureAwait(false);

        var sender = GetOrCreateSender(queueOrTopicName);
        var message = CreateMessage(notification);

        await sender.SendMessageAsync(message, cancellationToken);
    }

    public async Task PublishAsync(string topicName, object message, CancellationToken cancellationToken = default)
    {
        await _entityManager.EnsureTopicExistsAsync(topicName, cancellationToken).ConfigureAwait(false);

        var sender = GetOrCreateSender(topicName);
        var busmessage = CreateServiceBusMessage(message);

        await sender.SendMessageAsync(busmessage, cancellationToken);
    }

    public async Task EnqueueRequest(object request, CancellationToken cancellationToken = default)
    {
        var queueOrTopicName = $"channel-mediator-{request.GetType().Name}".ToLower();
        if (!string.IsNullOrWhiteSpace(_options.Prefix))
        {
            queueOrTopicName = $"{_options.Prefix}-{queueOrTopicName}".ToLower();
        }
        await _entityManager.EnsureQueueExistsAsync(queueOrTopicName, cancellationToken).ConfigureAwait(false);
        var sender = GetOrCreateSender(queueOrTopicName);
        var message = CreateServiceBusMessage(request);
        await sender.SendMessageAsync(message, cancellationToken);
    }

    public async Task Enqueue(string queueName, object message, CancellationToken cancellationToken = default)
    {
        await _entityManager.EnsureQueueExistsAsync(queueName, cancellationToken).ConfigureAwait(false);
        var sender = GetOrCreateSender(queueName);
        var busmessage = CreateServiceBusMessage(message);
        await sender.SendMessageAsync(busmessage, cancellationToken);
    }

    private ServiceBusSender GetOrCreateSender(string queueOrTopicName)
    {
        return _senders.GetOrAdd(queueOrTopicName, name => _client.CreateSender(name));
    }

    private ServiceBusMessage CreateMessage<TNotification>(TNotification notification)
        where TNotification : INotification
    {
        return CreateServiceBusMessage(notification);
    }

    private ServiceBusMessage CreateServiceBusMessage(object message)
    {
        var body = JsonSerializer.SerializeToUtf8Bytes(message, _jsonOptions);
        
        var busmessage = new ServiceBusMessage(body)
        {
            ContentType = "application/json",
            Subject = message.GetType().Name,
            ApplicationProperties =
            {
                ["messagetype"] = message.GetType().AssemblyQualifiedName
            }
        };

        return busmessage;
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
