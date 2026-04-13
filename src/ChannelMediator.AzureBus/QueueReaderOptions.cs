using System.Diagnostics;

namespace ChannelMediator.AzureBus;

/// <summary>
/// Configuration for a topic subscription reader.
/// </summary>
public sealed class QueueReaderOptions
{
    internal QueueReaderOptions()
    {
    }
    /// <summary>
    /// Gets or sets the topic name.
    /// </summary>
    public required string QueueName { get; set; }

    /// <summary>
    /// Gets the request type this reader handles.
    /// </summary>
    public required Type RequestType { get; set; }

    /// <summary>
    /// Gets or sets the delegate that handles messages of type TMessage.
    /// </summary>
    /// <remarks>Assign a method to this property to define custom processing logic for incoming messages. The
    /// handler is invoked with each message as it is received.</remarks>
    internal object Handler { get; set; } = default!;

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
    /// Gets or sets the number of messages to prefetch from the queue.
    /// A higher value can improve throughput by reducing round-trips. Default is 0 (SDK default).
    /// </summary>
    public int PrefetchCount { get; set; }
}
