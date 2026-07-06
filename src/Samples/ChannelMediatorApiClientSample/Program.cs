using ChannelMediator;
using ChannelMediator.ApiGenerators.Abstraction;
using ChannelMediatorApiClientSample.Handlers;
using ChannelMediatorApiContractsSample.Models;
using Microsoft.Extensions.DependencyInjection;

[assembly: ApiClient(typeof(GetProductRequest), HttpClientName = "ApiClient")]

var services = new ServiceCollection();

services.AddChannelMediator(null, typeof(Program).Assembly);

services.AddHttpClient("ApiClient").ConfigureHttpClient(cfg =>
{
	cfg.BaseAddress = new Uri("http://localhost:5126/api/");
});

var sp = services.BuildServiceProvider();

var mediator = sp.GetRequiredService<IMediator>();

var product = await mediator.Send(new GetProductRequest(1));

// var x = new GetProductRequestHandler();

Console.WriteLine(product!.Name);

Console.ReadLine();


