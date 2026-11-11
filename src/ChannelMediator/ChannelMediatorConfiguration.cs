namespace ChannelMediator;

/// <summary>
/// Provides configuration options used when registering ChannelMediator services.
/// </summary>
public class ChannelMediatorConfiguration
{
    /// <summary>
    /// Gets the service collection used to register mediator-related services.
    /// </summary>
    public IServiceCollection Services { get; internal set; } = default!;

    /// <summary>
    /// Gets or sets the strategy used to publish notifications.
    /// </summary>
    public NotificationPublishStrategy Strategy { get; set; } = NotificationPublishStrategy.Sequential;

    /// <summary>
    /// Gets or sets the maximum queued requests per mediator. A positive value enables
    /// asynchronous backpressure; null preserves the unbounded queue used by existing applications.
    /// Producers must await Send and bound their own concurrency: waiting producers also retain memory.
    /// This setting does not limit notifications or streams.
    /// </summary>
    public int? QueueCapacity { get; set; }

    /// <summary>
    /// Gets or sets the maximum concurrently executing top-level request chains across mediators
    /// created by this factory. Null preserves independent execution. Must be positive when set.
    /// Nested requests execute inline in their own DI scopes and share the parent's permit to avoid
    /// deadlocks. Parallel nested requests are part of one chain, not individually limited.
    /// Notifications and streams are not limited by this setting.
    /// </summary>
    public int? MaxConcurrentRequestChains { get; set; }

}
