using System.Reflection;

using ChannelMediator;

using ChannelMediatorSampleConsole;

using ChannelMediatorSampleShared;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var host = Host.CreateDefaultBuilder(args)
	.ConfigureServices((context, services) =>
	{
		services.AddSingleton<IProductCache, ProductCache>();

		var connectionString = context.Configuration.GetConnectionString("AzureBusConnectionString");

		services.AddChannelMediator(config =>
		{
			config.Strategy = NotificationPublishStrategy.Parallel;

		}, Assembly.GetExecutingAssembly());

		// Register GLOBAL pipeline behaviors - these apply to ALL requests automatically
		services.AddOpenPipelineBehavior(typeof(CorrelationBehavior<,>));
		services.AddOpenPipelineBehavior(typeof(PerformanceMonitoringBehavior<,>));

		// Register SPECIFIC pipeline behaviors - these only apply to specific request types
		services.AddPipelineBehavior<AddToCartRequest, CartItem, ValidationBehavior<AddToCartRequest, CartItem>>();
		services.AddPipelineBehavior<AddToCartRequest, CartItem, LoggingBehavior<AddToCartRequest, CartItem>>();
	})
	.Build();

using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
var cancellationToken = cts.Token;

await host.StartAsync(cancellationToken);

var serviceProvider = host.Services;
var mediator = serviceProvider.GetRequiredService<IMediator>();

// Test Request (with response) - using Send
Console.WriteLine("=== Testing Request with Send ===");
var cartItem = await mediator.Send(new AddToCartRequest("test"), cancellationToken);
Console.WriteLine($"Added {cartItem.ProductCode} (qty: {cartItem.Quantity}) totaling {cartItem.Total:C}");

Console.WriteLine();

// Test Request (with response) - using Send (MediatR-compatible API)
Console.WriteLine("=== Testing Request with Send (MediatR compatible) ===");
var cartItem2 = await mediator.Send(new AddToCartRequest("test-mediatr"), cancellationToken);
Console.WriteLine($"Added {cartItem2.ProductCode} (qty: {cartItem2.Quantity}) totaling {cartItem2.Total:C}");

Console.WriteLine();

// Test Command (no response) - using Send
Console.WriteLine("=== Testing Command without response - Send ===");
var logCommand = new LogOrderCommand("ORD-12345", 299.99m);
await mediator.Send(logCommand, cancellationToken);

Console.WriteLine();

// Test Command (no response) - using Send (MediatR-compatible)
Console.WriteLine("=== Testing Command without response - Send (MediatR compatible) ===");
var emailCommand = new SendEmailCommand(
	"customer@example.com",
	"Order Confirmation",
	"Your order has been confirmed!");
await mediator.Send(emailCommand, cancellationToken);

Console.WriteLine();

// Test Orchestration - Handler with IMediator injection calling multiple requests
Console.WriteLine("=== Testing Orchestration - Handler calling multiple requests ===");
var orderRequest = new ProcessOrderRequest(
	"ORD-98765",
	"laptop-pro",
	"customer@example.com");
var orderResult = await mediator.Send(orderRequest, cancellationToken);

Console.WriteLine($"Order Result:");
Console.WriteLine($"  Order ID: {orderResult.OrderId}");
Console.WriteLine($"  Product: {orderResult.Item.ProductCode}");
Console.WriteLine($"  Total: {orderResult.Item.Total:C}");
Console.WriteLine($"  Email Sent: {orderResult.EmailSent}");
Console.WriteLine($"  Logged: {orderResult.Logged}");

Console.WriteLine();
Console.WriteLine("All tests completed successfully!");
Console.WriteLine("Stopping host and disposing resources...");

await host.StopAsync(cancellationToken);
host.Dispose();

Console.WriteLine("Done!");
Environment.Exit(0);