using ChannelMediator.Contracts;

namespace ChannelMediator;

public sealed record AddToCartRequest(string ProductCode) : IRequest<CartItem>;
