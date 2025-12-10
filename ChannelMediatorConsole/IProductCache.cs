using System.Collections.Generic;

namespace ChannelMediator;

public interface IProductCache
{
    bool TryGet(string productCode, out CartItem? cartItem);
    void Set(string productCode, CartItem cartItem);
}

public class ProductCache : IProductCache
{
    private readonly Dictionary<string, CartItem> _cache = new();

    public bool TryGet(string productCode, out CartItem? cartItem)
    {
        return _cache.TryGetValue(productCode, out cartItem);
    }

    public void Set(string productCode, CartItem cartItem)
    {
        _cache[productCode] = cartItem;
    }
}
