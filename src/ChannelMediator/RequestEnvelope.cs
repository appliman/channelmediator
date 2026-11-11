namespace ChannelMediator;

internal sealed class RequestEnvelope<TResponse> : IRequestEnvelope
{
	private readonly IRequest<TResponse> _request;
	private readonly TaskCompletionSource<TResponse> _completionSource;
	private readonly CancellationToken _callerToken;
	// Only queued requests may complete early. Running handlers must finish their scopes first.
	private int _state;
	private readonly bool _recordPending;
	private readonly long _queuedAt;

	public RequestEnvelope(IRequest<TResponse> request, TaskCompletionSource<TResponse> completionSource, CancellationToken callerToken)
	{
		_request = request;
		_completionSource = completionSource;
		_callerToken = callerToken;
		_recordPending = MediatorMetrics.Pending.Enabled;
		_queuedAt = MediatorMetrics.QueueDuration.Enabled ? System.Diagnostics.Stopwatch.GetTimestamp() : 0;
		if (_recordPending)
		{
			MediatorMetrics.Pending.Add(1);
		}
	}

	public async ValueTask DispatchAsync(FrozenDictionary<Type, IRequestHandlerWrapper> handlers, CancellationToken dispatcherToken)
	{
		if (Interlocked.CompareExchange(ref _state, 1, 0) != 0)
		{
			return;
		}

		EndPending();
		if (!handlers.TryGetValue(_request.GetType(), out var handler))
		{
			_completionSource.TrySetException(new InvalidOperationException($"No handler registered for {_request.GetType().Name}."));
			return;
		}

		if (_callerToken.IsCancellationRequested || dispatcherToken.IsCancellationRequested)
		{
			_completionSource.TrySetCanceled(_callerToken.IsCancellationRequested ? _callerToken : dispatcherToken);
			return;
		}

		var recordActive = MediatorMetrics.Active.Enabled;
		var startedAt = MediatorMetrics.ExecutionDuration.Enabled ? System.Diagnostics.Stopwatch.GetTimestamp() : 0;
		if (recordActive)
		{
			MediatorMetrics.Active.Add(1);
		}
		try
		{
			using var linkedCts = _callerToken.CanBeCanceled
				? CancellationTokenSource.CreateLinkedTokenSource(dispatcherToken, _callerToken)
				: null;
			var response = await handler.HandleAsync(_request, linkedCts?.Token ?? dispatcherToken);
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
		finally
		{
			if (recordActive)
			{
				MediatorMetrics.Active.Add(-1);
			}
			if (startedAt != 0)
			{
				MediatorMetrics.ExecutionDuration.Record(System.Diagnostics.Stopwatch.GetElapsedTime(startedAt).TotalSeconds);
			}
		}
	}

	public void Cancel(CancellationToken cancellationToken)
	{
		if (Interlocked.CompareExchange(ref _state, 2, 0) == 0)
		{
			EndPending();
			_completionSource.TrySetCanceled(cancellationToken);
		}
	}

	private void EndPending()
	{
		if (_recordPending)
		{
			MediatorMetrics.Pending.Add(-1);
		}
		if (_queuedAt != 0)
		{
			MediatorMetrics.QueueDuration.Record(System.Diagnostics.Stopwatch.GetElapsedTime(_queuedAt).TotalSeconds);
		}
	}
}
