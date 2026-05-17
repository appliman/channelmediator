namespace ChannelMediator.RabbitMQ;

/// <summary>
/// Interface for publishing notifications and requests to RabbitMQ.
/// </summary>
internal interface IRabbitMqPublisher
{
    /// <summary>
    /// Publishes a notification to the RabbitMQ exchange corresponding to the notification type.
    /// </summary>
    /// <typeparam name="T">The type of the notification.</typeparam>
    /// <param name="notification">The notification to publish.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task Notify<T>(T notification, CancellationToken cancellationToken = default)
        where T : INotification;

    /// <summary>
    /// Enqueues a request for asynchronous processing via RabbitMQ.
    /// </summary>
    /// <typeparam name="R">The type of the request.</typeparam>
    /// <param name="request">The request to enqueue.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task EnqueueRequest<R>(R request, CancellationToken cancellationToken = default)
        where R : IRequest;
}
