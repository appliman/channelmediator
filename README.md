# 🚀 ChannelMediator

[![NuGet ChannelMediator](https://img.shields.io/nuget/v/ChannelMediator?label=ChannelMediator&logo=nuget)](https://www.nuget.org/packages/ChannelMediator/)
[![NuGet ChannelMediator.Contracts](https://img.shields.io/nuget/v/ChannelMediator.Contracts?label=ChannelMediator.Contracts&logo=nuget)](https://www.nuget.org/packages/ChannelMediator.Contracts/)
[![NuGet ChannelMediator.AzureBus](https://img.shields.io/nuget/v/ChannelMediator.AzureBus?label=ChannelMediator.AzureBus&logo=nuget)](https://www.nuget.org/packages/ChannelMediator.AzureBus/)
[![NuGet ChannelMediator.InMemory](https://img.shields.io/nuget/v/ChannelMediator.InMemory?label=ChannelMediator.InMemory&logo=nuget)](https://www.nuget.org/packages/ChannelMediator.InMemory/)
[![NuGet ChannelMediator.RabbitMQ](https://img.shields.io/nuget/v/ChannelMediator.RabbitMQ?label=ChannelMediator.RabbitMQ&logo=nuget)](https://www.nuget.org/packages/ChannelMediator.RabbitMQ/)
[![NuGet ChannelMediator.ApiGenerators.Abstraction](https://img.shields.io/nuget/v/ChannelMediator.ApiGenerators.Abstraction?label=ChannelMediator.ApiGenerators.Abstraction&logo=nuget)](https://www.nuget.org/packages/ChannelMediator.ApiGenerators.Abstraction/)
[![NuGet ChannelMediator.MinimalApiGenerator](https://img.shields.io/nuget/v/ChannelMediator.MinimalApiGenerator?label=ChannelMediator.MinimalApiGenerator&logo=nuget)](https://www.nuget.org/packages/ChannelMediator.MinimalApiGenerator/)
[![NuGet ChannelMediator.ApiClientGenerator](https://img.shields.io/nuget/v/ChannelMediator.ApiClientGenerator?label=ChannelMediator.ApiClientGenerator&logo=nuget)](https://www.nuget.org/packages/ChannelMediator.ApiClientGenerator/)
[![NuGet ChannelMediator.GrpcGenerator](https://img.shields.io/nuget/v/ChannelMediator.GrpcGenerator?label=ChannelMediator.GrpcGenerator&logo=nuget)](https://www.nuget.org/packages/ChannelMediator.GrpcGenerator/)
[![NuGet ChannelMediator.GrpcClientGenerator](https://img.shields.io/nuget/v/ChannelMediator.GrpcClientGenerator?label=ChannelMediator.GrpcClientGenerator&logo=nuget)](https://www.nuget.org/packages/ChannelMediator.GrpcClientGenerator/)
[![Build](https://github.com/appliman/channelmediator/actions/workflows/ci-publish.yml/badge.svg)](https://github.com/appliman/channelmediator/actions)
[![.NET](https://img.shields.io/badge/.NET-8.0%20%7C%209.0%20%7C%2010.0-purple?logo=dotnet)](https://dotnet.microsoft.com/)
[![C#](https://img.shields.io/badge/C%23-14-239120?logo=csharp)](https://learn.microsoft.com/dotnet/csharp/)

A modern, high-performance mediator for .NET, built on `System.Threading.Channels`, with **full MediatR compatibility**.

Compatible with **.NET 8**, **.NET 9**, and **.NET 10**.

## ✨ Features

- ✅ **MediatR Compatible** - Familiar API (`Send` / `Publish` / `CreateStream`)
- ✅ **Channel-Based** - Asynchronous processing with natural backpressure
- ✅ **Pipeline Behaviors** - Global AND specific
- ✅ **Streaming** - `IAsyncEnumerable<T>` with `IStreamRequest<T>` and stream pipeline behaviors
- ✅ **Parallel Notifications** - Sequential or parallel broadcasting
- ✅ **High Performance** - Channel-based with modern optimizations
- ✅ **In-Memory Pub/Sub** - Fire-and-forget `Notify` and `EnqueueRequest` without an external broker
- ✅ **Azure Service Bus** - Distributed messaging with queues and topics
- ✅ **RabbitMQ** - Self-hosted distributed messaging with exchanges and queues
- ✅ **Minimal API Generator** - Source-generated endpoint mapping from request attributes
- ✅ **API Client Generator** - Source-generated `HttpClient` handlers for consuming generated APIs
- ✅ **gRPC Generator** - Source-generated code-first gRPC services via `protobuf-net.Grpc`
- ✅ **gRPC Client Generator** - Source-generated `IRequestHandler` calling gRPC services
- ✅ **.NET 8 / 9 / 10** - Multi-targeted packages for current .NET versions

## 📦 Installation

```bash
# Package (coming soon)
dotnet add package ChannelMediator

# Or local reference
<ProjectReference Include="..\ChannelMediator\ChannelMediator.csproj" />
```

## 🎯 Quick Start

### Configuration

```csharp
using ChannelMediator;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

var services = new ServiceCollection();

// Register the mediator
services.AddChannelMediator(
    config => config.Strategy = NotificationPublishStrategy.Parallel,
    Assembly.GetExecutingAssembly());

var provider = services.BuildServiceProvider();
var mediator = provider.GetRequiredService<IMediator>();
```

> [!IMPORTANT]
> If multiple handlers are found for the same request, only the first registered handler is kept and the others are ignored.
> When you pass multiple assemblies to `AddChannelMediator`, the declaration order determines which handler is selected by default.
> Put the assembly containing your preferred default handler first.

```csharp
services.AddChannelMediator(
    assemblies: [
        typeof(MyPreferredHandler).Assembly,
        typeof(MyFallbackHandler).Assembly
    ]);
```

### Define a Request

```csharp
// Request
public record AddToCartRequest(string ProductCode) : IRequest<CartItem>;

// Response
public record CartItem(string ProductCode, int Quantity, decimal Total);

// Handler
public class AddToCartHandler : IRequestHandler<AddToCartRequest, CartItem>
{
    public async Task<CartItem> Handle(
        AddToCartRequest request, 
        CancellationToken cancellationToken)
    {
        // Business logic
        return new CartItem(request.ProductCode, 1, 19.99m);
    }
}
```

### Usage

```csharp
var cart = await mediator.Send(new AddToCartRequest("ABC123"));
```

### Notifications

```csharp
// Notification
public record ProductAddedNotification(string ProductCode, int Quantity) : INotification;

// Handlers (multiple handlers supported)
public class LogHandler : INotificationHandler<ProductAddedNotification>
{
    public Task Handle(ProductAddedNotification notification, CancellationToken ct)
    {
        Console.WriteLine($"LOG: {notification.ProductCode}");
        return Task.CompletedTask;
    }
}

public class EmailHandler : INotificationHandler<ProductAddedNotification>
{
    public async Task Handle(ProductAddedNotification notification, CancellationToken ct)
    {
        await SendEmailAsync(notification.ProductCode);
    }
}

// Publish notification to all handlers
await mediator.Publish(new ProductAddedNotification("ABC123", 1));
```

## 🎭 Pipeline Behaviors

### Global Behaviors (for ALL requests)

```csharp
public class LoggingBehavior<TRequest, TResponse> 
    : IPipelineBehavior<TRequest, TResponse>, IPipelineBehavior
    where TRequest : IRequest<TResponse>
{
    public async ValueTask<TResponse> HandleAsync(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        Console.WriteLine($"Before: {typeof(TRequest).Name}");
        var response = await next();
        Console.WriteLine($"After: {typeof(TRequest).Name}");
        return response;
    }
}

// Registration
services.AddOpenPipelineBehavior(typeof(LoggingBehavior<,>));
services.AddOpenPipelineBehavior(typeof(PerformanceMonitoringBehavior<,>));
```

### Specific Behaviors (for a specific request type)

```csharp
public class ValidationBehavior<TRequest, TResponse> 
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public async ValueTask<TResponse> HandleAsync(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        // Specific validation
        if (request is AddToCartRequest { ProductCode: null or "" })
            throw new ArgumentException("ProductCode required");
            
        return await next();
    }
}

// Registration
services.AddPipelineBehavior<AddToCartRequest, CartItem, ValidationBehavior<AddToCartRequest, CartItem>>();
```

## 🌊 Streaming

Stream large or incremental datasets with `IStreamRequest<T>` and `IAsyncEnumerable<T>`. Streaming requests **bypass the channel pump** and are dispatched directly, so they don't block other requests.

### Define a Stream Request

```csharp
public record GetOrderLinesQuery(int OrderId) : IStreamRequest<OrderLineDto>;

public class GetOrderLinesHandler : IStreamRequestHandler<GetOrderLinesQuery, OrderLineDto>
{
    private readonly IOrderRepository _repo;

    public GetOrderLinesHandler(IOrderRepository repo) => _repo = repo;

    public async IAsyncEnumerable<OrderLineDto> Handle(
        GetOrderLinesQuery request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var line in _repo.StreamOrderLinesAsync(request.OrderId, cancellationToken))
        {
            yield return line;
        }
    }
}
```

### Usage

```csharp
await foreach (var line in mediator.CreateStream(new GetOrderLinesQuery(42), cancellationToken))
{
    Console.WriteLine(line);
}
```

### Stream Pipeline Behaviors

```csharp
public class StreamLoggingBehavior<TRequest, TResponse>
    : IStreamPipelineBehavior<TRequest, TResponse>
    where TRequest : IStreamRequest<TResponse>
{
    public async IAsyncEnumerable<TResponse> Handle(
        TRequest request,
        StreamHandlerDelegate<TResponse> next,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        Console.WriteLine($"Stream starting: {typeof(TRequest).Name}");
        await foreach (var item in next().WithCancellation(cancellationToken))
        {
            yield return item;
        }
        Console.WriteLine($"Stream completed: {typeof(TRequest).Name}");
    }
}

// Registration
services.AddScoped<IStreamPipelineBehavior<GetOrderLinesQuery, OrderLineDto>,
    StreamLoggingBehavior<GetOrderLinesQuery, OrderLineDto>>();
```

## 📊 Available APIs

| Method | Return Type | Description |
|--------|-------------|-------------|
| `Send<TResponse>(IRequest<TResponse>, CancellationToken)` | `Task<TResponse>` | Sends a request to a single handler and returns the response |
| `Send(IRequest, CancellationToken)` | `Task` | Sends a request without response (command) |
| `Publish<TNotification>(TNotification, CancellationToken)` | `Task` | Publishes a notification to multiple handlers |
| `CreateStream<TResponse>(IStreamRequest<TResponse>, CancellationToken)` | `IAsyncEnumerable<TResponse>` | Creates an async stream from a streaming handler |

## 📚 Documentation

- [🧠 In-Memory Integration](#-in-memory-integration)
- [🚌 Azure Service Bus Integration](./AZURE_SERVICE_BUS.md)
- [🐇 RabbitMQ Integration](./RABBITMQ.md)
- [⚡ Minimal API & Client Generators](./GENERATORS.md)
- [🔄 MediatR Compatibility](./MEDIATR_COMPATIBILITY.md)
- [🎭 Pipeline Behaviors](./PIPELINE_BEHAVIORS.md)
- [📊 Sequence Diagram](./SEQUENCE_DIAGRAM.md)

## 🔌 API & gRPC Generators

ChannelMediator provides **Roslyn source generators** that eliminate boilerplate for ASP.NET Core Minimal APIs, their HTTP clients, and code-first gRPC services.

> [!NOTE]
> The shared attributes package was renamed from `ChannelMediator.MinimalApiGenerator.Abstraction` to **`ChannelMediator.ApiGenerators.Abstraction`** to reflect its broader scope (HTTP + gRPC).

### HTTP — Auto-generate Minimal API endpoints

Decorate request contracts with `[EndpointApi]` (default `Protocol = EndpointProtocol.Http`) and a mapper class with `[MapApiExtension]`.

```csharp
// 1. Shared contracts project — install ChannelMediator.ApiGenerators.Abstraction
[EndpointApi(GroupName = "Catalog", Path = "products", UseHttpStandardVerbs = true)]
public record GetProductRequest(int Id) : IRequest<Product?>;

[EndpointApi(GroupName = "Catalog", Path = "products")]
public record SaveProductRequest(Product Product) : IRequest<Product>;

// 2. Server project — install ChannelMediator.MinimalApiGenerator
[MapApiExtension]
public static partial class MyHttpMapper { }

// 3. Program.cs
app.MapMyHttpMapper(); // All HTTP endpoints registered!
```

### HTTP Client — Auto-generate HttpClient handlers

```csharp
// Client project — install ChannelMediator.ApiClientGenerator
[assembly: ApiClient(typeof(GetProductRequest), HttpClientName = "ApiClient")]

services.AddHttpClient("ApiClient")
    .ConfigureHttpClient(cfg => cfg.BaseAddress = new Uri("https://localhost:7031/api/"));

var product = await mediator.Send(new GetProductRequest(1));
```

### gRPC — Auto-generate code-first gRPC services

Set `Protocol = EndpointProtocol.Grpc` (or `Both`) on requests, then decorate a mapper with `[GrpcServiceExtension]`.

```csharp
// Shared contracts — set Grpc or Both on the protocol
[EndpointApi(GroupName = "Catalog", Path = "products", Protocol = EndpointProtocol.Both)]
public record GetProductRequest(int Id) : IRequest<Product?>;

// Server project — install ChannelMediator.GrpcGenerator
[GrpcServiceExtension]
public static partial class MyGrpcMapper { }

// Program.cs
app.MapMyGrpcMapperGrpcServices(); // Registers gRPC service
```

### gRPC Client — Auto-generate gRPC client handlers

```csharp
// Client project — install ChannelMediator.GrpcClientGenerator
[assembly: GrpcClient(typeof(GetProductRequest), GrpcClientName = "GrpcClient")]

services.AddGrpcClient<ICatalogService>("GrpcClient")
    .ConfigureChannel(o => o.Address = new Uri("https://localhost:7032"));

var product = await mediator.Send(new GetProductRequest(1));
```

See [⚡ Generators documentation](./GENERATORS.md) for the full reference.

👉 **[Full documentation →](./GENERATORS.md)**

## 🏗️ Architecture

```
Client
  ↓
IMediator (Send / Publish)
  ↓
Channel (async queue)
  ↓
RequestHandlerWrapper
  ↓
Pipeline Behaviors (chain)
  ├─ Global Behavior 1
  ├─ Global Behavior 2
  ├─ Specific Behavior 1
  └─ Request Handler (business logic)
```

## 🧠 In-Memory Integration

`ChannelMediator.InMemory` provides the same `Notify` and `EnqueueRequest` extension methods as the broker integrations, but dispatches everything inside the current process.

This is useful when you want the producer call to return immediately while the actual handler execution continues on a background thread, without requiring Azure Service Bus or RabbitMQ.

```csharp
using ChannelMediator.InMemory;

services.AddChannelMediator(config =>
{
    config.UseChannelMediatorInMemory();
}, Assembly.GetExecutingAssembly());

var mediator = provider.GetRequiredService<IMediator>();

// Scheduled on a background thread, returns immediately
await mediator.Notify(new ProductAddedNotification("SKU-001", 5));

// Scheduled on a background thread, returns immediately
await mediator.EnqueueRequest(new MyRequest("process-order-42"));
```

Both methods use fire-and-forget scheduling: the returned task completes once the in-memory work has been scheduled, not when the handler has completed.

## 🚌 Azure Service Bus Integration

In a microservice architecture, a single process cannot handle all requests. You need to **distribute workloads** across multiple consumer instances and **decouple services** through asynchronous messaging.

`ChannelMediator.AzureBus` extends the mediator with two extension methods that transparently route messages through **Azure Service Bus**:

- **`mediator.Notify(notification)`** → Publishes to a **Topic** (fan-out to all subscribers)
- **`mediator.EnqueueRequest(request)`** → Enqueues to a **Queue** (competing consumers, only one processes each message)

```csharp
var mediator = app.Services.GetRequiredService<IMediator>();

// Fan-out notification to all subscriber services
await mediator.Notify(new ProductAddedNotification("SKU-001", 5, 49.95m));

// Enqueue a request for competing consumer processing
await mediator.EnqueueRequest(new MyRequest("process-order-42"));
```

Supports **Live** mode (real Azure Service Bus) and **Mock** mode (in-process for local development). Queues, topics, and subscriptions are created automatically on first use.

👉 **[Full documentation →](./AZURE_SERVICE_BUS.md)**

## 🐇 RabbitMQ Integration

For self-hosted or on-premise scenarios, `ChannelMediator.RabbitMQ` provides the same distributed messaging patterns using **RabbitMQ**:

- **`mediator.NotifyRabbitMq(notification)`** → Publishes to a **Fanout Exchange** (fan-out to all bound queues)
- **`mediator.EnqueueRabbitMqRequest(request)`** → Enqueues to a **Queue** (competing consumers, only one processes each message)

```csharp
var mediator = app.Services.GetRequiredService<IMediator>();

// Fan-out notification to all subscriber services
await mediator.Notify(new ProductAddedNotification("SKU-001", 5, 49.95m));

// Enqueue a request for competing consumer processing
await mediator.EnqueueRequest(new MyRequest("process-order-42"));
```

Supports **Live** mode (real RabbitMQ broker) and **Mock** mode (in-process for local development). Exchanges, queues, and bindings are created automatically on first use.

👉 **[Full documentation →](./RABBITMQ.md)**

## 🎯 Use Cases

### Perfect for:
- ✅ High-load applications (backpressure)
- ✅ Microservices with CQRS patterns
- ✅ Migration from MediatR (drop-in replacement)
- ✅ REST / gRPC APIs with complex orchestration
- ✅ Event-driven architectures

### Examples:
- **E-commerce**: Orders, cart, checkout
- **CMS**: Publishing, workflow, notifications
- **IoT**: Telemetry, commands, events
- **Finance**: Transactions, audit, reporting

## ⚙️ Advanced Configuration

### Parallel Notifications

```csharp
services.AddChannelMediator(config => 
    config.Strategy = NotificationPublishStrategy.Parallel);

// All handlers execute in parallel with Task.WhenAll
await mediator.Publish(notification);
```

### Sequential Notifications

```csharp
services.AddChannelMediator(config => 
    config.Strategy = NotificationPublishStrategy.Sequential);

// Handlers execute one after another
await mediator.Publish(notification);
```

## 🧪 Tests

```csharp
[Fact]
public async Task Should_Handle_Request()
{
    // Arrange
    var services = new ServiceCollection();
    services.AddChannelMediator(Assembly.GetExecutingAssembly());
    var provider = services.BuildServiceProvider();
    var mediator = provider.GetRequiredService<IMediator>();

    // Act
    var result = await mediator.Send(new AddToCartRequest("TEST"));

    // Assert
    Assert.NotNull(result);
    Assert.Equal("TEST", result.ProductCode);
}
```

## 🔧 Compatibility

- **.NET 10** (can be back-ported to .NET 8)
- **C# 14** (can be adapted for C# 12)
- **Microsoft.Extensions.DependencyInjection 9.0+**

## 📝 License

MIT (to be defined)

## 👥 Contributing

Contributions are welcome! Open an issue or a PR.

## 🙏 Inspirations

- [MediatR](https://github.com/jbogard/MediatR) - The original and still excellent
- [System.Threading.Channels](https://docs.microsoft.com/dotnet/api/system.threading.channels) - The foundation of our implementation

## ⭐ Why ChannelMediator?

1. **Performance** - Channel-based asynchronous processing
2. **Flexibility** - MediatR-compatible API with powerful extensions
3. **Modern** - .NET 10, C# 14, modern patterns
4. **Powerful** - Global behaviors, parallel notifications
5. **Familiar** - MediatR compatible, easy migration
