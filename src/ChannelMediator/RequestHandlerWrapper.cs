namespace ChannelMediator;

internal sealed class RequestHandlerWrapper<TRequest, TResponse>
	: IRequestHandlerWrapper where TRequest : IRequest<TResponse>
{
	private static readonly bool IsCommand = typeof(TResponse) == typeof(Unit)
		&& typeof(TRequest).GetInterfaces().Any(i => i == typeof(IRequest));

	private static readonly Type? CommandHandlerType = IsCommand
		? typeof(IRequestHandler<>).MakeGenericType(typeof(TRequest))
		: null;

	private static readonly System.Reflection.MethodInfo? HandleMethod = CommandHandlerType?.GetMethod("Handle");

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

		RequestHandlerDelegate<TResponse> handler;

		// Check if this is a command (IRequest -> IRequest<Unit>)
		if (IsCommand)
		{
			// Try to get IRequestHandler<TRequest> first (for commands)
			var commandHandler = scope.ServiceProvider.GetService(CommandHandlerType!);

			if (commandHandler != null)
			{
				handler = async () =>
				{
					try
					{
						var task = (Task)HandleMethod!.Invoke(commandHandler, new object[] { typedRequest, cancellationToken })!;
						await task;
						return (TResponse)(object)Unit.Value;
					}
					catch (System.Reflection.TargetInvocationException ex) when (ex.InnerException != null)
					{
						// Unwrap reflection exception to get the actual exception
						System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
						throw; // This line will never be reached but is required for compilation
					}
				};
			}
			else
			{
				// Fallback to IRequestHandler<TRequest, Unit>
				handler = async () =>
				{
					var requestHandler = scope.ServiceProvider.GetRequiredService<IRequestHandler<TRequest, TResponse>>();
					return await requestHandler.Handle(typedRequest, cancellationToken);
				};
			}
		}
		else
		{
			// Regular request handler
			handler = async () =>
			{
				var requestHandler = scope.ServiceProvider.GetRequiredService<IRequestHandler<TRequest, TResponse>>();
				return await requestHandler.Handle(typedRequest, cancellationToken);
			};
		}

		foreach (var behavior in behaviors)
		{
			var currentHandler = handler;
			handler = () => behavior.HandleAsync(typedRequest, currentHandler, cancellationToken);
		}

		var response = await handler();
		return response!;
	}
}
