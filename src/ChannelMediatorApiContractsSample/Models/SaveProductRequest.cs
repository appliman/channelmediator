namespace ChannelMediatorApiContractsSample.Models;

[EndpointApi(
	GroupName = "Catalog",
	Path = "products"
)]
public record SaveProductRequest(Product Product)
	: IRequest<Product>;
