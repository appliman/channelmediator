namespace ChannelMediator;

/// <summary>
/// Handler for requests that don't return a value (commands).
/// </summary>
public interface IRequestHandler<in TRequest>
    where TRequest : IRequest
{
    /// <summary>
    /// MediatR-compatible alias for HandleAsync.
    /// </summary>
    Task Handle(TRequest request, CancellationToken cancellationToken);
}


/// <summary>
/// Handler for requests that return a response value.
/// </summary>
/// <typeparam name="TRequest">The type of request handled by this instance.</typeparam>
/// <typeparam name="TResponse">The type of response returned by the handler.</typeparam>
public interface IRequestHandler<in TRequest, TResponse> 
	where TRequest : IRequest<TResponse>
{
	/// <summary>
	/// MediatR-compatible alias for HandleAsync.
	/// </summary>
	Task<TResponse> Handle(TRequest request, CancellationToken cancellationToken);
}

