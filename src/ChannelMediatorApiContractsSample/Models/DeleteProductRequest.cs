namespace ChannelMediatorApiContractsSample.Models;

[EndpointApi(
	GroupName = "Catalog",
	Path = "products",
	UseHttpStandardVerbs = true,
	Tags = new[] { "Catalog" },
	Summary = "Delete a product",
	Description = "Delete a product by ID",
	Protocol = EndpointProtocol.Both
)]
public record DeleteProductRequest(int Id)
	: IRequest<bool>;
