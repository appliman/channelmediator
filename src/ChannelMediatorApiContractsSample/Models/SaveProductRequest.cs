namespace ChannelMediatorApiContractsSample.Models;

[EndpointApi(
	GroupName = "Catalog",
	EntityName = "products"
)]
public record SaveProductRequest(Product Product)
	: IRequest<Product>;
