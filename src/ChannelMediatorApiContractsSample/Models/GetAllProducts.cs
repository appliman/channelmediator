namespace ChannelMediatorApiContractsSample.Models;

[EndpointApi(
	GroupName = "Catalog",
	Path = "allproducts",
	UseHttpStandardVerbs = true
)]
public record GetAllProducts : IRequest<List<Product>>;
