# 🚀 ChannelMediator

A modern, high-performance mediator for .NET, built on `System.Threading.Channels`, with **full MediatR compatibility**.

## ✨ Features

- ✅ **MediatR Compatible** - Familiar API (`Send` / `Publish`)
- ✅ **Channel-Based** - Asynchronous processing with natural backpressure
- ✅ **Pipeline Behaviors** - Global AND specific
- ✅ **Parallel Notifications** - Sequential or parallel broadcasting
- ✅ **High Performance** - ValueTask, Channel, modern optimizations
- ✅ **.NET 10** - Modern code with C# 14

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

### Define a Request

```csharp
// Request
public record AddToCartRequest(string ProductCode) : IRequest<CartItem>;

// Response
public record CartItem(string ProductCode, int Quantity, decimal Total);

// Handler
public class AddToCartHandler : IRequestHandler<AddToCartRequest, CartItem>
{
    public async ValueTask<CartItem> HandleAsync(
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
// Native API (recommended)
var cart = await mediator.InvokeAsync(new AddToCartRequest("ABC123"));

// MediatR API (compatible)
var cart = await mediator.Send(new AddToCartRequest("ABC123"));
```

### Notifications

```csharp
// Notification
public record ProductAddedNotification(string ProductCode, int Quantity) : INotification;

// Handlers (multiple handlers supported)
public class LogHandler : INotificationHandler<ProductAddedNotification>
{
    public ValueTask HandleAsync(ProductAddedNotification notification, CancellationToken ct)
    {
        Console.WriteLine($"LOG: {notification.ProductCode}");
        return ValueTask.CompletedTask;
    }
}

public class EmailHandler : INotificationHandler<ProductAddedNotification>
{
    public async ValueTask HandleAsync(ProductAddedNotification notification, CancellationToken ct)
    {
        await SendEmailAsync(notification.ProductCode);
    }
}

// Usage - Native API
await mediator.PublishAsync(new ProductAddedNotification("ABC123", 1));

// Usage - MediatR API
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

## 📊 Available APIs

| Method | Return Type | Description | Style |
|--------|-------------|-------------|-------|
| `InvokeAsync` | `ValueTask<T>` | Executes a request | **Native** |
| `Send` | `Task<T>` | Executes a request | **MediatR** |
| `PublishAsync` | `ValueTask` | Publishes a notification | **Native** |
| `Publish` | `Task` | Publishes a notification | **MediatR** |

**Both APIs coexist** - choose whichever you prefer!

## 🔥 Advantages vs MediatR

| Feature | MediatR | ChannelMediator |
|----------------|---------|-----------------|
| API Compatible | ✅ | ✅ |
| Pipeline Behaviors | ✅ | ✅ |
| **Open Generic Behaviors** | ❌ | ✅ |
| **Parallel Notifications** | ❌ | ✅ |
| **Channel-Based** | ❌ | ✅ |
| **Backpressure** | ❌ | ✅ |
| **ValueTask** | ❌ | ✅ |

## 📚 Documentation

- [🔄 MediatR Compatibility](./MEDIATR_COMPATIBILITY.md)
- [🎭 Pipeline Behaviors](./PIPELINE_BEHAVIORS.md)
- [📊 Sequence Diagram](./SEQUENCE_DIAGRAM.md)

## 🏗️ Architecture

```
Client
  ↓
IMediator (Send / InvokeAsync)
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
await mediator.PublishAsync(notification);
```

### Sequential Notifications

```csharp
services.AddChannelMediator(config => 
    config.Strategy = NotificationPublishStrategy.Sequential);

// Handlers execute one after another
await mediator.PublishAsync(notification);
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
    var result = await mediator.InvokeAsync(new AddToCartRequest("TEST"));

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

1. **Performance** - Channel + ValueTask = fast
2. **Flexibility** - Native API + MediatR API
3. **Modern** - .NET 10, C# 14, modern patterns
4. **Powerful** - Global behaviors, parallel notifications
5. **Familiar** - MediatR compatible, easy migration

---

**Made with ❤️ for the .NET community**
