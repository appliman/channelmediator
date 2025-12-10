using ChannelMediator;

namespace ChannelMediatorConsole;

public record ProductAddedNotification(string ProductCode, int Quantity, decimal Total) : INotification;
