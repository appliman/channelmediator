namespace ChannelMediator;

internal sealed class Mediator : IMediator, IAsyncDisposable, IDisposable
{
	private readonly IReadOnlyDictionary<Type, IRequestHandlerWrapper> _handlers;
	private readonly IReadOnlyDictionary<Type, INotificationHandlerWrapper> _notificationHandlers;
	private readonly IServiceProvider _serviceProvider;
	private readonly NotificationPublisherConfiguration _notificationConfiguration;
	private readonly Channel<IRequestEnvelope> _channel;
	private readonly CancellationTokenSource _cts = new();
	private readonly Task _pump;

	// Pre-computed reflection data for Send(object) and Publish(object) optimization
	private readonly IReadOnlyDictionary<Type, RequestTypeInfo> _requestTypeCache;
	private readonly IReadOnlyDictionary<Type, NotificationTypeInfo> _notificationTypeCache;

	private sealed record RequestTypeInfo(Type ResponseType, Func<Mediator, object, CancellationToken, Task<object?>> InvokeFunc);
	private sealed record NotificationTypeInfo(Func<Mediator, object, CancellationToken, Task> PublishFunc);

	internal Mediator(
		IReadOnlyDictionary<Type, IRequestHandlerWrapper> handlers,
		IReadOnlyDictionary<Type, INotificationHandlerWrapper>? notificationHandlers = null,
		IServiceProvider? serviceProvider = null,
		NotificationPublisherConfiguration? notificationConfiguration = null)
	{
		_handlers = handlers ?? throw new ArgumentNullException(nameof(handlers));
		_notificationHandlers = notificationHandlers ?? new Dictionary<Type, INotificationHandlerWrapper>();
		_serviceProvider = serviceProvider!;
		_notificationConfiguration = notificationConfiguration ?? new NotificationPublisherConfiguration();
		_channel = Channel.CreateUnbounded<IRequestEnvelope>(new UnboundedChannelOptions
		{
			SingleReader = true,
			SingleWriter = false
		});

		// Pre-compute request type information
		_requestTypeCache = BuildRequestTypeCache(handlers.Keys);
		_notificationTypeCache = BuildNotificationTypeCache(_notificationHandlers.Keys);

		_pump = Task.Run(ProcessAsync);
	}

	// Constructor for tests - converts IEnumerable to Dictionary
	public Mediator(IEnumerable<IRequestHandlerWrapper> handlers)
		: this(handlers.ToDictionary(h => h.RequestType))
	{
	}

	private static IReadOnlyDictionary<Type, RequestTypeInfo> BuildRequestTypeCache(IEnumerable<Type> requestTypes)
	{
		var cache = new Dictionary<Type, RequestTypeInfo>();

		foreach (var requestType in requestTypes)
		{
			var requestInterface = requestType.GetInterfaces()
				.FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IRequest<>));

