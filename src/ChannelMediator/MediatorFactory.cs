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
	private readonly IServiceProvider _serviceProvider;
	private readonly ChannelMediatorConfiguration _notificationConfiguration;

	public MediatorFactory(
		FrozenDictionary<Type, IRequestHandlerWrapper> handlers,
		FrozenDictionary<Type, INotificationHandlerWrapper> notificationHandlers,
		IServiceProvider serviceProvider,
		ChannelMediatorConfiguration notificationConfiguration)
	{
		_handlers = handlers ?? throw new ArgumentNullException(nameof(handlers));
		_notificationHandlers = notificationHandlers ?? FrozenDictionary<Type, INotificationHandlerWrapper>.Empty;
		_serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
		_notificationConfiguration = notificationConfiguration ?? new ChannelMediatorConfiguration();
	}

	/// <inheritdoc />
	public IMediator CreateMediator()
	{
		return new Mediator(_handlers, _notificationHandlers, _serviceProvider, _notificationConfiguration);
	}
}
