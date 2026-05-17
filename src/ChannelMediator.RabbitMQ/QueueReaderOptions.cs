namespace ChannelMediator.RabbitMQ;

/// <summary>
/// Configuration for a queue reader.
/// </summary>
public sealed class QueueReaderOptions
{
    internal QueueReaderOptions()
    {
    }

    /// <summary>
    /// Gets or sets the queue name.
    /// </summary>
    public required string QueueName { get; set; }

    /// <summary>
    /// Gets the request type this reader handles.
    /// </summary>
    public required Type RequestType { get; set; }

    /// <summary>
    /// Gets or sets the delegate that handles messages.
    /// </summary>
    internal object Handler { get; set; } = default!;

    /// <summary>
    /// Gets or sets the prefetch count for this consumer. Default is 1.
    /// </summary>
    public ushort PrefetchCount { get; set; } = 1;
}
