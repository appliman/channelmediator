namespace ChannelMediator;

internal interface INotificationHandlerWrapper
{
	Type NotificationType { get; }
	ValueTask HandleAsync(object notification, IServiceProvider serviceProvider, CancellationToken cancellationToken);
}
