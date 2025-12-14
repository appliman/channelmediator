using ChannelMediator;

namespace ChannelMediatorConsole;

public sealed class LogProductAddedHandler : INotificationHandler<ProductAddedNotification>
{
	public Task Handle(ProductAddedNotification notification, CancellationToken cancellationToken)
	{
		Console.WriteLine($"[LOG] Product added: {notification.ProductCode} (qty: {notification.Quantity}) - Total: {notification.Total:C}");
		return Task.CompletedTask;
	}
}

public sealed class SendEmailHandler : INotificationHandler<ProductAddedNotification>
{
	public async Task Handle(ProductAddedNotification notification, CancellationToken cancellationToken)
	{
		await Task.Delay(50, cancellationToken);
		Console.WriteLine($"[EMAIL] Sending confirmation email for product: {notification.ProductCode}");
	}
}

public sealed class UpdateInventoryHandler : INotificationHandler<ProductAddedNotification>
{
	public async Task Handle(ProductAddedNotification notification, CancellationToken cancellationToken)
	{
		await Task.Delay(30, cancellationToken);
		Console.WriteLine($"[INVENTORY] Updating stock for product: {notification.ProductCode}");
	}
}
