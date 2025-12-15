namespace ChannelMediator.AzureBus;

/// <summary>
/// Interface for publishing notifications globally to Azure Service Bus topics
/// based on the registered topic subscription readers.
/// </summary>
internal interface IAzurePublisher
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

    /// <summary>
    /// Enqueues the specified request for asynchronous processing.
    /// </summary>
    /// <param name="request">The request object to be enqueued. Cannot be null.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the enqueue operation.</param>
    /// <returns>A task that represents the asynchronous enqueue operation.</returns>
    Task EnqueueRequest(object request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Enqueues a message to the specified queue for asynchronous processing.
    /// </summary>
    /// <param name="queueName">The name of the queue to which the message will be enqueued. Cannot be null or empty.</param>
    /// <param name="message">The message object to enqueue. Cannot be null.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the enqueue operation.</param>
    /// <returns>A task that represents the asynchronous enqueue operation.</returns>
    Task Enqueue(string queueName, object message, CancellationToken cancellationToken = default);
}
