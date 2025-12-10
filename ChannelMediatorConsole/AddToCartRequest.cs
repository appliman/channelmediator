using ChannelMediator.Contracts;

namespace ChannelMediatorConsole;

public sealed record AddToCartRequest(string ProductCode) : IRequest<CartItem>;
