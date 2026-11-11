namespace ChannelMediator;

/// <summary>Bounds root request chains without making awaited nested sends wait for their parent.</summary>
internal sealed class RequestChainLimiter
{
	private readonly SemaphoreSlim _semaphore;
	private readonly AsyncLocal<Lease?> _current = new();

	public RequestChainLimiter(int capacity)
	{
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);
		_semaphore = new SemaphoreSlim(capacity, capacity);
	}

	public bool IsNested => _current.Value is { IsActive: true };

	public async ValueTask DispatchAsync(IRequestEnvelope envelope,
		FrozenDictionary<Type, IRequestHandlerWrapper> handlers, CancellationToken cancellationToken)
	{
		// A pump may inherit an ExecutionContext when created by a handler. Pump work
		// is always a new root; only Send may opt into inline nested dispatch.
		await _semaphore.WaitAsync(cancellationToken);
		var previous = _current.Value;
		var lease = new Lease();
		_current.Value = lease;
		try
		{
			await envelope.DispatchAsync(handlers, cancellationToken);
		}
		finally
		{
			lease.Deactivate();
			_current.Value = previous;
			_semaphore.Release();
		}
	}

	private sealed class Lease
	{
		private int _active = 1;
		public bool IsActive => Volatile.Read(ref _active) == 1;
		public void Deactivate() => Interlocked.Exchange(ref _active, 0);
	}
}
