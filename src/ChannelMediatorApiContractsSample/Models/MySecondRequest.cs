namespace ChannelMediatorApiContractsSample.Models;

[EndpointApi(
	GroupName = "MyGroup",
	EntityName = "mysecond",
	Tags = new[] { "Test" },
	Summary = "Second test endpoint",
	Description = "This is a test endpoint for MySecondRequest",
	AuthenticationSchemes = ["MyApiKey"]
)]
public record MySecondRequest(string Name) : IRequest<MySecondResult>;
