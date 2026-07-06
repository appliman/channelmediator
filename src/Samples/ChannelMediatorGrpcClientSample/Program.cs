using ChannelMediator;
using ChannelMediator.ApiGenerators.Abstraction;
using ChannelMediatorApiContractsSample.Models;
using ChannelMediatorGrpcClientSample;
using Microsoft.Extensions.DependencyInjection;
using ProtoBuf.Grpc.ClientFactory;

[assembly: GrpcClient(typeof(GetProductRequest), GrpcClientName = "GrpcClient")]

var services = new ServiceCollection();

services.AddChannelMediator(null, typeof(Program).Assembly);

var sp = services.BuildServiceProvider();

var mediator = sp.GetRequiredService<IMediator>();

var product = await mediator.Send(new GetProductRequest(1));

Console.WriteLine($"Product: {product?.Name} — Price: {product?.Price:C}");

Console.ReadLine();
