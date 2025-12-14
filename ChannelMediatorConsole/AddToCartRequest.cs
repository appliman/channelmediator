using ChannelMediator;

namespace ChannelMediatorSampleConsole;

public sealed record AddToCartRequest(string ProductCode) : IRequest<CartItem>;
