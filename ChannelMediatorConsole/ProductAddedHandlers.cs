using ChannelMediator;

namespace ChannelMediatorConsole;

public sealed class LogProductAddedHandler : INotificationHandler<ProductAddedNotification>
{
	public ValueTask HandleAsync(ProductAddedNotification notification, CancellationToken cancellationToken)
	{
		Console.WriteLine($"[LOG] Product added: {notification.ProductCode} (qty: {notification.Quantity}) - Total: {notification.Total:C}");
		return ValueTask.CompletedTask;
	}
}

public sealed class SendEmailHandler : INotificationHandler<ProductAddedNotification>
{
	public async ValueTask HandleAsync(ProductAddedNotification notification, CancellationToken cancellationToken)
	{
		await Task.Delay(50, cancellationToken);
		Console.WriteLine($"[EMAIL] Sending confirmation email for product: {notification.ProductCode}");
	}
}

public sealed class UpdateInventoryHandler : INotificationHandler<ProductAddedNotification>
{
	public async ValueTask HandleAsync(ProductAddedNotification notification, CancellationToken cancellationToken)
	{
		await Task.Delay(30, cancellationToken);
		Console.WriteLine($"[INVENTORY] Updating stock for product: {notification.ProductCode}");
	}
}
