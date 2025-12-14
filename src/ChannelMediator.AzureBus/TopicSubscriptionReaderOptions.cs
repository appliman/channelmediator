namespace ChannelMediator.AzureBus;

/// <summary>
/// Configuration for a topic subscription reader.
/// </summary>
public sealed class TopicSubscriptionReaderOptions
{
    /// <summary>
    /// Gets or sets the topic name.
    /// </summary>
    public required string TopicName { get; set; }

    /// <summary>
    /// Gets or sets the subscription name.
    /// </summary>
    public required string SubscriptionName { get; set; }

    /// <summary>
    /// Gets the notification type this reader handles.
    /// </summary>
    public required Type NotificationType { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of concurrent calls to the message handler.
    /// Default is 1.
    /// </summary>
    public int MaxConcurrentCalls { get; set; } = 1;

    /// <summary>
    /// Gets or sets a value indicating whether to auto-complete messages after processing.
    /// Default is true.
    /// </summary>
    public bool AutoCompleteMessages { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum duration within which the lock will be renewed automatically.
    /// Default is 5 minutes.
    /// </summary>
    public TimeSpan MaxAutoLockRenewalDuration { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Gets or sets a value indicating whether to create the topic if it doesn't exist.
    /// Default is true.
    /// </summary>
    public bool CreateTopicIfNotExists { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to create the subscription if it doesn't exist.
    /// Default is true.
    /// </summary>
    public bool CreateSubscriptionIfNotExists { get; set; } = true;
}
