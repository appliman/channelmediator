# ChannelMediator - Diagramme de Séquence

## Traitement d'une Request avec Pipeline Behaviors

Le diagramme suivant illustre le flux complet d'exécution d'une requête à travers le ChannelMediator, incluant les behaviors globaux et spécifiques.

```mermaid
sequenceDiagram
    participant Client
    participant Mediator as ChannelMediator
    participant Channel as Request Channel
    participant Wrapper as RequestHandlerWrapper
    participant Correlation as CorrelationBehavior
    participant PerfMon as PerformanceMonitoringBehavior
    participant Validation as ValidationBehavior
    participant Logging as LoggingBehavior
    participant Handler as AddToCartHandler
    participant Cache as ProductCache

    Note over Client,Cache: 1. Envoi de la Request
    Client->>Mediator: InvokeAsync(AddToCartRequest)
    
    Note over Mediator: Création de l'enveloppe
    Mediator->>Mediator: new RequestEnvelope<CartItem>
    
    Note over Mediator,Channel: 2. Écriture dans le Channel (async)
    Mediator->>Channel: Writer.WriteAsync(envelope)
    Mediator-->>Client: Task<CartItem> (non-bloquant)
    
    Note over Channel,Wrapper: 3. Lecture depuis le Channel (background pump)
    Channel->>Channel: ReadAllAsync (background task)
    Channel->>Wrapper: envelope.DispatchAsync()
    
    Note over Wrapper: 4. Résolution des behaviors via DI
    Wrapper->>Wrapper: GetServices<IPipelineBehavior<>>
    Note over Wrapper: Ordre: Correlation, PerfMon,<br/>Validation, Logging

    Note over Wrapper,Handler: 5. Construction du Pipeline (ordre inverse)
    Wrapper->>Wrapper: Build pipeline chain

    Note over Correlation,Handler: 6. Exécution du Pipeline (behaviors + handler)
    
    Wrapper->>Correlation: HandleAsync(request, next)
    activate Correlation
    Note over Correlation: Génère correlationId
    Correlation->>Correlation: Log: "[CORRELATION] [abc123] Processing..."
    
    Correlation->>PerfMon: next() → HandleAsync(request, next)
    activate PerfMon
    Note over PerfMon: Démarre le chronomètre
    PerfMon->>PerfMon: Log: "[PERF-MONITOR] Request started..."
    
    PerfMon->>Validation: next() → HandleAsync(request, next)
    activate Validation
    Note over Validation: Valide ProductCode
    Validation->>Validation: Log: "[VALIDATION] Validating..."
    Validation->>Validation: Check: ProductCode not empty ✓
    Validation->>Validation: Log: "[VALIDATION] Valid"
    
    Validation->>Logging: next() → HandleAsync(request, next)
    activate Logging
    Note over Logging: Démarre Stopwatch
    Logging->>Logging: Log: "[BEHAVIOR] Handling AddToCartRequest..."
    
    Logging->>Handler: next() → HandleAsync(request)
    activate Handler
    Note over Handler: Traitement métier
    Handler->>Cache: TryGet("test")
    Cache-->>Handler: false (cache miss)
    Handler->>Handler: await Task.Delay(100ms)
    Handler->>Handler: new CartItem("test", 1, 19.90m)
    Handler->>Cache: Set("test", cartItem)
    Handler-->>Logging: CartItem
    deactivate Handler
    
    Note over Logging: Arrête Stopwatch
    Logging->>Logging: Log: "[BEHAVIOR] Handled successfully in 105ms"
    Logging-->>Validation: CartItem
    deactivate Logging
    
    Note over Validation: Pas de post-traitement
    Validation-->>PerfMon: CartItem
    deactivate Validation
    
    Note over PerfMon: Calcule durée totale
    PerfMon->>PerfMon: Log: "[PERF-MONITOR] 🚀 Completed in 107ms"
    PerfMon-->>Correlation: CartItem
    deactivate PerfMon
    
    Note over Correlation: Finalise tracking
    Correlation->>Correlation: Log: "[CORRELATION] [abc123] Completed"
    Correlation-->>Wrapper: CartItem
    deactivate Correlation
    
    Note over Wrapper,Client: 7. Retour du Résultat
    Wrapper->>Channel: TaskCompletionSource.SetResult(cartItem)
    Channel-->>Mediator: CartItem
    Mediator-->>Client: CartItem (Task complete)
    
    Note over Client: 8. Client reçoit le résultat
    Client->>Client: Console.WriteLine($"Added {cartItem}...")
```

