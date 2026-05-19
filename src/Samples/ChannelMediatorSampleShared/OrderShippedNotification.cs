using ChannelMediator;

namespace ChannelMediatorSampleShared;

public record OrderShippedNotification(string OrderId, string Destination, DateTimeOffset ShippedAt) : INotification;
