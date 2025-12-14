using System.Text.Json;
using Azure.Messaging.ServiceBus;

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
        IServiceProvider serviceProvider)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _entityManager = entityManager ?? throw new ArgumentNullException(nameof(entityManager));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
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
    public Type NotificationType => _options.NotificationType;

    /// <summary>
    /// Ensures the topic and subscription exist in Azure Service Bus.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task EnsureTopicAndSubscriptionExistAsync(CancellationToken cancellationToken)
    {
        if (_options.CreateTopicIfNotExists)
        {
            await _entityManager.EnsureTopicExistsAsync(_options.TopicName, cancellationToken).ConfigureAwait(false);
        }

        if (_options.CreateSubscriptionIfNotExists)
        {
            await _entityManager.EnsureSubscriptionExistsAsync(
                _options.TopicName,
                _options.SubscriptionName,
                _options.NotificationType,
                cancellationToken).ConfigureAwait(false);
        }
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
        var notificationTypeName = args.Message.ApplicationProperties.TryGetValue("NotificationType", out var typeNameObj)
            ? typeNameObj as string
            : null;

        if (string.IsNullOrWhiteSpace(notificationTypeName))
        {
            // Log warning: Missing notification type, skip message
            return;
        }

        var notificationType = Type.GetType(notificationTypeName);
        if (notificationType is null)
        {
            // Log warning: Unknown notification type
            return;
        }

        // Verify this message is for our expected notification type
        if (!_options.NotificationType.IsAssignableFrom(notificationType))
        {
            // Log warning: Unexpected notification type for this reader
            return;
        }

        var notification = JsonSerializer.Deserialize(args.Message.Body.ToArray(), notificationType, _jsonOptions);
        if (notification is null)
        {
            // Log warning: Failed to deserialize notification
            return;
        }

        await DispatchNotificationAsync(notification, notificationType, args.CancellationToken).ConfigureAwait(false);
    }

    private async Task DispatchNotificationAsync(object notification, Type notificationType, CancellationToken cancellationToken)
    {
        var mediator = _serviceProvider.GetService(typeof(IMediator)) as IMediator;
        if (mediator is null)
        {
            throw new InvalidOperationException("IMediator is not registered in the service provider.");
        }

        await mediator.Publish(notification, cancellationToken).ConfigureAwait(false);
    }

    private Task ProcessErrorAsync(ProcessErrorEventArgs args)
    {
        Console.Error.WriteLine($"Service Bus error on {_options.TopicName}/{_options.SubscriptionName}: {args.Exception.Message}");
        Console.Error.WriteLine($"Error source: {args.ErrorSource}");
        Console.Error.WriteLine($"Entity path: {args.EntityPath}");

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
