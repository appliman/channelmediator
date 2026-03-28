namespace ChannelMediator;

public class ChannelMediatorConfiguration
{
    internal static bool Await { get; set; } = false;
    public IServiceCollection Services { get; set; } = default!;
    public NotificationPublishStrategy Strategy { get; set; } = NotificationPublishStrategy.Sequential;

}
