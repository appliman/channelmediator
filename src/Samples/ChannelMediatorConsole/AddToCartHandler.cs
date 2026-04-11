using ChannelMediator;

using ChannelMediatorSampleShared;

namespace ChannelMediatorSampleConsole;

public sealed class AddToCartHandler(IProductCache cache, IMediator mediator)
	: IRequestHandler<AddToCartRequest, CartItem>
{
	public async Task<CartItem> Handle(AddToCartRequest request, CancellationToken cancellationToken)
	{
		if (cache.TryGet(request.ProductCode, out var cachedItem))
		{
			Console.WriteLine($"Cache hit for product: {request.ProductCode}");
			return cachedItem!;
		}

		Console.WriteLine($"Cache miss for product: {request.ProductCode}");
		await Task.Delay(100, cancellationToken);

		var cartItem = new CartItem(request.ProductCode, 1, 19.90m);
		cache.Set(request.ProductCode, cartItem);

		await mediator.Publish(new ProductAddedNotification(request.ProductCode, 1, 19.90m), cancellationToken);

		return cartItem;
	}
}
