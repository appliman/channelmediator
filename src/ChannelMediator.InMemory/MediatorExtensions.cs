namespace ChannelMediator.InMemory;

/// <summary>
/// Extension methods for IMediator to support in-memory publishing.
/// </summary>
public static class MediatorExtensions
{
	private static IMemoryPublisher? _globalPublisher;
	private static readonly object Lock = new();

	/// <summary>
	/// Sets the global publisher instance. This is called internally during service configuration.
	/// </summary>
	/// <param name="globalPublisher">The global publisher instance.</param>
	internal static void SetGlobalPublisher(IMemoryPublisher globalPublisher)
	{
		lock (Lock)
		{
			_globalPublisher = globalPublisher;
		}
	}

	/// <summary>
	/// Publishes a notification in memory through the configured mediator.
	/// </summary>
	/// <typeparam name="TNotification">The notification type.</typeparam>
	/// <param name="mediator">The mediator instance.</param>
	/// <param name="notification">The notification to publish.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>A completed task once the background work has been scheduled.</returns>
	public static Task Notify<TNotification>(
		this IMediator mediator,
		TNotification notification,
		CancellationToken cancellationToken = default)
		where TNotification : INotification
	{
		ArgumentNullException.ThrowIfNull(mediator);
		ArgumentNullException.ThrowIfNull(notification);

		var publisher = _globalPublisher
			?? throw new InvalidOperationException(
				"GlobalPublisher is not configured. Ensure UseChannelMediatorInMemory() has been called during service configuration.");

		_ = Task.Run(() => publisher.Notify(notification, cancellationToken), CancellationToken.None);
		return Task.CompletedTask;
	}

	/// <summary>
	/// Enqueues a request in memory through the configured mediator.
	/// </summary>
	/// <typeparam name="R">The request type.</typeparam>
	/// <param name="mediator">The mediator instance.</param>
	/// <param name="request">The request to enqueue.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>A completed task once the background work has been scheduled.</returns>
	public static Task EnqueueRequest<R>(this IMediator mediator, R request, CancellationToken cancellationToken = default)
		where R : IRequest
	{
		ArgumentNullException.ThrowIfNull(mediator);
		ArgumentNullException.ThrowIfNull(request);

		var publisher = _globalPublisher
			?? throw new InvalidOperationException(
				"GlobalPublisher is not configured. Ensure UseChannelMediatorInMemory() has been called during service configuration.");

		_ = Task.Run(() => publisher.EnqueueRequest(request, cancellationToken), CancellationToken.None);
		return Task.CompletedTask;
	}
}
