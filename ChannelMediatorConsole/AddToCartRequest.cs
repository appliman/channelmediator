using ChannelMediator;

namespace ChannelMediatorConsole;

public sealed record AddToCartRequest(string ProductCode) : IRequest<CartItem>;
