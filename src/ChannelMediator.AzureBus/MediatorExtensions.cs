using ChannelMediator.AzureBus;

namespace ChannelMediator;

/// <summary>
/// Extension methods for IMediator to support Azure Service Bus global publishing.
/// </summary>
public static class MediatorExtensions
{
    private static IAzurePublisher? _globalPublisher;
    private static readonly object _lock = new();

    /// <summary>
    /// Sets the global publisher instance. This is called internally during service configuration.
    /// </summary>
    /// <param name="globalPublisher">The global publisher instance.</param>
    internal static void SetGlobalPublisher(IAzurePublisher globalPublisher)
    {
        lock (_lock)
        {
            _globalPublisher = globalPublisher;
        }
    }

    /// <summary>
    /// Publishes a notification globally to Azure Service Bus.
    /// The notification is sent to the topic registered for its type via AddAzureBusTopicReader.
    /// </summary>
    /// <typeparam name="TNotification">The type of the notification.</typeparam>
    /// <param name="mediator">The mediator instance.</param>
    /// <param name="notification">The notification to publish.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when Azure Service Bus is not configured or no topic is registered for the notification type.
    /// </exception>
    public static Task GlobalNotify<TNotification>(this IMediator mediator, TNotification notification, CancellationToken cancellationToken = default)
        where TNotification : INotification
    {
        ArgumentNullException.ThrowIfNull(mediator);
        ArgumentNullException.ThrowIfNull(notification);

        var publisher = _globalPublisher 
            ?? throw new InvalidOperationException(
                "GlobalPublisher is not configured. Ensure UseAzureServiceBus() has been called during service configuration.");

        return publisher.PublishAsync(notification, cancellationToken);
    }

    /// <summary>
    /// Publishes a message to the specified global topic .
    /// </summary>
    /// <param name=""></param>
    /// <param name="topicName">The name of the topic to which the message will be published. Cannot be null or empty.</param>
    /// <param name="message">The message object to publish. Cannot be null.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the publish operation.</param>
    /// <returns>A task that represents the asynchronous publish operation.</returns>
    /// <exception cref="NotImplementedException">The method is not implemented.</exception>
    public static Task GlobalPublish(this IMediator mediator, string topicName, object message, CancellationToken cancellationToken = default)
    {
        var publisher = _globalPublisher
            ?? throw new InvalidOperationException(
                "GlobalPublisher is not configured. Ensure UseAzureServiceBus() has been called during service configuration.");

        return publisher.PublishAsync(topicName, message, cancellationToken);
    }

    /// <summary>
    /// Enqueues a request for asynchronous processing using the globally configured publisher.
    /// </summary>
    /// <param name="mediator">The mediator instance used to dispatch the request. Cannot be null.</param>
    /// <param name="request">The request object to enqueue. Must implement the IRequest interface. Cannot be null.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the enqueue operation.</param>
    /// <returns>A task that represents the asynchronous enqueue operation.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the global publisher is not configured. Ensure UseAzureServiceBus() has been called during service
    /// configuration.</exception>
    /// <exception cref="ArgumentException">Thrown if the request object does not implement the IRequest interface.</exception>
    public static Task EnqueueRequest(this IMediator mediator, object request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(mediator);
        ArgumentNullException.ThrowIfNull(request);
        var publisher = _globalPublisher 
            ?? throw new InvalidOperationException(
                "GlobalPublisher is not configured. Ensure UseAzureServiceBus() has been called during service configuration.");

        // On verifie que l'objet implémente IRequest<TResponse> ou IRequest
        if (request is not IRequest)
        {
            throw new ArgumentException("The request object must implement only IRequest.", nameof(request));
        }

        return publisher.EnqueueRequest(request, cancellationToken);
    }

    /// <summary>
    /// Enqueues a message to the specified queue using the configured global publisher.
    /// </summary>
    /// <param name="mediator">The mediator instance used to access the global publisher. Cannot be null.</param>
    /// <param name="queueName">The name of the queue to which the message will be enqueued. Cannot be null.</param>
    /// <param name="message">The message object to enqueue. Cannot be null.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the enqueue operation.</param>
    /// <returns>A task that represents the asynchronous enqueue operation.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the global publisher is not configured. Ensure that UseAzureServiceBus() has been called during
    /// service configuration.</exception>
    public static Task Enqueue(this IMediator mediator, string queueName, object message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(mediator);
        ArgumentNullException.ThrowIfNull(queueName);
        ArgumentNullException.ThrowIfNull(message);
        var publisher = _globalPublisher 
            ?? throw new InvalidOperationException(
                "GlobalPublisher is not configured. Ensure UseAzureServiceBus() has been called during service configuration.");
        return publisher.Enqueue(queueName, message, cancellationToken);
    }
}
