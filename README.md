# 🚀 ChannelMediator

Un médiateur moderne et performant pour .NET, basé sur `System.Threading.Channels`, avec **compatibilité complète MediatR**.

## ✨ Caractéristiques

- ✅ **Compatible MediatR** - API familière (`Send` / `Publish`)
- ✅ **Channel-Based** - Traitement asynchrone avec backpressure naturelle
- ✅ **Pipeline Behaviors** - Globaux ET spécifiques
- ✅ **Parallel Notifications** - Diffusion séquentielle ou parallèle
- ✅ **High Performance** - ValueTask, Channel, optimisations modernes
- ✅ **.NET 10** - Code moderne avec C# 14

## 📦 Installation

```bash
# Package (à venir)
dotnet add package ChannelMediator

# Ou référence locale
<ProjectReference Include="..\ChannelMediator\ChannelMediator.csproj" />
```

## 🎯 Quick Start

### Configuration

```csharp
using ChannelMediator;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

var services = new ServiceCollection();

// Enregistrer le médiateur
services.AddChannelMediator(
    config => config.Strategy = NotificationPublishStrategy.Parallel,
    Assembly.GetExecutingAssembly());

var provider = services.BuildServiceProvider();
var mediator = provider.GetRequiredService<IMediator>();
```

### Définir une Request

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
        // Logique métier
        return new CartItem(request.ProductCode, 1, 19.99m);
    }
}
```

### Utilisation

```csharp
// API Native (recommandée)
var cart = await mediator.InvokeAsync(new AddToCartRequest("ABC123"));

// API MediatR (compatible)
var cart = await mediator.Send(new AddToCartRequest("ABC123"));
```

### Notifications

```csharp
// Notification
public record ProductAddedNotification(string ProductCode, int Quantity) : INotification;

// Handlers (multiple handlers possibles)
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

// Utilisation - API Native
await mediator.PublishAsync(new ProductAddedNotification("ABC123", 1));

// Utilisation - API MediatR
await mediator.Publish(new ProductAddedNotification("ABC123", 1));
```

## 🎭 Pipeline Behaviors

### Behaviors Globaux (pour TOUS les requests)

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

// Enregistrement
services.AddOpenPipelineBehavior(typeof(LoggingBehavior<,>));
services.AddOpenPipelineBehavior(typeof(PerformanceMonitoringBehavior<,>));
```

### Behaviors Spécifiques (pour un type de request)

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
        // Validation spécifique
        if (request is AddToCartRequest { ProductCode: null or "" })
            throw new ArgumentException("ProductCode required");
            
        return await next();
    }
}

// Enregistrement
services.AddPipelineBehavior<AddToCartRequest, CartItem, ValidationBehavior<AddToCartRequest, CartItem>>();
```

## 📊 APIs Disponibles

| Méthode | Type Retour | Description | Style |
|---------|-------------|-------------|-------|
| `InvokeAsync` | `ValueTask<T>` | Exécute un request | **Native** |
| `Send` | `Task<T>` | Exécute un request | **MediatR** |
| `PublishAsync` | `ValueTask` | Publie une notification | **Native** |
| `Publish` | `Task` | Publie une notification | **MediatR** |

**Les deux APIs coexistent** - choisissez celle que vous préférez !

## 🔥 Avantages vs MediatR

| Fonctionnalité | MediatR | ChannelMediator |
|----------------|---------|-----------------|
| API Compatible | ✅ | ✅ |
| Pipeline Behaviors | ✅ | ✅ |
| **Open Generic Behaviors** | ❌ | ✅ |
| **Parallel Notifications** | ❌ | ✅ |
| **Channel-Based** | ❌ | ✅ |
| **Backpressure** | ❌ | ✅ |
| **ValueTask** | ❌ | ✅ |

## 📚 Documentation

- [🔄 Compatibilité MediatR](./MEDIATR_COMPATIBILITY.md)
- [🎭 Pipeline Behaviors](./PIPELINE_BEHAVIORS.md)
- [📊 Diagramme de Séquence](./SEQUENCE_DIAGRAM.md)

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
Pipeline Behaviors (chaîne)
  ├─ Global Behavior 1
  ├─ Global Behavior 2
  ├─ Specific Behavior 1
  └─ Request Handler (métier)
```

## 🎯 Cas d'Usage

### Parfait pour:
- ✅ Applications avec forte charge (backpressure)
- ✅ Microservices avec patterns CQRS
- ✅ Migration depuis MediatR (drop-in replacement)
- ✅ APIs REST / gRPC avec orchestration complexe
- ✅ Event-driven architectures

### Exemples:
- **E-commerce**: Commandes, panier, checkout
- **CMS**: Publication, workflow, notifications
- **IoT**: Télémétrie, commandes, événements
- **Finance**: Transactions, audit, reporting

## ⚙️ Configuration Avancée

### Notifications Parallèles

```csharp
services.AddChannelMediator(config => 
    config.Strategy = NotificationPublishStrategy.Parallel);

// Tous les handlers s'exécutent en parallèle avec Task.WhenAll
await mediator.PublishAsync(notification);
```

### Notifications Séquentielles

```csharp
services.AddChannelMediator(config => 
    config.Strategy = NotificationPublishStrategy.Sequential);

// Les handlers s'exécutent l'un après l'autre
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

## 🔧 Compatibilité

- **.NET 10** (peut être rétro-porté à .NET 8)
- **C# 14** (peut être adapté pour C# 12)
- **Microsoft.Extensions.DependencyInjection 9.0+**

## 📝 Licence

MIT (à définir)

## 👥 Contribution

Les contributions sont les bienvenues ! Ouvrez une issue ou un PR.

## 🙏 Inspirations

- [MediatR](https://github.com/jbogard/MediatR) - L'original et toujours excellent
- [System.Threading.Channels](https://docs.microsoft.com/dotnet/api/system.threading.channels) - La base de notre implémentation

## ⭐ Pourquoi ChannelMediator ?

1. **Performance** - Channel + ValueTask = rapide
2. **Flexibilité** - API native + API MediatR
3. **Moderne** - .NET 10, C# 14, patterns modernes
4. **Puissant** - Behaviors globaux, parallel notifications
5. **Familier** - Compatible MediatR, migration facile

---

**Fait avec ❤️ pour la communauté .NET**
