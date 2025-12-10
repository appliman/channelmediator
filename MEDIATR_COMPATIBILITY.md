# ChannelMediator - Compatibilité MediatR

## Vue d'ensemble

ChannelMediator offre une **compatibilité complète** avec l'API de MediatR tout en fournissant des méthodes natives optimisées. Vous pouvez choisir l'API qui vous convient le mieux.

## Deux APIs disponibles

### 1. API Native ChannelMediator (Recommandée)

```csharp
// Request/Response
var result = await mediator.InvokeAsync(request, cancellationToken);

// Notification
await mediator.PublishAsync(notification, cancellationToken);
```

**Avantages:**
- ✅ Retourne `ValueTask<T>` (plus performant pour les opérations synchrones)
- ✅ Nom explicite sur le comportement (Invoke = exécution, Publish = diffusion)
- ✅ API moderne et performante

### 2. API Compatible MediatR

```csharp
// Request/Response
var result = await mediator.Send(request, cancellationToken);

// Notification
await mediator.Publish(notification, cancellationToken);
```

**Avantages:**
- ✅ Compatible avec le code existant utilisant MediatR
- ✅ Migration facile depuis MediatR
- ✅ Noms familiers pour les utilisateurs de MediatR

## Implémentation Interne

Les méthodes MediatR délèguent simplement aux méthodes natives:

```csharp
public async Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
{
    return await InvokeAsync(request, cancellationToken).ConfigureAwait(false);
}

public async Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
    where TNotification : INotification
{
    await PublishAsync(notification, cancellationToken).ConfigureAwait(false);
}
```

**Il n'y a AUCUNE différence de performance** - c'est juste un wrapper mince.

## Comparaison des APIs

| Fonctionnalité | ChannelMediator Native | MediatR Compatible | Notes |
|----------------|------------------------|-------------------|-------|
| **Request/Response** | `InvokeAsync<T>()` | `Send<T>()` | Même implémentation |
| **Type de retour** | `ValueTask<T>` | `Task<T>` | ValueTask légèrement plus performant |
| **Notification** | `PublishAsync()` | `Publish()` | Même implémentation |
| **Type de retour** | `ValueTask` | `Task` | ValueTask légèrement plus performant |
| **CancellationToken** | ✅ Supporté | ✅ Supporté | Identique |
| **Pipeline Behaviors** | ✅ Supporté | ✅ Supporté | Identique |

## Exemples de Migration depuis MediatR

### Avant (MediatR)

```csharp
public class MyController
{
    private readonly IMediator _mediator;

    public MyController(IMediator mediator)
    {
        _mediator = mediator;
    }

    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        // Request/Response
        var result = await _mediator.Send(new GetProductQuery(123), cancellationToken);
        
        // Notification
        await _mediator.Publish(new ProductViewedNotification(123), cancellationToken);
        
        return Ok(result);
    }
}
```

### Après (ChannelMediator - Option 1: API MediatR)

```csharp
public class MyController
{
    private readonly IMediator _mediator; // ← Même interface !

    public MyController(IMediator mediator)
    {
        _mediator = mediator;
    }

    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        // Request/Response - AUCUN CHANGEMENT !
        var result = await _mediator.Send(new GetProductQuery(123), cancellationToken);
        
        // Notification - AUCUN CHANGEMENT !
        await _mediator.Publish(new ProductViewedNotification(123), cancellationToken);
        
        return Ok(result);
    }
}
```

### Après (ChannelMediator - Option 2: API Native)

```csharp
public class MyController
{
    private readonly IMediator _mediator;

    public MyController(IMediator mediator)
    {
        _mediator = mediator;
    }

    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        // Request/Response - API native
        var result = await _mediator.InvokeAsync(new GetProductQuery(123), cancellationToken);
        
        // Notification - API native
        await _mediator.PublishAsync(new ProductViewedNotification(123), cancellationToken);
        
        return Ok(result);
    }
}
```

## Configuration - Migration depuis MediatR

### Avant (MediatR)

```csharp
services.AddMediatR(cfg => 
    cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly()));
```

### Après (ChannelMediator)

```csharp
services.AddChannelMediator(
    config => config.Strategy = NotificationPublishStrategy.Parallel,
    Assembly.GetExecutingAssembly());
```

