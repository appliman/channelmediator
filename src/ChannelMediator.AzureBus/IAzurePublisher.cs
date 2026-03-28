namespace ChannelMediator.AzureBus;

/// <summary>
/// Interface for publishing notifications globally to Azure Service Bus topics
/// based on the registered topic subscription readers.
/// </summary>
internal interface IAzurePublisher
{
    /// <summary>
    /// Notifies the appropriate Azure Service Bus topic with a notification
    /// based on the notification type registration in the TopicSubscriptionReaderRegistry.
    /// </summary>
    /// <typeparam name="TNotification">The type of the notification.</typeparam>
    /// <param name="notification">The notification to publish.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when no topic is registered for the notification type.
    /// </exception>
    Task Notify<T>(T notification, CancellationToken cancellationToken = default)
        where T : INotification;

    /// <summary>
    /// Enqueues the specified request for asynchronous processing.
    /// </summary>
    /// <param name="request">The request object to be enqueued. Cannot be null.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the enqueue operation.</param>
    /// <returns>A task that represents the asynchronous enqueue operation.</returns>
    Task EnqueueRequest<R>(R request, CancellationToken cancellationToken = default)
        where R : IRequest;
}
