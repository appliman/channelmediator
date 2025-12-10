# ChannelMediator - Commands Without Response

ChannelMediator supporte maintenant les **commandes sans retour** (void commands), compatibles avec MediatR.

## Utilisation

### Définir une commande sans retour

```csharp
using ChannelMediator.Contracts;

// Commande sans retour - hérite de IRequest (sans type générique)
public record SendEmailCommand(string To, string Subject, string Body) : IRequest;
```

### Créer un handler pour la commande

```csharp
using ChannelMediator;

public class SendEmailCommandHandler : IRequestHandler<SendEmailCommand>
{
    public async ValueTask HandleAsync(SendEmailCommand command, CancellationToken cancellationToken)
    {
        // Votre logique d'envoi d'email
        await SendEmailAsync(command.To, command.Subject, command.Body);
    }
}
```

### Envoyer la commande

#### Avec InvokeAsync (API native)

```csharp
var command = new SendEmailCommand("user@example.com", "Hello", "Body");
await mediator.InvokeAsync(command);
```

#### Avec Send (compatible MediatR)

```csharp
var command = new SendEmailCommand("user@example.com", "Hello", "Body");
await mediator.Send(command);
```

## Types de requêtes

### Requêtes avec réponse (Queries/Commands avec retour)

```csharp
// Définition
public record GetProductQuery(string Id) : IRequest<Product>;

// Handler
public class GetProductQueryHandler : IRequestHandler<GetProductQuery, Product>
{
    public ValueTask<Product> HandleAsync(GetProductQuery request, CancellationToken ct)
    {
        // Retourne un produit
        return ValueTask.FromResult(new Product());
    }
}

// Utilisation
var product = await mediator.InvokeAsync(new GetProductQuery("123"));
// ou
var product = await mediator.Send(new GetProductQuery("123"));
```

### Commandes sans réponse (Void Commands)

```csharp
// Définition
public record LogOrderCommand(string OrderId) : IRequest;

// Handler
public class LogOrderCommandHandler : IRequestHandler<LogOrderCommand>
{
    public ValueTask HandleAsync(LogOrderCommand command, CancellationToken ct)
    {
        // Effectue l'action sans retourner de valeur
        Log(command.OrderId);
        return ValueTask.CompletedTask;
    }
}

// Utilisation
await mediator.InvokeAsync(new LogOrderCommand("ORD-123"));
// ou
await mediator.Send(new LogOrderCommand("ORD-123"));
```

## Type Unit

Internement, les commandes sans retour utilisent le type `Unit` qui représente l'absence de valeur :

```csharp
// IRequest hérite de IRequest<Unit>
public interface IRequest : IRequest<Unit> { }

// IRequestHandler<TRequest> hérite de IRequestHandler<TRequest, Unit>
public interface IRequestHandler<TRequest> : IRequestHandler<TRequest, Unit>
    where TRequest : IRequest<Unit>
{
    new ValueTask HandleAsync(TRequest request, CancellationToken ct);
}
```

Le type `Unit` est similaire à `void` mais peut être utilisé comme type de retour dans des génériques.

## Pipeline Behaviors

Les behaviors fonctionnent également avec les commandes sans retour :

```csharp
public class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public async ValueTask<TResponse> HandleAsync(
        TRequest request, 
        RequestHandlerDelegate<TResponse> next, 
        CancellationToken ct)
    {
        Console.WriteLine($"Before: {typeof(TRequest).Name}");
        var response = await next();
        Console.WriteLine($"After: {typeof(TRequest).Name}");
        return response;
    }
}

// S'applique aussi bien aux requêtes avec retour qu'aux commandes sans retour
services.AddPipelineBehavior<SendEmailCommand, Unit, LoggingBehavior<SendEmailCommand, Unit>>();
```

## Compatibilité MediatR

Cette implémentation est 100% compatible avec MediatR :

| MediatR | ChannelMediator |
|---------|----------------|
| `IRequest<TResponse>` | `IRequest<TResponse>` |
| `IRequest` (sans retour) | `IRequest` (hérite de `IRequest<Unit>`) |
| `IRequestHandler<TRequest, TResponse>` | `IRequestHandler<TRequest, TResponse>` |
| `IRequestHandler<TRequest>` | `IRequestHandler<TRequest>` (hérite de `IRequestHandler<TRequest, Unit>`) |
| `Send<TResponse>()` | `Send<TResponse>()` / `InvokeAsync<TResponse>()` |
| `Send()` (void) | `Send()` / `InvokeAsync()` |
| `Unit` | `Unit` |

## Exemple complet

```csharp
using ChannelMediator;
using ChannelMediator.Contracts;
using Microsoft.Extensions.DependencyInjection;

// 1. Définir la commande
public record ProcessOrderCommand(string OrderId, decimal Amount) : IRequest;

// 2. Créer le handler
public class ProcessOrderCommandHandler : IRequestHandler<ProcessOrderCommand>
{
    private readonly ILogger _logger;
    
    public ProcessOrderCommandHandler(ILogger logger)
    {
        _logger = logger;
    }
    
    public async ValueTask HandleAsync(ProcessOrderCommand command, CancellationToken ct)
    {
        _logger.LogInformation($"Processing order {command.OrderId}");
        // ... logique métier
        await Task.CompletedTask;
    }
}

// 3. Configuration
var services = new ServiceCollection();
services.AddChannelMediator(null, Assembly.GetExecutingAssembly());
var provider = services.BuildServiceProvider();

// 4. Utilisation
var mediator = provider.GetRequiredService<IMediator>();
await mediator.Send(new ProcessOrderCommand("ORD-123", 299.99m));
```

## Tests

La couverture de code pour cette fonctionnalité est de **98.7%** avec des tests complets pour :
- Commandes avec et sans retour
- Pipeline behaviors
- Gestion des erreurs
- Annulation (cancellation)
- Concurrence
- Compatibilité MediatR

Voir `ChannelMediator.Tests\CommandHandlerTests.cs` et `ChannelMediator.Tests\UnitTests.cs` pour des exemples de tests.
