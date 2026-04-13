namespace ChannelMediator;

internal sealed class Mediator : IMediator, IAsyncDisposable, IDisposable
{
	private readonly FrozenDictionary<Type, IRequestHandlerWrapper> _handlers;
	private readonly FrozenDictionary<Type, INotificationHandlerWrapper> _notificationHandlers;
	private readonly IServiceProvider _serviceProvider;
	private readonly ChannelMediatorConfiguration _notificationConfiguration;
	private readonly Channel<IRequestEnvelope> _channel;
	private readonly CancellationTokenSource _cts = new();
	private readonly Task _pump;

	internal Mediator(
		FrozenDictionary<Type, IRequestHandlerWrapper> handlers,
		FrozenDictionary<Type, INotificationHandlerWrapper>? notificationHandlers = null,
		IServiceProvider? serviceProvider = null,
		ChannelMediatorConfiguration? notificationConfiguration = null)
	{
		_handlers = handlers ?? throw new ArgumentNullException(nameof(handlers));
		_notificationHandlers = notificationHandlers ?? FrozenDictionary<Type, INotificationHandlerWrapper>.Empty;
		_serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
		_notificationConfiguration = notificationConfiguration ?? new ChannelMediatorConfiguration();
		_channel = Channel.CreateUnbounded<IRequestEnvelope>(new UnboundedChannelOptions
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

		await _channel.Writer.WriteAsync(envelope, cancellationToken);

		return await completionSource.Task;
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
		using var scope = _serviceProvider.CreateScope();
		var handlers = scope.ServiceProvider.GetServices<INotificationHandler<TNotification>>();

		var handlerArray = handlers as INotificationHandler<TNotification>[] ?? handlers.ToArray();
		var tasks = new Task[handlerArray.Length];
		for (var i = 0; i < handlerArray.Length; i++)
		{
			tasks[i] = handlerArray[i].Handle(notification, cancellationToken);
		}

		await Task.WhenAll(tasks);
	}

	private async Task ProcessAsync()
	{
		try
		{
			await foreach (var envelope in _channel.Reader.ReadAllAsync(_cts.Token))
			{
				await envelope.DispatchAsync(_handlers, _cts.Token);
			}
		}
		catch (OperationCanceledException)
		{
			// Shutdown requested
		}
	}

	private int _disposed;

	public void Dispose()
	{
		if (Interlocked.Exchange(ref _disposed, 1) == 1)
		{
			return;
		}

		_channel.Writer.TryComplete();
		_cts.Cancel();

		try
		{
			_pump.GetAwaiter().GetResult();
		}
		catch (OperationCanceledException)
		{
			// Dead for science
		}

		_cts.Dispose();
	}

	public async ValueTask DisposeAsync()
	{
		if (Interlocked.Exchange(ref _disposed, 1) == 1)
		{
			return;
		}

		_channel.Writer.TryComplete();

		if (!_cts.IsCancellationRequested)
		{
			await _cts.CancelAsync();
		}

		try
		{
			await _pump;
		}
		catch (OperationCanceledException)
		{
			// Dead for science
		}

		_cts.Dispose();
	}
}

