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
		await using var scope = _serviceProvider.CreateAsyncScope();
		var typedRequest = (TRequest)request;
		var behaviors = scope.ServiceProvider.GetServices<IPipelineBehavior<TRequest, TResponse>>();
		var behaviorArray = behaviors as IPipelineBehavior<TRequest, TResponse>[] ?? behaviors.ToArray();

		TResponse result;
		if (behaviorArray.Length == 0)
		{
			var handler = scope.ServiceProvider.GetRequiredService<IRequestHandler<TRequest, TResponse>>();
			result = await handler.Handle(typedRequest, cancellationToken);
		}
		else
		{
			result = await InvokePipelineAsync(scope.ServiceProvider, typedRequest, behaviorArray, cancellationToken);
		}

		return result!;
	}

	private static ValueTask<TResponse> InvokePipelineAsync(
		IServiceProvider serviceProvider,
		TRequest request,
		IPipelineBehavior<TRequest, TResponse>[] behaviors,
		CancellationToken cancellationToken)
	{
		// Resolve lazily: a behavior may return without constructing the handler.
		RequestHandlerDelegate<TResponse> handler = () =>
		{
			var requestHandler = serviceProvider.GetRequiredService<IRequestHandler<TRequest, TResponse>>();
			var result = new ValueTask<TResponse>(requestHandler.Handle(request, cancellationToken));
			return result;
		};

		for (var i = behaviors.Length - 1; i >= 0; i--)
		{
			var currentHandler = handler;
			var behavior = behaviors[i];
			handler = () => behavior.HandleAsync(request, currentHandler, cancellationToken);
		}

		var result = handler();
		return result;
	}
}
