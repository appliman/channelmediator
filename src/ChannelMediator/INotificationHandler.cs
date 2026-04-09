namespace ChannelMediator;

/// <summary>
/// Defines a handler for notifications published through the mediator.
/// </summary>
/// <typeparam name="TNotification">The type of notification handled by this instance.</typeparam>
public interface INotificationHandler<in TNotification> 
	where TNotification : INotification
{
   /// <summary>
	/// Handles the specified notification.
	/// </summary>
	/// <param name="notification">The notification to handle.</param>
	/// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
	/// <returns>A task that represents the asynchronous handling operation.</returns>
	Task Handle(TNotification notification, CancellationToken cancellationToken);
}
