namespace ChannelMediator;

internal sealed class RequestEnvelope<TResponse> : IRequestEnvelope
{
	private readonly IRequest<TResponse> _request;
	private readonly TaskCompletionSource<TResponse> _completionSource;
	private readonly CancellationToken _callerToken;

	public RequestEnvelope(IRequest<TResponse> request, TaskCompletionSource<TResponse> completionSource, CancellationToken callerToken)
	{
		_request = request;
		_completionSource = completionSource;
		_callerToken = callerToken;
	}

	public async ValueTask DispatchAsync(IReadOnlyDictionary<Type, IRequestHandlerWrapper> handlers, CancellationToken dispatcherToken)
	{
		if (!handlers.TryGetValue(_request.GetType(), out var handler))
		{
			_completionSource.TrySetException(new InvalidOperationException($"No handler registered for {_request.GetType().Name}."));
			return;
		}

		if (_callerToken.IsCancellationRequested)
		{
			_completionSource.TrySetCanceled(_callerToken);
			return;
		}

		try
		{
			using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(dispatcherToken, _callerToken);
			var response = await handler.HandleAsync(_request, linkedCts.Token);
			_completionSource.TrySetResult((TResponse)response);
		}
		catch (OperationCanceledException exception)
		{
			_completionSource.TrySetCanceled(exception.CancellationToken);
		}
		catch (Exception exception)
		{
			_completionSource.TrySetException(exception);
		}
	}
}
