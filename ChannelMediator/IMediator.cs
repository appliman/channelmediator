namespace ChannelMediator;

public interface IMediator
{
	/// <summary>
	/// Sends a request to a single handler and returns the response.
	/// This is the core method used internally.
	/// </summary>
	ValueTask<TResponse> InvokeAsync<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default);

	/// <summary>
	/// Sends a request to a single handler without returning a value.
	/// This is the core method used internally for commands.
	/// </summary>
	ValueTask InvokeAsync(IRequest request, CancellationToken cancellationToken = default);

	/// <summary>
	/// Publishes a notification to multiple handlers.
	/// This is the core method used internally.
	/// </summary>
	ValueTask PublishAsync<TNotification>(TNotification notification, CancellationToken cancellationToken = default) where TNotification : INotification;

	/// <summary>
	/// Sends a request to a single handler and returns the response.
	/// MediatR-compatible method that internally calls InvokeAsync.
	/// </summary>
	Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default);

	/// <summary>
	/// Sends a request to a single handler without returning a value.
	/// MediatR-compatible method that internally calls InvokeAsync.
	/// </summary>
	Task Send(IRequest request, CancellationToken cancellationToken = default);

	/// <summary>
	/// Publishes a notification to multiple handlers.
	/// MediatR-compatible method that internally calls PublishAsync.
	/// </summary>
	Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default) where TNotification : INotification;

	/// <summary>
	/// Sends a request to a single handler and returns the response as an object.
	/// This method accepts a non-generic request object and returns the response boxed as object.
	/// </summary>
	Task<object?> InvokeAsync(object request, CancellationToken cancellationToken = default);

	/// <summary>
	/// Sends a request to a single handler and returns the response as an object.
	/// MediatR-compatible method that internally calls InvokeAsync.
	/// </summary>
	Task<object?> Send(object request, CancellationToken cancellationToken = default);
}
