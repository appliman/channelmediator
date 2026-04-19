namespace ChannelMediatorApiContractsSample.Models;

[EndpointApi(
	GroupName = "Catalog",
	EntityName = "allproducts",
	UseHttpStandardVerbs = true
)]
public record GetAllProducts : IRequest<List<Product>>;
