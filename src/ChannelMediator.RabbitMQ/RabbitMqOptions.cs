using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ChannelMediator.RabbitMQ;

/// <summary>
/// Configuration options for RabbitMQ integration.
/// </summary>
public sealed class RabbitMqOptions
{
    internal RabbitMqOptions()
    {
    }

    /// <summary>
    /// Gets or sets the collection of service descriptors for dependency injection.
    /// </summary>
    public IServiceCollection Services { get; set; } = default!;

    /// <summary>
    /// Gets or sets the strategy used to publish notifications.
    /// </summary>
    public NotificationPublishStrategy Strategy { get; set; } = NotificationPublishStrategy.Sequential;

    /// <summary>
    /// Gets or sets the RabbitMQ host name.
    /// </summary>
    public string HostName { get; set; } = "localhost";

    /// <summary>
    /// Gets or sets the RabbitMQ port. Default is 5672.
    /// </summary>
    public int Port { get; set; } = 5672;

    /// <summary>
    /// Gets or sets the RabbitMQ user name.
    /// </summary>
    public string UserName { get; set; } = "guest";

    /// <summary>
    /// Gets or sets the RabbitMQ password.
    /// </summary>
    public string Password { get; set; } = "guest";

    /// <summary>
    /// Gets or sets the RabbitMQ virtual host. Default is "/".
    /// </summary>
    public string VirtualHost { get; set; } = "/";

    /// <summary>
    /// Gets or sets the prefix for exchange and queue names.
    /// </summary>
    public string Prefix { get; set; } = null!;

    /// <summary>
    /// Gets or sets the name of the subscriber used for topic subscriptions.
    /// </summary>
    public string TopicSubscriberName { get; set; } = Environment.MachineName.ToLower();

    /// <summary>
    /// Gets or sets the maximum number of prefetched messages per consumer. Default is 1.
    /// </summary>
    public ushort PrefetchCount { get; set; } = 1;

    /// <summary>
    /// Gets or sets the processing mode. Default is Live.
    /// </summary>
    public RabbitMqMode ProcessMode { get; set; } = RabbitMqMode.Live;

    internal bool SubscribeToAllTopics { get; set; } = true;

    /// <summary>
    /// Subscribes to all exchanges matching the configured prefix for notification delivery.
    /// </summary>
    public void AddAllRabbitMqTopicNotification()
    {
        if (string.IsNullOrWhiteSpace(TopicSubscriberName))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(Prefix))
        {
            return;
        }

        SubscribeToAllTopics = true;
        Services.TryAddEnumerable(
            ServiceDescriptor.Singleton<Microsoft.Extensions.Hosting.IHostedService, TopicSubscriptionReadersHostedService>());
    }

    /// <summary>
    /// Adds an exchange subscription reader for a specific notification type.
    /// If the exchange or queue doesn't exist, they will be created automatically.
    /// </summary>
    /// <typeparam name="TNotification">The notification type this reader handles.</typeparam>
    /// <param name="subscriptionName">The subscription queue name to read from.</param>
    /// <param name="configure">Action to configure reader options.</param>
    public void AddRabbitMqTopicNotificationReader<TNotification>(
        string subscriptionName,
        Action<TopicSubscriptionReaderOptions>? configure = null)
        where TNotification : INotification
    {
        ArgumentNullException.ThrowIfNull(subscriptionName);

        var exchangeName = typeof(TNotification).Name;
        exchangeName = RabbitMqNameBuilder.Build(Prefix, exchangeName);

        var readerOptions = new TopicSubscriptionReaderOptions
        {
            ExchangeName = exchangeName,
            SubscriptionName = subscriptionName.ToLower(),
            MessageType = typeof(TNotification)
        };

        configure?.Invoke(readerOptions);

        TopicSubscriptionReaderRegistry.Register(readerOptions);

        Services.TryAddEnumerable(
            ServiceDescriptor.Singleton<Microsoft.Extensions.Hosting.IHostedService, TopicSubscriptionReadersHostedService>());
    }

    /// <summary>
    /// Adds a queue reader for a specific request type.
    /// If the queue doesn't exist, it will be created automatically.
    /// </summary>
    /// <typeparam name="TRequest">The request type this reader handles.</typeparam>
    /// <param name="queueName">Optional queue name override.</param>
    /// <param name="configure">Action to configure reader options.</param>
    public void AddRabbitMqQueueRequestReader<TRequest>(
        string? queueName = null,
        Action<QueueReaderOptions>? configure = null)
        where TRequest : IRequest
    {
        queueName ??= typeof(TRequest).Name;
        queueName = RabbitMqNameBuilder.Build(Prefix, queueName);

        var readerOptions = new QueueReaderOptions
        {
            QueueName = queueName,
            RequestType = typeof(TRequest)
        };

        configure?.Invoke(readerOptions);

        QueueReaderRegistry.Register(readerOptions);

        Services.TryAddEnumerable(
            ServiceDescriptor.Singleton<Microsoft.Extensions.Hosting.IHostedService, QueueReadersHostedService>());
    }
}
