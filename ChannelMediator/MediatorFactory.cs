namespace ChannelMediator;

/// <summary>
/// Factory that creates new mediator instances on demand.
/// Each created mediator has its own channel and pump, allowing nested calls
/// without deadlocking the main mediator's pump.
/// </summary>
internal sealed class MediatorFactory : IMediatorFactory
{
	private readonly IReadOnlyDictionary<Type, IRequestHandlerWrapper> _handlers;
	private readonly IReadOnlyDictionary<Type, INotificationHandlerWrapper> _notificationHandlers;
	private readonly IServiceProvider _serviceProvider;
	private readonly NotificationPublisherConfiguration _notificationConfiguration;

	public MediatorFactory(
		IReadOnlyDictionary<Type, IRequestHandlerWrapper> handlers,
		IReadOnlyDictionary<Type, INotificationHandlerWrapper> notificationHandlers,
		IServiceProvider serviceProvider,
		NotificationPublisherConfiguration notificationConfiguration)
	{
		_handlers = handlers ?? throw new ArgumentNullException(nameof(handlers));
		_notificationHandlers = notificationHandlers ?? new Dictionary<Type, INotificationHandlerWrapper>();
		_serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
		_notificationConfiguration = notificationConfiguration ?? new NotificationPublisherConfiguration();
	}

	/// <inheritdoc />
	public IMediator CreateMediator()
	{
		return new Mediator(_handlers, _notificationHandlers, _serviceProvider, _notificationConfiguration);
	}
}
