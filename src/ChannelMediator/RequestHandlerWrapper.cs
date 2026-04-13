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

		var behaviors = scope.ServiceProvider.GetServices<IPipelineBehavior<TRequest, TResponse>>();

		// Materialize once; iterate backward to build the pipeline from innermost to outermost
		var behaviorArray = behaviors as IPipelineBehavior<TRequest, TResponse>[] ?? behaviors.ToArray();

		RequestHandlerDelegate<TResponse> handler = async () =>
		{
			var requestHandler = scope.ServiceProvider.GetRequiredService<IRequestHandler<TRequest, TResponse>>();
			return await requestHandler.Handle(typedRequest, cancellationToken);
		};

		for (var i = behaviorArray.Length - 1; i >= 0; i--)
		{
			var currentHandler = handler;
			var behavior = behaviorArray[i];
			handler = () => behavior.HandleAsync(typedRequest, currentHandler, cancellationToken);
		}

		var response = await handler();
		return response!;
	}
}
