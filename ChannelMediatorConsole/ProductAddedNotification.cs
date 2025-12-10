using ChannelMediator.Contracts;

namespace ChannelMediator;

public record ProductAddedNotification(string ProductCode, int Quantity, decimal Total) : INotification;
