namespace ChannelMediator.InMemory;

/// <summary>
/// Interface for publishing notifications and enqueuing requests in memory.
/// </summary>
internal interface IMemoryPublisher
{
	/// <summary>
	/// Publishes the specified notification through the local mediator.
	/// </summary>
	/// <typeparam name="T">The notification type.</typeparam>
	/// <param name="notification">The notification to publish.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>A task representing the asynchronous operation.</returns>
	Task Notify<T>(T notification, CancellationToken cancellationToken = default)
		where T : INotification;

	/// <summary>
	/// Dispatches the specified request through the local mediator.
	/// </summary>
	/// <typeparam name="R">The request type.</typeparam>
	/// <param name="request">The request to enqueue.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>A task representing the asynchronous operation.</returns>
	Task EnqueueRequest<R>(R request, CancellationToken cancellationToken = default)
		where R : IRequest;
}
