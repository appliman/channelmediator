namespace ChannelMediator;

/// <summary>
/// Represents the next delegate in the request handling pipeline.
/// </summary>
/// <typeparam name="TResponse">The type of response returned by the request pipeline.</typeparam>
public delegate ValueTask<TResponse> RequestHandlerDelegate<TResponse>();

/// <summary>
/// Defines a pipeline behavior that can intercept the execution of a request handler.
/// </summary>
/// <typeparam name="TRequest">The type of request being handled.</typeparam>
/// <typeparam name="TResponse">The type of response returned by the request.</typeparam>
public interface IPipelineBehavior<in TRequest, TResponse> where TRequest : IRequest<TResponse>
{
    /// <summary>
    /// Handles the specified request and optionally invokes the next delegate in the pipeline.
    /// </summary>
    /// <param name="request">The request being processed.</param>
    /// <param name="next">The delegate that invokes the next behavior or handler.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
    /// <returns>A value task that represents the asynchronous pipeline execution.</returns>
    ValueTask<TResponse> HandleAsync(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken);
}

/// <summary>
/// Marker interface for global pipeline behaviors that apply to all request handlers.
/// Implement this instead of IPipelineBehavior to create a behavior that applies globally.
/// </summary>
public interface IPipelineBehavior
{
}
