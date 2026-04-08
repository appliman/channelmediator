namespace ChannelMediator.RabbitMQ;

/// <summary>
/// Extension methods for IMediator to support RabbitMQ global publishing.
/// </summary>
public static class MediatorExtensions
{
    private static IRabbitMqPublisher? _globalPublisher;
    private static readonly object _lock = new();

    /// <summary>
    /// Sets the global publisher instance. Called internally during service configuration.
    /// </summary>
    internal static void SetGlobalPublisher(IRabbitMqPublisher globalPublisher)
    {
        lock (_lock)
        {
            _globalPublisher = globalPublisher;
        }
    }

    /// <summary>
    /// Publishes a notification globally to RabbitMQ.
    /// The notification is sent to the exchange registered for its type.
    /// </summary>
    /// <typeparam name="TNotification">The type of the notification.</typeparam>
    /// <param name="mediator">The mediator instance.</param>
    /// <param name="notification">The notification to publish.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public static Task Notify<TNotification>(this IMediator mediator, TNotification notification, CancellationToken cancellationToken = default)
        where TNotification : INotification
    {
        ArgumentNullException.ThrowIfNull(mediator);
        ArgumentNullException.ThrowIfNull(notification);

        var publisher = _globalPublisher
            ?? throw new InvalidOperationException(
                "GlobalPublisher is not configured. Ensure UseChannelMediatorRabbitMQ() has been called during service configuration.");

        if (publisher is MockRabbitMqPublisher)
        {
            return publisher.Notify(notification, cancellationToken);
        }

        _ = Task.Run(() => publisher.Notify(notification, cancellationToken), cancellationToken);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Enqueues a request for asynchronous processing using the globally configured RabbitMQ publisher.
    /// </summary>
    /// <typeparam name="R">The type of the request.</typeparam>
    /// <param name="mediator">The mediator instance.</param>
    /// <param name="request">The request to enqueue.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public static Task EnqueueRequest<R>(this IMediator mediator, R request, CancellationToken cancellationToken = default)
        where R : IRequest
    {
        ArgumentNullException.ThrowIfNull(mediator);
        ArgumentNullException.ThrowIfNull(request);

        var publisher = _globalPublisher
            ?? throw new InvalidOperationException(
                "GlobalPublisher is not configured. Ensure UseChannelMediatorRabbitMQ() has been called during service configuration.");

        if (request is not IRequest)
        {
            throw new ArgumentException("The request object must implement only IRequest.", nameof(request));
        }

        if (publisher is MockRabbitMqPublisher)
        {
            return publisher.EnqueueRequest(request, cancellationToken);
        }

        _ = Task.Run(() => publisher.EnqueueRequest(request, cancellationToken), cancellationToken);
        return Task.CompletedTask;
    }
}
