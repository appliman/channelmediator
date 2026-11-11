namespace ChannelMediator;

internal sealed class Mediator : IMediator, IAsyncDisposable, IDisposable
{
	private readonly FrozenDictionary<Type, IRequestHandlerWrapper> _handlers;
	private readonly FrozenDictionary<Type, INotificationHandlerWrapper> _notificationHandlers;
	private readonly FrozenDictionary<Type, IStreamRequestHandlerWrapper> _streamHandlers;
	private readonly IServiceProvider _serviceProvider;
	private readonly ChannelMediatorConfiguration _notificationConfiguration;
	private readonly Channel<IRequestEnvelope> _channel;
	private readonly CancellationTokenSource _cts = new();
	private readonly Task _pump;
	private readonly ILogger<Mediator> _logger;
	private readonly RequestChainLimiter? _requestChainLimiter;
	private readonly object _disposeLock = new();
	private Task? _disposeTask;
	private int _inlineCount;
	private TaskCompletionSource? _inlineCompleted;

	internal Mediator(
		FrozenDictionary<Type, IRequestHandlerWrapper> handlers,
		FrozenDictionary<Type, INotificationHandlerWrapper>? notificationHandlers = null,
		IServiceProvider? serviceProvider = null,
		ChannelMediatorConfiguration? notificationConfiguration = null,
		FrozenDictionary<Type, IStreamRequestHandlerWrapper>? streamHandlers = null,
		RequestChainLimiter? requestChainLimiter = null)
	{
		_handlers = handlers ?? throw new ArgumentNullException(nameof(handlers));
		_notificationHandlers = notificationHandlers ?? FrozenDictionary<Type, INotificationHandlerWrapper>.Empty;
		_streamHandlers = streamHandlers ?? FrozenDictionary<Type, IStreamRequestHandlerWrapper>.Empty;
		_serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
		_notificationConfiguration = notificationConfiguration ?? new ChannelMediatorConfiguration();
		_logger = _serviceProvider.GetService<ILogger<Mediator>>() ?? NullLogger<Mediator>.Instance;
		_requestChainLimiter = requestChainLimiter;
		_channel = _notificationConfiguration.QueueCapacity is int capacity
			? Channel.CreateBounded<IRequestEnvelope>(new BoundedChannelOptions(capacity)
			{
				SingleReader = true,
				SingleWriter = false,
				FullMode = BoundedChannelFullMode.Wait
			})
			: Channel.CreateUnbounded<IRequestEnvelope>(new UnboundedChannelOptions
			{
				SingleReader = true,
				SingleWriter = false
			});

		_pump = Task.Run(ProcessAsync);
	}

	// Constructor for tests - converts IEnumerable to Dictionary
	public Mediator(IEnumerable<IRequestHandlerWrapper> handlers)
		: this(handlers.ToFrozenDictionary(h => h.RequestType), serviceProvider: new ServiceCollection().BuildServiceProvider())
	{
	}

	/// <inheritdoc />
	public async Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
	{
		if (request is null)
		{
			throw new ArgumentNullException(nameof(request));
		}

		var completionSource = new TaskCompletionSource<TResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
		var envelope = new RequestEnvelope<TResponse>(request, completionSource, cancellationToken);
		using var registration = cancellationToken.UnsafeRegister(
			static (state, token) => ((IRequestEnvelope)state!).Cancel(token), envelope);

		if (_requestChainLimiter is { IsNested: true } && TryStartInline())
		{
			// Awaited descendants share the root permit. Queueing here could deadlock
			// against their parent or a different request waiting for the same permit.
			try
			{
				await envelope.DispatchAsync(_handlers, _cts.Token);
			}
			finally
			{
				lock (_disposeLock)
				{
					if (--_inlineCount == 0)
					{
						_inlineCompleted!.TrySetResult();
					}
				}
			}
		}
		else
		{
			try
			{
				await _channel.Writer.WriteAsync(envelope, cancellationToken);
			}
			catch
			{
				envelope.Cancel(cancellationToken);
				throw;
			}
		}

		var result = await completionSource.Task;
		return result;
	}

	/// <inheritdoc />
	public async Task Send(IRequest request, CancellationToken cancellationToken = default)
	{
		if (request is null)
		{
			throw new ArgumentNullException(nameof(request));
		}

		await Send<Unit>(request, cancellationToken);
	}

