using ChannelMediator;

namespace ChannelMediatorSampleShared;

public sealed record AddToCartRequest(string ProductCode) : IRequest<CartItem>;
