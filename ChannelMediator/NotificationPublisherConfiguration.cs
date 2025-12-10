namespace ChannelMediator;

public enum NotificationPublishStrategy
{
    Sequential,
    Parallel
}

public class NotificationPublisherConfiguration
{
    public NotificationPublishStrategy Strategy { get; set; } = NotificationPublishStrategy.Sequential;
}
