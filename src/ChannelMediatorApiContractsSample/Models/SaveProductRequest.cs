namespace ChannelMediatorApiContractsSample.Models;

[EndpointApi(
	GroupName = "Catalog",
	Path = "products",
	Protocol = EndpointProtocol.Both
)]
public record SaveProductRequest(Product Product)
	: IRequest<Product>;
