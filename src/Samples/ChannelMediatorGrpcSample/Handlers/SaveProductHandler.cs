namespace ChannelMediatorGrpcSample.Handlers;

public class SaveProductHandler : IRequestHandler<SaveProductRequest, Product>
{
    public Task<Product> Handle(SaveProductRequest request, CancellationToken cancellationToken)
    {
        var saved = new Product
        {
            Id = request.Product.Id == 0 ? Random.Shared.Next(1, 1000) : request.Product.Id,
            Name = request.Product.Name,
            Price = request.Product.Price,
            Type = request.Product.Type
        };
        return Task.FromResult(saved);
    }
}
