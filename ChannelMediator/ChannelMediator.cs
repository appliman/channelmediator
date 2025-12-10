namespace ChannelMediator;

public sealed class ChannelMediator : IMediator, IAsyncDisposable
{
	private readonly IReadOnlyDictionary<Type, IRequestHandlerWrapper> _handlers;
	private readonly IReadOnlyDictionary<Type, INotificationHandlerWrapper> _notificationHandlers;
	private readonly IServiceProvider _serviceProvider;
	private readonly NotificationPublisherConfiguration _notificationConfiguration;
	private readonly Channel<IRequestEnvelope> _channel;
	private readonly CancellationTokenSource _cts = new();
	private readonly Task _pump;

	public ChannelMediator(IEnumerable<IRequestHandlerWrapper> handlers)
	{
		_handlers = handlers.ToDictionary(handler => handler.RequestType);
		_notificationHandlers = new Dictionary<Type, INotificationHandlerWrapper>();
		_serviceProvider = null!;
		_notificationConfiguration = new NotificationPublisherConfiguration();
		_channel = Channel.CreateUnbounded<IRequestEnvelope>(new UnboundedChannelOptions
		{
			SingleReader = true,
			SingleWriter = false
		});
		_pump = Task.Run(ProcessAsync);
	}

	internal ChannelMediator(
		IReadOnlyDictionary<Type, IRequestHandlerWrapper> handlers,
		IReadOnlyDictionary<Type, INotificationHandlerWrapper> notificationHandlers,
		IServiceProvider serviceProvider,
		NotificationPublisherConfiguration notificationConfiguration)
	{
		_handlers = handlers;
		_notificationHandlers = notificationHandlers;
		_serviceProvider = serviceProvider;
		_notificationConfiguration = notificationConfiguration;
		_channel = Channel.CreateUnbounded<IRequestEnvelope>(new UnboundedChannelOptions
		{
			SingleReader = true,
			SingleWriter = false
		});
		_pump = Task.Run(ProcessAsync);
	}

	public async ValueTask<TResponse> InvokeAsync<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
	{
		if (request is null)
		{
			throw new ArgumentNullException(nameof(request));
		}

		var completionSource = new TaskCompletionSource<TResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
		var envelope = new RequestEnvelope<TResponse>(request, completionSource, cancellationToken);

		await _channel.Writer.WriteAsync(envelope, cancellationToken).ConfigureAwait(false);
		return await completionSource.Task.ConfigureAwait(false);
	}

	public async ValueTask PublishAsync<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
		where TNotification : INotification
	{
		if (notification is null)
		{
			throw new ArgumentNullException(nameof(notification));
		}

		var notificationType = typeof(TNotification);
		if (!_notificationHandlers.TryGetValue(notificationType, out var wrapper))
		{
			return;
		}

		if (_notificationConfiguration.Strategy == NotificationPublishStrategy.Parallel)
		{
			await PublishParallelAsync(notification, cancellationToken).ConfigureAwait(false);
		}
		else
		{
			await PublishSequentialAsync(notification, wrapper, cancellationToken).ConfigureAwait(false);
		}
	}

	private async ValueTask PublishSequentialAsync<TNotification>(
		TNotification notification,
		INotificationHandlerWrapper wrapper,
		CancellationToken cancellationToken)
		where TNotification : INotification
	{
		await wrapper.HandleAsync(notification, _serviceProvider, cancellationToken).ConfigureAwait(false);
	}

	private async ValueTask PublishParallelAsync<TNotification>(
		TNotification notification,
		CancellationToken cancellationToken)
		where TNotification : INotification
	{
		using var scope = _serviceProvider.CreateScope();
		var handlers = scope.ServiceProvider.GetServices<INotificationHandler<TNotification>>();

		var tasks = handlers.Select(handler =>
			handler.HandleAsync(notification, cancellationToken).AsTask());

		await Task.WhenAll(tasks).ConfigureAwait(false);
	}

	private async Task ProcessAsync()
	{
		try
		{
			await foreach (var envelope in _channel.Reader.ReadAllAsync(_cts.Token).ConfigureAwait(false))
			{
				await envelope.DispatchAsync(_handlers, _cts.Token).ConfigureAwait(false);
			}
		}
		catch (OperationCanceledException)
		{
		}
	}

	/// <summary>
	/// Sends a request to a single handler and returns the response.
	/// MediatR-compatible method that internally calls InvokeAsync.
	/// </summary>
	public async Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
	{
		return await InvokeAsync(request, cancellationToken).ConfigureAwait(false);
	}

	/// <summary>
	/// Publishes a notification to multiple handlers.
	/// MediatR-compatible method that internally calls PublishAsync.
	/// </summary>
	public async Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
		where TNotification : INotification
	{
		await PublishAsync(notification, cancellationToken).ConfigureAwait(false);
	}

	public async ValueTask DisposeAsync()
	{
		_channel.Writer.TryComplete();
		await _cts.CancelAsync();

		try
		{
			await _pump.ConfigureAwait(false);
		}
		catch (OperationCanceledException)
		{
		}

		_cts.Dispose();
	}
}
