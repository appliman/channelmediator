namespace ChannelMediatorApiContractsSample.Models;

[EndpointApi(
	GroupName = "Catalog",
	Path = "products",
	UseHttpStandardVerbs = true,
	Protocol = EndpointProtocol.Both
)]
public record GetProductRequest(int Id) : IRequest<Product?>;
