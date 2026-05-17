namespace ChannelMediator;

internal sealed class NotificationHandlerWrapper<TNotification> : INotificationHandlerWrapper
	where TNotification : INotification
{
	public Type NotificationType => typeof(TNotification);

	public async ValueTask HandleAsync(object notification, IServiceProvider serviceProvider, CancellationToken cancellationToken)
	{
		var typedNotification = (TNotification)notification;

		using var scope = serviceProvider.CreateScope();
		var handlers = scope.ServiceProvider.GetServices<INotificationHandler<TNotification>>();

		foreach (var handler in handlers)
		{
			await handler.Handle(typedNotification, cancellationToken);
		}
	}
}
