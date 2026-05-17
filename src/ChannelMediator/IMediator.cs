namespace ChannelMediator;

/// <summary>
/// Defines a mediator to encapsulate request/response and publishing interaction patterns.
/// Compatible with MediatR interface signatures.
/// </summary>
public interface IMediator
{
	/// <summary>
	/// Asynchronously send a request to a single handler.
	/// </summary>
	/// <typeparam name="TResponse">Response type</typeparam>
	/// <param name="request">Request object</param>
	/// <param name="cancellationToken">Optional cancellation token</param>
	/// <returns>A task that represents the send operation. The task result contains the handler response.</returns>
	Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default);

	/// <summary>
	/// Asynchronously send a request to a single handler without a response.
	/// </summary>
	/// <param name="request">Request object</param>
	/// <param name="cancellationToken">Optional cancellation token</param>
	/// <returns>A task that represents the send operation.</returns>
	Task Send(IRequest request, CancellationToken cancellationToken = default);

	/// <summary>
	/// Asynchronously send a notification to multiple handlers.
	/// </summary>
	/// <typeparam name="TNotification">Notification type</typeparam>
	/// <param name="notification">Notification object</param>
	/// <param name="cancellationToken">Optional cancellation token</param>
	/// <returns>A task that represents the publish operation.</returns>
	Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default) where TNotification : INotification;

	/// <summary>
	/// Creates an asynchronous stream for the given stream request.
	/// The handler is dispatched directly (bypassing the channel pump).
	/// </summary>
	/// <typeparam name="TResponse">The type of each item yielded by the stream.</typeparam>
	/// <param name="request">The stream request object.</param>
	/// <param name="cancellationToken">Optional cancellation token.</param>
	/// <returns>An asynchronous sequence of response items.</returns>
	IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default);
}

