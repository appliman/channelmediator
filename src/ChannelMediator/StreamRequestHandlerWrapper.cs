namespace ChannelMediator;

internal sealed class StreamRequestHandlerWrapper<TRequest, TResponse>
	: IStreamRequestHandlerWrapper where TRequest : IStreamRequest<TResponse>
{
	private readonly IServiceProvider _serviceProvider;

	public StreamRequestHandlerWrapper(IServiceProvider serviceProvider)
	{
		_serviceProvider = serviceProvider;
	}

	public Type RequestType => typeof(TRequest);

	public IAsyncEnumerable<object?> HandleAsync(object request, CancellationToken cancellationToken)
	{
		return HandleTypedAsync((TRequest)request, cancellationToken);
	}

	private async IAsyncEnumerable<object?> HandleTypedAsync(
		TRequest request,
		[System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
	{
		// The scope lives for the full duration of the enumeration
		await using var scope = _serviceProvider.CreateAsyncScope();

		var behaviors = scope.ServiceProvider
			.GetServices<IStreamPipelineBehavior<TRequest, TResponse>>();
		var behaviorArray = behaviors as IStreamPipelineBehavior<TRequest, TResponse>[] ?? behaviors.ToArray();

		StreamHandlerDelegate<TResponse> handler = () =>
		{
			var requestHandler = scope.ServiceProvider
				.GetRequiredService<IStreamRequestHandler<TRequest, TResponse>>();
			return requestHandler.Handle(request, cancellationToken);
		};

		for (var i = behaviorArray.Length - 1; i >= 0; i--)
		{
			var currentHandler = handler;
			var behavior = behaviorArray[i];
			handler = () => behavior.Handle(request, currentHandler, cancellationToken);
		}

		await foreach (var item in handler().WithCancellation(cancellationToken))
		{
			yield return item;
		}
	}
}
