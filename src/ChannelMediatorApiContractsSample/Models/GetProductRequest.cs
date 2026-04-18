namespace ChannelMediatorApiContractsSample.Models;

[EndpointApi(
	GroupName = "Catalog",
	EntityName = "products",
	UseHttpStandardVerbs = true
)]
public record GetProductRequest(int Id) : IRequest<Product?>;
