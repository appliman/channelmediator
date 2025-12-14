using ChannelMediator;

namespace ChannelMediatorSampleNotificationWriterConsole;

public record ProductAddedNotification2(string ProductCode, int Quantity, decimal Total) 
    : INotification;
