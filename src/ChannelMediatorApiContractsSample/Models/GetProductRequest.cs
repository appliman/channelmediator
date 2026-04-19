namespace ChannelMediatorApiContractsSample.Models;

[EndpointApi(
	GroupName = "Catalog",
	Path = "products",
	UseHttpStandardVerbs = true
)]
public record GetProductRequest(int Id) : IRequest<Product?>;
