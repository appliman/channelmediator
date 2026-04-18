using ChannelMediator;
using ChannelMediator.MinimalApiGenerator.Abstraction;
using ChannelMediatorApiContractsSample.Models;
using Microsoft.Extensions.DependencyInjection;

[assembly: ApiClient(typeof(GetProductRequest), HttpClientName = "ApiClient")]

var services = new ServiceCollection();

services.AddChannelMediator(null, typeof(Program).Assembly);

services.AddHttpClient("ApiClient").ConfigureHttpClient(cfg =>
{
	cfg.BaseAddress = new Uri("https://localhost:7031/api/");
});

var sp = services.BuildServiceProvider();

var mediator = sp.GetRequiredService<IMediator>();

var product = await mediator.Send(new GetProductRequest(1));

Console.WriteLine(product!.Name);

Console.ReadLine();


