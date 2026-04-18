namespace ChannelMediatorMinimalApiSample.Handlers;

public class GetProductHandler : IRequestHandler<GetProductRequest, Product?>
{
	public Task<Product?> Handle(GetProductRequest request, CancellationToken cancellationToken)
	{
		if (request.Id == 999)
		{
			return Task.FromResult<Product?>(null);
		}

		return Task.FromResult<Product?>(new Product
		{
			Id = request.Id,
			Name = $"Product {request.Id}",
			Price = 99.99
		});
	}
}
