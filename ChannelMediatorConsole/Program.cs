using ChannelMediator;
using ChannelMediatorConsole;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

var services = new ServiceCollection();

services.AddSingleton<IProductCache, ProductCache>();

// Configure notification strategy: Sequential or Parallel
services.AddChannelMediator(
    config => config.Strategy = NotificationPublishStrategy.Parallel,
    Assembly.GetExecutingAssembly());

// Register GLOBAL pipeline behaviors - these apply to ALL requests automatically
services.AddOpenPipelineBehavior(typeof(CorrelationBehavior<,>));
services.AddOpenPipelineBehavior(typeof(PerformanceMonitoringBehavior<,>));

// Register SPECIFIC pipeline behaviors - these only apply to specific request types
services.AddPipelineBehavior<AddToCartRequest, CartItem, ValidationBehavior<AddToCartRequest, CartItem>>();
services.AddPipelineBehavior<AddToCartRequest, CartItem, LoggingBehavior<AddToCartRequest, CartItem>>();

var serviceProvider = services.BuildServiceProvider();

var cancellationToken = CancellationToken.None;

var mediator = serviceProvider.GetRequiredService<IMediator>();

// Test Request (with response) - using InvokeAsync (ChannelMediator native API)
Console.WriteLine("=== Testing Request with InvokeAsync ===");
var cartItem = await mediator.InvokeAsync(new AddToCartRequest("test"), cancellationToken);
Console.WriteLine($"Added {cartItem.ProductCode} (qty: {cartItem.Quantity}) totaling {cartItem.Total:C}");

Console.WriteLine();

// Test Request (with response) - using Send (MediatR-compatible API)
Console.WriteLine("=== Testing Request with Send (MediatR compatible) ===");
var cartItem2 = await mediator.Send(new AddToCartRequest("test-mediatr"), cancellationToken);
Console.WriteLine($"Added {cartItem2.ProductCode} (qty: {cartItem2.Quantity}) totaling {cartItem2.Total:C}");

Console.WriteLine();

// Test Notification (no response, multiple handlers) - using PublishAsync
Console.WriteLine("=== Testing Notification with PublishAsync (Parallel) ===");
var notification = new ProductAddedNotification(cartItem.ProductCode, cartItem.Quantity, cartItem.Total);
await mediator.PublishAsync(notification, cancellationToken);

Console.WriteLine();

// Test Notification (no response, multiple handlers) - using Publish (MediatR-compatible)
Console.WriteLine("=== Testing Notification with Publish (MediatR compatible) ===");
var notification2 = new ProductAddedNotification(cartItem2.ProductCode, cartItem2.Quantity, cartItem2.Total);
await mediator.Publish(notification2, cancellationToken);

Console.WriteLine();
Console.WriteLine("Press any key to exit...");
Console.Read();

await (mediator as IAsyncDisposable)!.DisposeAsync();
await serviceProvider.DisposeAsync();
