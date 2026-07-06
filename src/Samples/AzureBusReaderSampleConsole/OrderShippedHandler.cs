using ChannelMediator;

using ChannelMediatorSampleShared;

namespace AzureBusReaderSampleReaderConsole;

public sealed class OrderShippedHandler : INotificationHandler<OrderShippedNotification>
{
    public async Task Handle(OrderShippedNotification notification, CancellationToken cancellationToken)
    {
        await Task.Delay(20, cancellationToken);
        Console.WriteLine($"[ORDER] Order {notification.OrderId} shipped to {notification.Destination} at {notification.ShippedAt:u}");
    }
}
