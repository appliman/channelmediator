namespace ChannelMediatorApiContractsSample.Models;

[EndpointApi(
	GroupName = "Catalog",
	EntityName = "products",
	UseHttpStandardVerbs = true,
	Tags = new[] { "Catalog" },
	Summary = "Delete a product",
	Description = "Delete a product by ID"
)]
public record DeleteProductRequest(int Id)
	: IRequest<bool>;
