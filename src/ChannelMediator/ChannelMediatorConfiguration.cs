namespace ChannelMediator;

public class ChannelMediatorConfiguration
{
    public IServiceCollection Services { get; set; } = default!;
    public NotificationPublishStrategy Strategy { get; set; } = NotificationPublishStrategy.Sequential;

}
