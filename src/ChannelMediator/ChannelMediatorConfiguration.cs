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

}
