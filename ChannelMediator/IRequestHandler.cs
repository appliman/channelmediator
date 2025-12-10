namespace ChannelMediator;

public interface IRequestHandler<TRequest, TResponse> where TRequest : IRequest<TResponse>
{
	ValueTask<TResponse> HandleAsync(TRequest request, CancellationToken cancellationToken);
}
