namespace ChannelMediator.AzureBus;

/// <summary>
/// Interface for publishing notifications globally to Azure Service Bus topics
/// based on the registered topic subscription readers.
/// </summary>
public interface IGlobalPublisher
{
    /// <summary>
    /// Publishes a notification to the appropriate Azure Service Bus topic
    /// based on the notification type registration in the TopicSubscriptionReaderRegistry.
    /// </summary>
    /// <typeparam name="TNotification">The type of the notification.</typeparam>
    /// <param name="notification">The notification to publish.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when no topic is registered for the notification type.
    /// </exception>
    Task PublishAsync<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
        where TNotification : INotification;
}
