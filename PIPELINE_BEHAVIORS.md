# ChannelMediator - Pipeline Behaviors

## Overview

ChannelMediator supporte les **Pipeline Behaviors**, similaires à MediatR, permettant d'ajouter des comportements cross-cutting avant et après l'exécution des handlers.

## Types de Behaviors

### 1. Behaviors Spécifiques (Specific Behaviors)

Les behaviors spécifiques s'appliquent uniquement à un type de requête donné.

**Enregistrement:**
```csharp
services.AddPipelineBehavior<AddToCartRequest, CartItem, ValidationBehavior<AddToCartRequest, CartItem>>();
```

**Exemple:**
```csharp
public class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public async ValueTask<TResponse> HandleAsync(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        // Validation logic
        if (request is AddToCartRequest addToCart && string.IsNullOrWhiteSpace(addToCart.ProductCode))
        {
            throw new ArgumentException("ProductCode cannot be empty");
        }

        return await next();
    }
}
```

### 2. Behaviors Globaux (Global/Open Behaviors)

Les behaviors globaux s'appliquent automatiquement à **TOUS** les request handlers sans configuration unitaire.

**Enregistrement:**
```csharp
services.AddOpenPipelineBehavior(typeof(PerformanceMonitoringBehavior<,>));
services.AddOpenPipelineBehavior(typeof(CorrelationBehavior<,>));
```

**Important:** Le type doit être un generic ouvert (open generic) avec `<,>`.

**Exemple:**
```csharp
// Le marker interface IPipelineBehavior indique que c'est un behavior global
public class PerformanceMonitoringBehavior<TRequest, TResponse> 
    : IPipelineBehavior<TRequest, TResponse>, IPipelineBehavior
    where TRequest : IRequest<TResponse>
{
    public async ValueTask<TResponse> HandleAsync(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        
        var response = await next();
        
        sw.Stop();
        Console.WriteLine($"Request completed in {sw.ElapsedMilliseconds}ms");
        
        return response;
    }
}
```

## Ordre d'Exécution

Les behaviors s'exécutent dans l'ordre inverse de leur enregistrement, créant un pattern de décorateur:

```csharp
// Ordre d'enregistrement
services.AddOpenPipelineBehavior(typeof(CorrelationBehavior<,>));           // 1
services.AddOpenPipelineBehavior(typeof(PerformanceMonitoringBehavior<,>)); // 2
services.AddPipelineBehavior<Request, Response, ValidationBehavior<...>>(); // 3
services.AddPipelineBehavior<Request, Response, LoggingBehavior<...>>();    // 4

// Ordre d'exécution
// -> CorrelationBehavior        (début)
//    -> PerformanceMonitoring   (début)
//       -> ValidationBehavior   (début)
//          -> LoggingBehavior   (début)
//             -> HANDLER RÉEL
//          <- LoggingBehavior   (fin)
//       <- ValidationBehavior   (fin)
//    <- PerformanceMonitoring   (fin)
// <- CorrelationBehavior        (fin)
```

## Cas d'Usage

### Behaviors Globaux (recommandés pour)
- ✅ Logging et monitoring
- ✅ Corrélation ID / Request tracking
- ✅ Métriques de performance
- ✅ Gestion d'erreurs globale
- ✅ Audit trail
- ✅ Cache global

### Behaviors Spécifiques (recommandés pour)
- ✅ Validation métier spécifique
- ✅ Autorisation par type de requête
- ✅ Retry logic spécifique
- ✅ Cache spécifique à un type
- ✅ Transformation de données

## Configuration Complète

```csharp
var services = new ServiceCollection();

// Configuration du médiateur
services.AddChannelMediator(
    config => config.Strategy = NotificationPublishStrategy.Parallel,
    Assembly.GetExecutingAssembly());

// Behaviors GLOBAUX (s'appliquent à TOUTES les requêtes)
services.AddOpenPipelineBehavior(typeof(CorrelationBehavior<,>));
services.AddOpenPipelineBehavior(typeof(PerformanceMonitoringBehavior<,>));

// Behaviors SPÉCIFIQUES (s'appliquent seulement à AddToCartRequest)
services.AddPipelineBehavior<AddToCartRequest, CartItem, ValidationBehavior<AddToCartRequest, CartItem>>();
services.AddPipelineBehavior<AddToCartRequest, CartItem, LoggingBehavior<AddToCartRequest, CartItem>>();
```

## Avantages

1. **Séparation des préoccupations**: Les cross-cutting concerns sont isolés
2. **Réutilisabilité**: Un behavior peut être appliqué à plusieurs requêtes
3. **Testabilité**: Les behaviors peuvent être testés indépendamment
4. **Flexibilité**: Mix de behaviors globaux et spécifiques
5. **Performance**: Pas d'overhead si aucun behavior n'est enregistré

## Notes Techniques

- Les behaviors sont résolus via Dependency Injection avec un scope
- Les behaviors sont exécutés de manière asynchrone avec `ValueTask`
- Le delegate `next()` doit être appelé pour continuer le pipeline
- Les exceptions remontent le pipeline dans l'ordre inverse
