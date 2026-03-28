
namespace ChannelMediator.AzureBus;

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
	public static Task Notify<TNotification>(this IMediator mediator, TNotification notification, CancellationToken cancellationToken = default)
		where TNotification : INotification
	{
		ArgumentNullException.ThrowIfNull(mediator);
		ArgumentNullException.ThrowIfNull(notification);

		var publisher = _globalPublisher 
			?? throw new InvalidOperationException(
				"GlobalPublisher is not configured. Ensure UseChannelMediatorAzureBus() has been called during service configuration.");

		if (publisher is MockAzurePublisher)
		{
			return publisher.Notify(notification, cancellationToken);
		}

		_ = Task.Run(() => publisher.Notify(notification, cancellationToken), cancellationToken);
		return Task.CompletedTask;
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
    public static Task EnqueueRequest<R>(this IMediator mediator, R request, CancellationToken cancellationToken = default)
        where R : IRequest
    {
        ArgumentNullException.ThrowIfNull(mediator);
        ArgumentNullException.ThrowIfNull(request);
        var publisher = _globalPublisher 
            ?? throw new InvalidOperationException(
				"GlobalPublisher is not configured. Ensure UseChannelMediatorAzureBus() has been called during service configuration.");

        // Verify that the object implements IRequest<TResponse> or IRequest
        if (request is not IRequest)
        {
            throw new ArgumentException("The request object must implement only IRequest.", nameof(request));
        }

        if (publisher is MockAzurePublisher)
        {
            return publisher.EnqueueRequest(request, cancellationToken);
        }

        _ = Task.Run(() => publisher.EnqueueRequest(request, cancellationToken), cancellationToken);
        return Task.CompletedTask;
    }
}

/// <summary>
/// Represents metrics captured by the mock mode recording mediator.
/// </summary>
public readonly record struct RecordingMetrics(int PublishCalls, int SendCalls);
