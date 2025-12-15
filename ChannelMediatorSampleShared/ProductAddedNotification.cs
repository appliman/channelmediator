using ChannelMediator;

namespace ChannelMediatorSampleShared;

public record ProductAddedNotification(string ProductCode, int Quantity, decimal Total) 
    : INotification;