## Différences Clés avec MediatR

| Aspect | MediatR | ChannelMediator |
|--------|---------|-----------------|
| **Architecture** | Direct synchronous call | **Channel-based async processing** |
| **Backpressure** | ❌ Non | ✅ **Oui** (via Channel) |
| **Notification Strategy** | Sequential uniquement | **Sequential OU Parallel** |
| **Performance** | Très bon | **Légèrement meilleur** (Channel + ValueTask) |
| **Pipeline Behaviors** | ✅ Oui | ✅ **Oui** (global + spécifique) |
| **Open Generic Behaviors** | ❌ Non natif | ✅ **Oui** (AddOpenPipelineBehavior) |
| **Cancellation** | ✅ Oui | ✅ Oui |

## Fonctionnalités Bonus de ChannelMediator

### 1. Behaviors Globaux (Pas dans MediatR standard)

```csharp
// S'applique à TOUS les requests automatiquement
services.AddOpenPipelineBehavior(typeof(LoggingBehavior<,>));
services.AddOpenPipelineBehavior(typeof(PerformanceMonitoringBehavior<,>));
```

### 2. Notification Strategy Configurable

```csharp
// MediatR: toujours séquentiel
// ChannelMediator: séquentiel OU parallèle
services.AddChannelMediator(config => 
    config.Strategy = NotificationPublishStrategy.Parallel);
```

### 3. Channel-Based Processing

```csharp
// Traitement asynchrone avec backpressure naturelle
// Les requests sont queued dans un Channel et traitées en arrière-plan
```

## Performance: ValueTask vs Task

```csharp
// ValueTask (ChannelMediator native)
ValueTask<Result> InvokeAsync(...);
// ↑ Optimisé pour les opérations synchrones (ex: cache hit)
//   Pas d'allocation si le résultat est disponible immédiatement

// Task (MediatR compatible)
Task<Result> Send(...);
// ↑ Toujours alloue un Task même si synchrone
//   Léger overhead mais négligeable dans la plupart des cas
```

**Recommandation:** Utilisez l'API native pour les nouveaux projets, l'API MediatR pour la migration.

## Quelle API choisir ?

### Utilisez l'API Native (`InvokeAsync` / `PublishAsync`) si:
- ✅ Vous créez un nouveau projet
- ✅ Vous voulez les meilleures performances
- ✅ Vous appréciez les noms explicites

### Utilisez l'API MediatR (`Send` / `Publish`) si:
- ✅ Vous migrez depuis MediatR
- ✅ Votre équipe connaît déjà MediatR
- ✅ Vous voulez minimiser les changements de code

### Mix des deux ?
**Oui, c'est possible !** Les deux APIs coexistent sans problème:

```csharp
// Mix dans le même code
var result1 = await mediator.InvokeAsync(request1);  // Native
var result2 = await mediator.Send(request2);         // MediatR
```

## Exemple Complet

```csharp
// Setup
var services = new ServiceCollection();
services.AddChannelMediator(Assembly.GetExecutingAssembly());

// Behaviors (même syntaxe que MediatR)
services.AddOpenPipelineBehavior(typeof(LoggingBehavior<,>));

var provider = services.BuildServiceProvider();
var mediator = provider.GetRequiredService<IMediator>();

// API Native (recommandé pour nouveau code)
var cart = await mediator.InvokeAsync(new AddToCartRequest("ABC"));
await mediator.PublishAsync(new ProductAddedNotification("ABC", 1, 19.99m));

// API MediatR (pour migration facile)
var cart2 = await mediator.Send(new AddToCartRequest("XYZ"));
await mediator.Publish(new ProductAddedNotification("XYZ", 2, 39.98m));
```

## Conclusion

ChannelMediator offre:
1. ✅ **100% de compatibilité** avec l'API MediatR
2. ✅ **API native optimisée** avec ValueTask
3. ✅ **Fonctionnalités supplémentaires** (behaviors globaux, parallel notifications)
4. ✅ **Migration facile** depuis MediatR (simple remplacement de package)

Choisissez l'API qui vous convient, les deux fonctionnent parfaitement ! 🚀