## Légende du Diagramme

### Participants
- **Client**: L'appelant (Program.cs)
- **ChannelMediator**: Point d'entrée du médiateur
- **Request Channel**: Channel asynchrone pour le traitement en arrière-plan
- **RequestHandlerWrapper**: Wrapper qui construit et exécute le pipeline
- **Behaviors**: Les comportements dans l'ordre d'exécution
  - CorrelationBehavior (global)
  - PerformanceMonitoringBehavior (global)
  - ValidationBehavior (spécifique)
  - LoggingBehavior (spécifique)
- **AddToCartHandler**: Le handler métier final
- **ProductCache**: Service de cache

### Phases d'Exécution

#### Phase 1-2: Envoi Asynchrone
La requête est encapsulée dans une enveloppe et envoyée dans le Channel. Le client reçoit immédiatement une `Task<CartItem>` non bloquante.

#### Phase 3-4: Traitement en Arrière-Plan
Un task en arrière-plan (pump) lit le Channel et dispatche la requête. Le wrapper résout tous les behaviors via DI.

#### Phase 5: Construction du Pipeline
Les behaviors sont chaînés dans l'ordre inverse de leur enregistrement, créant un pattern décorateur.

#### Phase 6: Exécution
Le pipeline s'exécute de manière séquentielle:
1. Chaque behavior appelle `next()` pour passer au suivant
2. Le handler final traite la requête métier
3. Le résultat remonte le pipeline en ordre inverse
4. Chaque behavior peut post-traiter le résultat

#### Phase 7-8: Retour du Résultat
Le résultat est renvoyé via le `TaskCompletionSource`, complétant la `Task` du client.

## Ordre d'Exécution des Behaviors

```
Configuration (Program.cs):
┌─────────────────────────────────────────┐
│ 1. AddOpenPipelineBehavior(Correlation) │
│ 2. AddOpenPipelineBehavior(PerfMon)     │
│ 3. AddPipelineBehavior(Validation)      │
│ 4. AddPipelineBehavior(Logging)         │
└─────────────────────────────────────────┘

Exécution (ordre inverse = décorateur):
┌──────────────────────────────────────────┐
│ → Correlation (début)                    │
│   → PerfMon (début)                      │
│     → Validation (début)                 │
│       → Logging (début)                  │
│         → HANDLER                        │
│       ← Logging (fin)                    │
│     ← Validation (fin)                   │
│   ← PerfMon (fin)                        │
│ ← Correlation (fin)                      │
└──────────────────────────────────────────┘
```

## Gestion des Erreurs

```mermaid
sequenceDiagram
    participant Client
    participant Mediator
    participant Behavior1
    participant Behavior2
    participant Handler

    Client->>Mediator: InvokeAsync(request)
    Mediator->>Behavior1: HandleAsync(request, next)
    Behavior1->>Behavior2: next()
    Behavior2->>Handler: next()
    Handler--xBehavior2: Exception ❌
    Note over Behavior2: try/catch
    Behavior2->>Behavior2: Log error
    Behavior2--xBehavior1: Exception (rethrow)
    Note over Behavior1: try/catch
    Behavior1->>Behavior1: Log with correlationId
    Behavior1--xMediator: Exception (rethrow)
    Mediator->>Mediator: TaskCompletionSource.SetException()
    Mediator--xClient: Exception propagated
```

## Performance et Asynchronisme

### Avantages du Channel-Based Approach
1. **Non-bloquant**: Le client reçoit immédiatement une Task
2. **Backpressure**: Le Channel gère naturellement la charge
3. **Single Reader**: Optimisation pour un lecteur unique (pump)
4. **Cancellation**: Support du CancellationToken à tous les niveaux

### Comportement Asynchrone des Behaviors
- Chaque behavior utilise `ValueTask<TResponse>`
- Les behaviors peuvent contenir du code async (`await`)
- Le pipeline complet est async de bout en bout
- Pas de blocage synchrone dans le flux

## Notes Techniques

1. **Scope DI**: Un nouveau scope est créé dans le wrapper pour chaque requête
2. **Reverse Order**: Les behaviors sont inversés (`.Reverse()`) pour l'ordre d'exécution correct
3. **Delegate Chain**: Chaque behavior capture le `next` précédent via closure
4. **Exception Handling**: Les exceptions remontent le pipeline en ordre inverse
5. **Task Completion**: Le `TaskCompletionSource` gère le retour asynchrone au client
