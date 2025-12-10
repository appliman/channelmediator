namespace ChannelMediator;

public delegate ValueTask<TResponse> RequestHandlerDelegate<TResponse>();

public interface IPipelineBehavior<in TRequest, TResponse> where TRequest : IRequest<TResponse>
{
    ValueTask<TResponse> HandleAsync(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken);
}

/// <summary>
/// Marker interface for global pipeline behaviors that apply to all request handlers.
/// Implement this instead of IPipelineBehavior to create a behavior that applies globally.
/// </summary>
public interface IPipelineBehavior
{
}
