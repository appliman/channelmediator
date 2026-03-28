using ChannelMediator;

using ChannelMediatorSampleShared;

namespace ChannelMediatorSampleConsole;

public sealed class AddToCartHandler : IRequestHandler<AddToCartRequest, CartItem>
{
	private readonly IProductCache _cache;

	public AddToCartHandler(IProductCache cache)
	{
		_cache = cache ?? throw new ArgumentNullException(nameof(cache));
	}

	public async ValueTask<CartItem> HandleAsync(AddToCartRequest request, CancellationToken cancellationToken)
	{
		if (_cache.TryGet(request.ProductCode, out var cachedItem))
		{
			Console.WriteLine($"Cache hit for product: {request.ProductCode}");
			return cachedItem!;
		}

		Console.WriteLine($"Cache miss for product: {request.ProductCode}");
		await Task.Delay(100, cancellationToken).ConfigureAwait(false);

		var cartItem = new CartItem(request.ProductCode, 1, 19.90m);
		_cache.Set(request.ProductCode, cartItem);

		return cartItem;
	}

	public async Task<CartItem> Handle(AddToCartRequest request, CancellationToken cancellationToken)
	{
		return await HandleAsync(request, cancellationToken).ConfigureAwait(false);
	}
}