	/// <inheritdoc />
	public async Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
		where TNotification : INotification
	{
		if (notification is null)
		{
			throw new ArgumentNullException(nameof(notification));
		}

		if (!_notificationHandlers.TryGetValue(notification.GetType(), out var wrapper))
		{
			_logger.LogTrace("No notification handler wrapper registered for type {NotificationType}. Ensure the assembly containing the INotificationHandler<{NotificationType}> implementation is passed to AddChannelMediator().",
				notification.GetType().FullName, notification.GetType().Name);
			return;
		}

		if (_notificationConfiguration.Strategy == NotificationPublishStrategy.Parallel)
		{
			await PublishParallelAsync(notification, cancellationToken);
		}
		else
		{
			await PublishSequentialAsync(notification, wrapper, cancellationToken);
		}
	}

	private async Task PublishSequentialAsync<TNotification>(
		TNotification notification,
		INotificationHandlerWrapper wrapper,
		CancellationToken cancellationToken)
		where TNotification : INotification
	{
		await wrapper.HandleAsync(notification, _serviceProvider, cancellationToken);
	}

	private async Task PublishParallelAsync<TNotification>(
		TNotification notification,
		CancellationToken cancellationToken)
		where TNotification : INotification
	{
		await using var scope = _serviceProvider.CreateAsyncScope();
		var handlers = scope.ServiceProvider.GetServices<INotificationHandler<TNotification>>();

		var handlerArray = handlers as INotificationHandler<TNotification>[] ?? handlers.ToArray();
		var tasks = new Task[handlerArray.Length];
		for (var i = 0; i < handlerArray.Length; i++)
		{
			tasks[i] = InvokeNotificationAsync(handlerArray[i], notification, cancellationToken);
		}

		await Task.WhenAll(tasks);
	}

	private static async Task InvokeNotificationAsync<TNotification>(
		INotificationHandler<TNotification> handler, TNotification notification, CancellationToken cancellationToken)
		where TNotification : INotification
	{
		// Capture synchronous exceptions so all started handlers finish before scope disposal.
		await handler.Handle(notification, cancellationToken);
	}

	/// <inheritdoc />
	public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default)
	{
		if (request is null)
		{
			throw new ArgumentNullException(nameof(request));
		}

		if (!_streamHandlers.TryGetValue(request.GetType(), out var wrapper))
		{
			throw new InvalidOperationException($"No stream handler registered for request type '{request.GetType().FullName}'.");
		}

		return CastStream<TResponse>(wrapper.HandleAsync(request, cancellationToken));
	}

	private static async IAsyncEnumerable<TResponse> CastStream<TResponse>(
		IAsyncEnumerable<object?> source,
		[System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
	{
		await foreach (var item in source.WithCancellation(cancellationToken))
		{
			yield return (TResponse)item!;
		}
	}

	private async Task ProcessAsync()
	{
		IRequestEnvelope? current = null;
		try
		{
			await foreach (var envelope in _channel.Reader.ReadAllAsync(_cts.Token))
			{
				current = envelope;
				_cts.Token.ThrowIfCancellationRequested();
				if (_requestChainLimiter is null)
				{
					await envelope.DispatchAsync(_handlers, _cts.Token);
				}
				else
				{
					await _requestChainLimiter.DispatchAsync(envelope, _handlers, _cts.Token);
				}
				current = null;
			}
		}
		catch (OperationCanceledException) when (_cts.IsCancellationRequested)
		{
			// Shutdown is cooperative for the active handler; queued work never starts.
		}
		finally
		{
			current?.Cancel(_cts.Token);
			while (_channel.Reader.TryRead(out var pending))
			{
				pending.Cancel(_cts.Token);
			}
		}
	}

	private int _disposed;

	private bool TryStartInline()
	{
		lock (_disposeLock)
		{
			if (_disposed != 0)
			{
				return false;
			}
			if (_inlineCount++ == 0)
			{
				_inlineCompleted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
			}
			return true;
		}
	}

	public void Dispose()
	{
		GetDisposeTask().GetAwaiter().GetResult();
	}

	public ValueTask DisposeAsync()
	{
		var result = new ValueTask(GetDisposeTask());
		return result;
	}

	private Task GetDisposeTask()
	{
		lock (_disposeLock)
		{
			if (_disposeTask is null)
			{
				Volatile.Write(ref _disposed, 1);
				_channel.Writer.TryComplete();
				// Synchronous DI disposal must not block a captured UI synchronization context.
				// All callers share this task and wait for the handler and its scope to finish.
				_disposeTask = Task.Run(StopAsync);
			}
			return _disposeTask;
		}
	}

	private async Task StopAsync()
	{
		try
		{
			await _cts.CancelAsync();
		}
		finally
		{
			try
			{
				await _pump;
				Task inlineTask;
				lock (_disposeLock)
				{
					inlineTask = _inlineCompleted?.Task ?? Task.CompletedTask;
				}
				await inlineTask;
			}
			finally
			{
				_cts.Dispose();
			}
		}
	}
}
