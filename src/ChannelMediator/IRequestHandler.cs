namespace ChannelMediator;

/// <summary>
/// Handler for requests that don't return a value (commands).
/// Extends <see cref="IRequestHandler{TRequest, TResponse}"/> with a default implementation
/// that bridges to the simpler <see cref="Handle"/> method, eliminating the need for reflection.
/// </summary>
public interface IRequestHandler<in TRequest> : IRequestHandler<TRequest, Unit>
    where TRequest : IRequest
{
    /// <summary>
    /// Handles the command without returning a value.
    /// </summary>
    new Task Handle(TRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Default implementation that bridges to the command handler and returns <see cref="Unit.Value"/>.
    /// </summary>
    async Task<Unit> IRequestHandler<TRequest, Unit>.Handle(TRequest request, CancellationToken cancellationToken)
    {
        await Handle(request, cancellationToken);
        return Unit.Value;
    }
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