			if (requestInterface is null)
			{
				// Command without response (IRequest only) - response type is Unit
				cache[requestType] = new RequestTypeInfo(typeof(Unit), CreateCommandInvoker(requestType));
			}
			else
			{
				var responseType = requestInterface.GetGenericArguments()[0];
				cache[requestType] = new RequestTypeInfo(responseType, CreateRequestInvoker(requestType, responseType));
			}
		}

		return cache;
	}

	private static Func<Mediator, object, CancellationToken, Task<object?>> CreateRequestInvoker(Type requestType, Type responseType)
	{
		// Create a compiled delegate for Send<TResponse> to avoid reflection on each call
		var sendMethod = typeof(Mediator)
			.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
			.First(m =>
				m.Name == nameof(Send) &&
				m.IsGenericMethodDefinition &&
				m.GetGenericArguments().Length == 1 &&
				m.GetParameters().Length == 2 &&
				m.GetParameters()[0].ParameterType.IsGenericType &&
				m.GetParameters()[0].ParameterType.GetGenericTypeDefinition() == typeof(IRequest<>));

		var genericSendMethod = sendMethod.MakeGenericMethod(responseType);

		return async (mediator, request, ct) =>
		{
			var task = (Task)genericSendMethod.Invoke(mediator, [request, ct])!;
			await task.ConfigureAwait(false);

			var resultProperty = task.GetType().GetProperty(nameof(Task<object>.Result));
			var result = resultProperty!.GetValue(task);

			return result is Unit ? null : result;
		};
	}

	private static Func<Mediator, object, CancellationToken, Task<object?>> CreateCommandInvoker(Type requestType)
	{
		return async (mediator, request, ct) =>
		{
			await mediator.Send((IRequest)request, ct).ConfigureAwait(false);
			return null;
		};
	}

	private static IReadOnlyDictionary<Type, NotificationTypeInfo> BuildNotificationTypeCache(IEnumerable<Type> notificationTypes)
	{
		var cache = new Dictionary<Type, NotificationTypeInfo>();

		foreach (var notificationType in notificationTypes)
		{
			cache[notificationType] = new NotificationTypeInfo(CreateNotificationPublisher(notificationType));
		}

		return cache;
	}

	private static Func<Mediator, object, CancellationToken, Task> CreateNotificationPublisher(Type notificationType)
	{
		var publishMethod = typeof(Mediator)
			.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
			.First(m =>
				m.Name == nameof(Publish) &&
				m.IsGenericMethodDefinition &&
				m.GetGenericArguments().Length == 1);

		var genericPublishMethod = publishMethod.MakeGenericMethod(notificationType);

		return (mediator, notification, ct) =>
		{
			return (Task)genericPublishMethod.Invoke(mediator, [notification, ct])!;
		};
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

		await _channel.Writer.WriteAsync(envelope, cancellationToken).ConfigureAwait(false);

		return await completionSource.Task.ConfigureAwait(false);
	}

	/// <inheritdoc />
	public async Task Send(IRequest request, CancellationToken cancellationToken = default)
	{
		if (request is null)
		{
			throw new ArgumentNullException(nameof(request));
		}

		await Send<Unit>(request, cancellationToken).ConfigureAwait(false);
	}

	/// <inheritdoc />
	public async Task<object?> Send(object request, CancellationToken cancellationToken = default)
	{
		if (request is null)
		{
			throw new ArgumentNullException(nameof(request));
		}

		var requestType = request.GetType();

		// Fast path: use pre-computed cache
		if (_requestTypeCache.TryGetValue(requestType, out var typeInfo))
		{
			return await typeInfo.InvokeFunc(this, request, cancellationToken).ConfigureAwait(false);
		}

		// Slow path: fallback for types not in cache (shouldn't happen in normal usage)
		return await SendObjectSlowPath(request, requestType, cancellationToken).ConfigureAwait(false);
	}

	private async Task<object?> SendObjectSlowPath(object request, Type requestType, CancellationToken cancellationToken)
	{
		var requestInterfaces = requestType.GetInterfaces()
			.Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IRequest<>))
			.ToList();

		if (requestInterfaces.Count == 0)
		{
			if (request is IRequest commandRequest)
			{
				await Send(commandRequest, cancellationToken).ConfigureAwait(false);
				return null;
			}

			throw new ArgumentException($"Request type {requestType.Name} does not implement IRequest<TResponse> or IRequest", nameof(request));
		}

		var requestInterface = requestInterfaces[0];
		var responseType = requestInterface.GetGenericArguments()[0];

		var sendMethod = typeof(Mediator)
			.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
			.FirstOrDefault(m =>
				m.Name == nameof(Send) &&
				m.IsGenericMethodDefinition &&
				m.GetGenericArguments().Length == 1 &&
				m.GetParameters().Length == 2 &&
				m.GetParameters()[0].ParameterType.IsGenericType &&
				m.GetParameters()[0].ParameterType.GetGenericTypeDefinition() == typeof(IRequest<>));

		if (sendMethod is null)
		{
			throw new InvalidOperationException($"Could not find Send method for {requestType.Name}");
		}

		var genericSendMethod = sendMethod.MakeGenericMethod(responseType);
		var task = (Task)genericSendMethod.Invoke(this, [request, cancellationToken])!;
		await task.ConfigureAwait(false);

		var resultProperty = task.GetType().GetProperty(nameof(Task<object>.Result));
		var result = resultProperty!.GetValue(task);

		return result is Unit ? null : result;
	}

	/// <inheritdoc />
	public async Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
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

	/// <inheritdoc />
	public async Task Publish(object notification, CancellationToken cancellationToken = default)
	{
		if (notification is null)
		{
			throw new ArgumentNullException(nameof(notification));
		}

		var notificationType = notification.GetType();

		// Fast path: use pre-computed cache
		if (_notificationTypeCache.TryGetValue(notificationType, out var typeInfo))
		{
			await typeInfo.PublishFunc(this, notification, cancellationToken).ConfigureAwait(false);
			return;
		}

		// Slow path: fallback for types not in cache
		await PublishObjectSlowPath(notification, notificationType, cancellationToken).ConfigureAwait(false);
	}

	private async Task PublishObjectSlowPath(object notification, Type notificationType, CancellationToken cancellationToken)
	{
		var notificationInterface = notificationType.GetInterfaces()
			.FirstOrDefault(i => i == typeof(INotification));

		if (notificationInterface is null)
		{
			throw new ArgumentException($"Notification type {notificationType.Name} does not implement INotification", nameof(notification));
		}

		var publishMethod = typeof(Mediator)
			.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
			.FirstOrDefault(m =>
				m.Name == nameof(Publish) &&
				m.IsGenericMethodDefinition &&
				m.GetGenericArguments().Length == 1);

		if (publishMethod is null)
		{
			throw new InvalidOperationException($"Could not find Publish method for {notificationType.Name}");
		}

		var genericPublishMethod = publishMethod.MakeGenericMethod(notificationType);
		var task = (Task)genericPublishMethod.Invoke(this, [notification, cancellationToken])!;
		await task.ConfigureAwait(false);
	}

	private async Task PublishSequentialAsync<TNotification>(
		TNotification notification,
		INotificationHandlerWrapper wrapper,
		CancellationToken cancellationToken)
		where TNotification : INotification
	{
		await wrapper.HandleAsync(notification, _serviceProvider, cancellationToken).ConfigureAwait(false);
	}

	private async Task PublishParallelAsync<TNotification>(
		TNotification notification,
		CancellationToken cancellationToken)
		where TNotification : INotification
	{
		using var scope = _serviceProvider.CreateScope();
		var handlers = scope.ServiceProvider.GetServices<INotificationHandler<TNotification>>();

		var tasks = handlers.Select(handler =>
			handler.Handle(notification, cancellationToken));

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
			await _pump.ConfigureAwait(false);
		}
		catch (OperationCanceledException)
		{
            // Dead for science
        }

        _cts.Dispose();
	}
}

