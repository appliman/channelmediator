using System.Text.Json;

using Azure.Messaging.ServiceBus;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ChannelMediator.AzureBus;

/// <summary>
/// A dedicated reader for a specific topic subscription that handles a single notification type.
/// Each instance is a singleton per topic/subscription combination.
/// </summary>
internal sealed class TopicSubscriptionReader : IAsyncDisposable
{
    private readonly ServiceBusClient _client;
    private readonly AzureServiceBusEntityManager _entityManager;
    private readonly TopicSubscriptionReaderOptions _options;
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
    public TopicSubscriptionReader(
        ServiceBusClient client,
        AzureServiceBusEntityManager entityManager,
        TopicSubscriptionReaderOptions options,
        IServiceProvider serviceProvider,
        ILogger<TopicSubscriptionReader> logger)
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
    public string TopicName => _options.TopicName;

    /// <summary>
    /// Gets the notification type this reader handles.
    /// </summary>
    public Type NotificationType => _options.MessageType;

    /// <summary>
    /// Ensures the topic and subscription exist in Azure Service Bus.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task EnsureTopicAndSubscriptionExistAsync(CancellationToken cancellationToken)
    {
        await _entityManager.EnsureTopicExistsAsync(_options.TopicName, cancellationToken).ConfigureAwait(false);
        await _entityManager.EnsureSubscriptionExistsAsync(
                _options.TopicName,
                _options.SubscriptionName,
                _options.MessageType,
                cancellationToken).ConfigureAwait(false);
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
            await EnsureTopicAndSubscriptionExistAsync(cancellationToken).ConfigureAwait(false);
            _initialized = true;
        }

        var processorOptions = new ServiceBusProcessorOptions
        {
            MaxConcurrentCalls = _options.MaxConcurrentCalls,
            AutoCompleteMessages = _options.AutoCompleteMessages,
            MaxAutoLockRenewalDuration = _options.MaxAutoLockRenewalDuration,
            ReceiveMode = ServiceBusReceiveMode.ReceiveAndDelete
        };

        _processor = _client.CreateProcessor(_options.TopicName, _options.SubscriptionName, processorOptions);
        _processor.ProcessMessageAsync += ProcessMessageAsync;
        _processor.ProcessErrorAsync += ProcessErrorAsync;

        _logger.LogInformation("Start reading messages from topic {Topic} and subscription {Subscription}.", _options.TopicName, _options.SubscriptionName);

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
        try
        {
            var messageTypeName = args.Message.ApplicationProperties.TryGetValue("messagetype", out var typeNameObj)
                ? typeNameObj as string
                : null;

            if (string.IsNullOrWhiteSpace(messageTypeName))
            {
                _logger.LogWarning("Message received without 'messagetype' property on topic {Topic}/{Subscription}. MessageId: {MessageId}", _options.TopicName, _options.SubscriptionName, args.Message.MessageId);
                return;
            }

            var messageType = Type.GetType(messageTypeName);
            if (messageType is null)
            {
                _logger.LogWarning("Unknown message type '{MessageTypeName}' received on topic {Topic}/{Subscription}. MessageId: {MessageId}", messageTypeName, _options.TopicName, _options.SubscriptionName, args.Message.MessageId);
                return;
            }

            if (!typeof(INotification).IsAssignableFrom(messageType))
            {
                _logger.LogWarning("Message type '{MessageTypeName}' does not implement INotification on topic {Topic}/{Subscription}. MessageId: {MessageId}", messageTypeName, _options.TopicName, _options.SubscriptionName, args.Message.MessageId);
                return;
            }

            var notification = JsonSerializer.Deserialize(args.Message.Body.ToArray(), messageType, _jsonOptions);
            if (notification is null)
            {
                _logger.LogWarning("Failed to deserialize notification of type {MessageType} on topic {Topic}/{Subscription}. MessageId: {MessageId}", messageType.FullName, _options.TopicName, _options.SubscriptionName, args.Message.MessageId);
                return;
            }

            await DispatchNotificationAsync(notification, args.CancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing message on topic {Topic}/{Subscription}. MessageId: {MessageId}", _options.TopicName, _options.SubscriptionName, args.Message?.MessageId);
            // swallow to keep processor running
        }
    }

    private async Task DispatchNotificationAsync(object notification, CancellationToken cancellationToken)
    {
        var mediator = _serviceProvider.GetService(typeof(IMediator)) as IMediator;
        if (mediator is null)
        {
            _logger.LogError("IMediator is not registered in the service provider while publishing notification on {Topic}/{Subscription}.", _options.TopicName, _options.SubscriptionName);
            throw new InvalidOperationException("IMediator is not registered in the service provider.");
        }

        _logger.LogTrace("Publishing notification of type {NotificationType} from topic {Topic}/{Subscription}.", notification.GetType().FullName, _options.TopicName, _options.SubscriptionName);
		await mediator.Publish(notification, cancellationToken).ConfigureAwait(false);
    }

    private Task ProcessErrorAsync(ProcessErrorEventArgs args)
    {
        _logger.LogError(args.Exception, "Service Bus error on {Topic}/{Subscription}: {Message}", _options.TopicName, _options.SubscriptionName, args.Exception.Message);
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
