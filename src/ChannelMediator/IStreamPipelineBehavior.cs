namespace ChannelMediator;

/// <summary>
/// Represents the next delegate in a streaming request pipeline.
/// </summary>
/// <typeparam name="TResponse">The type of each item yielded by the stream.</typeparam>
public delegate IAsyncEnumerable<TResponse> StreamHandlerDelegate<out TResponse>();

/// <summary>
/// Defines a pipeline behavior that can intercept the execution of a streaming request handler.
/// </summary>
/// <typeparam name="TRequest">The type of stream request being handled.</typeparam>
/// <typeparam name="TResponse">The type of each item yielded by the stream.</typeparam>
public interface IStreamPipelineBehavior<in TRequest, TResponse>
	where TRequest : IStreamRequest<TResponse>
{
	/// <summary>
	/// Handles the specified stream request and optionally invokes the next delegate in the pipeline.
	/// </summary>
	/// <param name="request">The stream request being processed.</param>
	/// <param name="next">The delegate that invokes the next behavior or handler.</param>
	/// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
	/// <returns>An asynchronous sequence of response items.</returns>
	IAsyncEnumerable<TResponse> Handle(TRequest request, StreamHandlerDelegate<TResponse> next, CancellationToken cancellationToken);
}
