using ChannelMediator;

namespace ChannelMediatorSampleNotificationReaderConsole;

public record ProductAddedNotification(string ProductCode, int Quantity, decimal Total) 
    : INotification;
