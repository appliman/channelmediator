namespace ChannelMediatorApiContractsSample.Models;

[EndpointApi(
	GroupName = "MyGroup",
	Path = "myfirst",
	Tags = new[] { "Test", "Example" },
	Summary = "Test endpoint",
	Description = "This is a test endpoint for MyFirstRequest",
	AuthenticationSchemes = new[] { "ApiKeyScheme" }
)]
public record MyFirstRequest(int Value) : IRequest<MyFirstResult>;
