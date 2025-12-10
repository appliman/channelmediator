namespace ChannelMediator;

internal sealed class RequestHandlerWrapper<TRequest, TResponse> 
	: IRequestHandlerWrapper where TRequest : IRequest<TResponse>
{
	private readonly IServiceProvider _serviceProvider;

	public RequestHandlerWrapper(IServiceProvider serviceProvider)
	{
		_serviceProvider = serviceProvider;
	}

	public System.Type RequestType => typeof(TRequest);

	public async ValueTask<object> HandleAsync(object request, CancellationToken cancellationToken)
	{
		using var scope = _serviceProvider.CreateScope();
		var typedRequest = (TRequest)request;

		var behaviors = scope.ServiceProvider.GetServices<IPipelineBehavior<TRequest, TResponse>>().Reverse().ToList();

		RequestHandlerDelegate<TResponse> handler = async () =>
		{
			var requestHandler = scope.ServiceProvider.GetRequiredService<IRequestHandler<TRequest, TResponse>>();
			return await requestHandler.HandleAsync(typedRequest, cancellationToken).ConfigureAwait(false);
		};

		foreach (var behavior in behaviors)
		{
			var currentHandler = handler;
			handler = () => behavior.HandleAsync(typedRequest, currentHandler, cancellationToken);
		}

		var response = await handler().ConfigureAwait(false);
		return response!;
	}
}
