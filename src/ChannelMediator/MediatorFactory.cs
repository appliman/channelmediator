namespace ChannelMediator;

/// <summary>
/// Factory that creates new mediator instances on demand.
/// Each created mediator has its own channel and pump, allowing nested calls
/// without deadlocking the main mediator's pump.
/// </summary>
internal sealed class MediatorFactory : IMediatorFactory
{
	private readonly FrozenDictionary<Type, IRequestHandlerWrapper> _handlers;
	private readonly FrozenDictionary<Type, INotificationHandlerWrapper> _notificationHandlers;
	private readonly FrozenDictionary<Type, IStreamRequestHandlerWrapper> _streamHandlers;
	private readonly IServiceProvider _serviceProvider;
	private readonly ChannelMediatorConfiguration _notificationConfiguration;
	private readonly RequestChainLimiter? _requestChainLimiter;

	public MediatorFactory(
		FrozenDictionary<Type, IRequestHandlerWrapper> handlers,
		FrozenDictionary<Type, INotificationHandlerWrapper> notificationHandlers,
		IServiceProvider serviceProvider,
		ChannelMediatorConfiguration notificationConfiguration,
		FrozenDictionary<Type, IStreamRequestHandlerWrapper>? streamHandlers = null)
	{
		_handlers = handlers ?? throw new ArgumentNullException(nameof(handlers));
		_notificationHandlers = notificationHandlers ?? FrozenDictionary<Type, INotificationHandlerWrapper>.Empty;
		_streamHandlers = streamHandlers ?? FrozenDictionary<Type, IStreamRequestHandlerWrapper>.Empty;
		_serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
		_notificationConfiguration = notificationConfiguration ?? new ChannelMediatorConfiguration();
		if (_notificationConfiguration.QueueCapacity is <= 0)
		{
			throw new ArgumentOutOfRangeException(nameof(notificationConfiguration), "QueueCapacity must be positive.");
		}
		_requestChainLimiter = _notificationConfiguration.MaxConcurrentRequestChains is int capacity
			? new RequestChainLimiter(capacity)
			: null;
	}

	/// <inheritdoc />
	public IMediator CreateMediator()
	{
		var result = new Mediator(_handlers, _notificationHandlers, _serviceProvider, _notificationConfiguration, _streamHandlers, _requestChainLimiter);
		return result;
	}
}
