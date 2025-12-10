namespace ChannelMediator;

public interface IRequestHandler<TRequest, TResponse> where TRequest : IRequest<TResponse>
{
	ValueTask<TResponse> HandleAsync(TRequest request, CancellationToken cancellationToken);
}

/// <summary>
/// Handler for requests that don't return a value (commands).
/// </summary>
public interface IRequestHandler<TRequest> : IRequestHandler<TRequest, Unit>
	where TRequest : IRequest<Unit>
{
	async ValueTask<Unit> IRequestHandler<TRequest, Unit>.HandleAsync(TRequest request, CancellationToken cancellationToken)
	{
		await HandleAsync(request, cancellationToken).ConfigureAwait(false);
		return Unit.Value;
	}

	/// <summary>
	/// Handles a request without returning a value.
	/// </summary>
	new ValueTask HandleAsync(TRequest request, CancellationToken cancellationToken);
}
