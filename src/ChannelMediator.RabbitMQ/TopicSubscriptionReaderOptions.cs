namespace ChannelMediator.RabbitMQ;

/// <summary>
/// Configuration for a topic (exchange) subscription reader.
/// </summary>
public sealed class TopicSubscriptionReaderOptions
{
    internal TopicSubscriptionReaderOptions()
    {
    }

    /// <summary>
    /// Gets or sets the exchange name.
    /// </summary>
    public required string ExchangeName { get; set; }

    /// <summary>
    /// Gets or sets the subscription queue name bound to the exchange.
    /// </summary>
    public required string SubscriptionName { get; set; }

    /// <summary>
    /// Gets the notification type this reader handles.
    /// </summary>
    public required Type MessageType { get; set; }

    /// <summary>
    /// Gets or sets the prefetch count for this consumer. Default is 1.
    /// </summary>
    public ushort PrefetchCount { get; set; } = 1;
}
