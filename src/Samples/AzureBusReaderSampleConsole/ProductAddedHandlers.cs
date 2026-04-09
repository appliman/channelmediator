using ChannelMediator;

using ChannelMediatorSampleShared;

namespace AzureBusReaderSampleReaderConsole;

public sealed class UpdateInventoryHandler : INotificationHandler<ProductAddedNotification>
{
	public async Task Handle(ProductAddedNotification notification, CancellationToken cancellationToken)
	{
		await Task.Delay(30, cancellationToken);
		Console.WriteLine($"[INVENTORY] Updating stock for product: {notification.ProductCode}");
	}
}