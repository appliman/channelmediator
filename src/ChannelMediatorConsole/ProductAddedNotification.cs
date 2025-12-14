using ChannelMediator;

namespace ChannelMediatorSampleConsole;

public record ProductAddedNotification(string ProductCode, int Quantity, decimal Total) : INotification;
