# 📖 ChannelMediator - Exemples de Code

Ce document contient des exemples pratiques pour tous les cas d'usage courants.

## Table des Matières
- [Configuration de Base](#configuration-de-base)
- [Requests & Responses](#requests--responses)
- [Notifications](#notifications)
- [Pipeline Behaviors](#pipeline-behaviors)
- [Migration depuis MediatR](#migration-depuis-mediatr)
- [Patterns Avancés](#patterns-avancés)

---

## Configuration de Base

### Setup Minimal

```csharp
using ChannelMediator;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

var services = new ServiceCollection();

// Configuration minimale
services.AddChannelMediator(Assembly.GetExecutingAssembly());

var provider = services.BuildServiceProvider();
var mediator = provider.GetRequiredService<IMediator>();
```

### Setup avec Behaviors

```csharp
var services = new ServiceCollection();

// Configuration avec notifications parallèles
services.AddChannelMediator(
    config => config.Strategy = NotificationPublishStrategy.Parallel,
    Assembly.GetExecutingAssembly());

// Behaviors globaux (tous les requests)
services.AddOpenPipelineBehavior(typeof(LoggingBehavior<,>));
services.AddOpenPipelineBehavior(typeof(PerformanceMonitoringBehavior<,>));

// Behaviors spécifiques
services.AddPipelineBehavior<CreateOrderRequest, Order, ValidationBehavior<CreateOrderRequest, Order>>();

var provider = services.BuildServiceProvider();
var mediator = provider.GetRequiredService<IMediator>();
```

---

## Requests & Responses

### Exemple Simple

```csharp
// 1. Définir la request et la response
public record GetUserRequest(int UserId) : IRequest<User>;

public record User(int Id, string Name, string Email);

// 2. Créer le handler
public class GetUserHandler : IRequestHandler<GetUserRequest, User>
{
    private readonly IUserRepository _repository;

    public GetUserHandler(IUserRepository repository)
    {
        _repository = repository;
    }

    public async ValueTask<User> HandleAsync(
        GetUserRequest request, 
        CancellationToken cancellationToken)
    {
        var user = await _repository.GetByIdAsync(request.UserId, cancellationToken);
        
        if (user == null)
            throw new UserNotFoundException(request.UserId);
            
        return new User(user.Id, user.Name, user.Email);
    }
}

// 3. Utiliser le médiateur
// API Native
var user = await mediator.InvokeAsync(new GetUserRequest(123), cancellationToken);

// API MediatR
var user = await mediator.Send(new GetUserRequest(123), cancellationToken);
```

### Exemple avec Dépendances

```csharp
public record CreateOrderRequest(int UserId, List<OrderItem> Items) : IRequest<OrderCreated>;

public record OrderCreated(int OrderId, decimal TotalAmount);

public class CreateOrderHandler : IRequestHandler<CreateOrderRequest, OrderCreated>
{
    private readonly IOrderRepository _orderRepo;
    private readonly IInventoryService _inventoryService;
    private readonly IPaymentService _paymentService;
    private readonly ILogger<CreateOrderHandler> _logger;

    public CreateOrderHandler(
        IOrderRepository orderRepo,
        IInventoryService inventoryService,
        IPaymentService paymentService,
        ILogger<CreateOrderHandler> logger)
    {
        _orderRepo = orderRepo;
        _inventoryService = inventoryService;
        _paymentService = paymentService;
        _logger = logger;
    }

    public async ValueTask<OrderCreated> HandleAsync(
        CreateOrderRequest request, 
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Creating order for user {UserId}", request.UserId);

        // 1. Vérifier l'inventaire
        await _inventoryService.ReserveAsync(request.Items, cancellationToken);

        // 2. Calculer le total
        var total = request.Items.Sum(i => i.Price * i.Quantity);

        // 3. Créer la commande
        var orderId = await _orderRepo.CreateAsync(request.UserId, request.Items, total, cancellationToken);

        // 4. Traiter le paiement
        await _paymentService.ProcessAsync(orderId, total, cancellationToken);

        _logger.LogInformation("Order {OrderId} created successfully", orderId);

        return new OrderCreated(orderId, total);
    }
}

// Utilisation
var result = await mediator.InvokeAsync(
    new CreateOrderRequest(userId: 123, items: cartItems),
    cancellationToken);

Console.WriteLine($"Order {result.OrderId} created: {result.TotalAmount:C}");
```

### Exemple avec Cache

```csharp
public record GetProductRequest(string ProductCode) : IRequest<Product>;

public class GetProductHandler : IRequestHandler<GetProductRequest, Product>
{
    private readonly IProductRepository _repository;
    private readonly IMemoryCache _cache;

    public GetProductHandler(IProductRepository repository, IMemoryCache cache)
    {
        _repository = repository;
        _cache = cache;
    }

    public async ValueTask<Product> HandleAsync(
        GetProductRequest request, 
        CancellationToken cancellationToken)
    {
        var cacheKey = $"product:{request.ProductCode}";

        // Essayer le cache d'abord
        if (_cache.TryGetValue<Product>(cacheKey, out var cachedProduct))
        {
            return cachedProduct!;
        }

        // Cache miss - aller chercher en DB
        var product = await _repository.GetByCodeAsync(request.ProductCode, cancellationToken);

        // Mettre en cache pour 5 minutes
        _cache.Set(cacheKey, product, TimeSpan.FromMinutes(5));

        return product;
    }
}
```

---

## Notifications

### Notification Simple

```csharp
// 1. Définir la notification
public record UserRegisteredNotification(int UserId, string Email) : INotification;

// 2. Créer les handlers (plusieurs possibles)
public class SendWelcomeEmailHandler : INotificationHandler<UserRegisteredNotification>
{
    private readonly IEmailService _emailService;

    public SendWelcomeEmailHandler(IEmailService emailService)
    {
        _emailService = emailService;
    }

    public async ValueTask HandleAsync(
        UserRegisteredNotification notification, 
        CancellationToken cancellationToken)
    {
        await _emailService.SendWelcomeEmailAsync(
            notification.Email, 
            cancellationToken);
    }
}

public class CreateUserProfileHandler : INotificationHandler<UserRegisteredNotification>
{
    private readonly IProfileService _profileService;

    public CreateUserProfileHandler(IProfileService profileService)
    {
        _profileService = profileService;
    }

    public async ValueTask HandleAsync(
        UserRegisteredNotification notification, 
        CancellationToken cancellationToken)
    {
        await _profileService.CreateDefaultProfileAsync(
            notification.UserId, 
            cancellationToken);
    }
}

public class LogUserRegistrationHandler : INotificationHandler<UserRegisteredNotification>
{
    private readonly ILogger<LogUserRegistrationHandler> _logger;

    public LogUserRegistrationHandler(ILogger<LogUserRegistrationHandler> logger)
    {
        _logger = logger;
    }

    public ValueTask HandleAsync(
        UserRegisteredNotification notification, 
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "User {UserId} registered with email {Email}", 
            notification.UserId, 
            notification.Email);
        
        return ValueTask.CompletedTask;
    }
}

// 3. Publier la notification
await mediator.PublishAsync(
    new UserRegisteredNotification(userId: 123, email: "user@example.com"),
    cancellationToken);
```

### Notifications en Parallèle vs Séquentiel

```csharp
// Configuration PARALLÈLE (recommandé pour handlers indépendants)
services.AddChannelMediator(config => 
    config.Strategy = NotificationPublishStrategy.Parallel);

// Les 3 handlers s'exécutent en PARALLÈLE
await mediator.PublishAsync(new UserRegisteredNotification(123, "user@example.com"));
// → SendWelcomeEmailHandler       } En parallèle
// → CreateUserProfileHandler      } avec Task.WhenAll
// → LogUserRegistrationHandler    }

// Configuration SÉQUENTIELLE (pour handlers avec dépendances)
services.AddChannelMediator(config => 
    config.Strategy = NotificationPublishStrategy.Sequential);

// Les handlers s'exécutent l'un APRÈS l'autre
await mediator.PublishAsync(new OrderCreatedNotification(orderId));
// → ReserveInventoryHandler    (d'abord)
// → ProcessPaymentHandler      (puis)
// → SendConfirmationHandler    (enfin)
```

---

## Pipeline Behaviors

### Logging Behavior (Global)

```csharp
public class LoggingBehavior<TRequest, TResponse> 
    : IPipelineBehavior<TRequest, TResponse>, IPipelineBehavior
    where TRequest : IRequest<TResponse>
{
    private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _logger;

    public LoggingBehavior(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    {
        _logger = logger;
    }

    public async ValueTask<TResponse> HandleAsync(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;
        
        _logger.LogInformation("Handling {RequestName}: {@Request}", requestName, request);

        try
        {
            var response = await next();
            _logger.LogInformation("{RequestName} handled successfully", requestName);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{RequestName} failed: {Error}", requestName, ex.Message);
            throw;
        }
    }
}

// Enregistrement (s'applique à TOUS les requests)
services.AddOpenPipelineBehavior(typeof(LoggingBehavior<,>));
```

### Performance Monitoring Behavior (Global)

```csharp
public class PerformanceMonitoringBehavior<TRequest, TResponse> 
    : IPipelineBehavior<TRequest, TResponse>, IPipelineBehavior
    where TRequest : IRequest<TResponse>
{
    private readonly ILogger<PerformanceMonitoringBehavior<TRequest, TResponse>> _logger;
    private readonly IMetrics _metrics;

    public PerformanceMonitoringBehavior(
        ILogger<PerformanceMonitoringBehavior<TRequest, TResponse>> logger,
        IMetrics metrics)
    {
        _logger = logger;
        _metrics = metrics;
    }

    public async ValueTask<TResponse> HandleAsync(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;
        var sw = Stopwatch.StartNew();

        try
        {
            var response = await next();
            sw.Stop();

            var elapsed = sw.ElapsedMilliseconds;
            _metrics.RecordRequestDuration(requestName, elapsed);

            if (elapsed > 1000)
            {
                _logger.LogWarning(
                    "SLOW REQUEST: {RequestName} took {ElapsedMs}ms", 
                    requestName, 
                    elapsed);
            }

            return response;
        }
        catch
        {
            sw.Stop();
            _metrics.RecordRequestFailure(requestName);
            throw;
        }
    }
}

// Enregistrement
services.AddOpenPipelineBehavior(typeof(PerformanceMonitoringBehavior<,>));
```

### Validation Behavior (Spécifique)

```csharp
public class ValidationBehavior<TRequest, TResponse> 
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly IValidator<TRequest> _validator;

    public ValidationBehavior(IValidator<TRequest> validator)
    {
        _validator = validator;
    }

    public async ValueTask<TResponse> HandleAsync(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        // Valider la requête
        var validationResult = await _validator.ValidateAsync(request, cancellationToken);

        if (!validationResult.IsValid)
        {
            throw new ValidationException(validationResult.Errors);
        }

        return await next();
    }
}

// Enregistrement (seulement pour CreateOrderRequest)
services.AddPipelineBehavior<CreateOrderRequest, Order, ValidationBehavior<CreateOrderRequest, Order>>();
```

### Retry Behavior (Spécifique)

```csharp
public class RetryBehavior<TRequest, TResponse> 
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly ILogger<RetryBehavior<TRequest, TResponse>> _logger;
    private const int MaxRetries = 3;

    public RetryBehavior(ILogger<RetryBehavior<TRequest, TResponse>> logger)
    {
        _logger = logger;
    }

    public async ValueTask<TResponse> HandleAsync(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        for (int attempt = 1; attempt <= MaxRetries; attempt++)
        {
            try
            {
                return await next();
            }
            catch (Exception ex) when (attempt < MaxRetries && IsTransientError(ex))
            {
                _logger.LogWarning(
                    "Attempt {Attempt}/{MaxRetries} failed: {Error}. Retrying...", 
                    attempt, 
                    MaxRetries, 
                    ex.Message);

                await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)), cancellationToken);
            }
        }

        // Dernier essai sans catch
        return await next();
    }

    private static bool IsTransientError(Exception ex)
    {
        return ex is HttpRequestException 
            or TimeoutException 
            or SqlException { Number: -2 }; // Timeout
    }
}
```

### Transaction Behavior (Spécifique)

```csharp
public class TransactionBehavior<TRequest, TResponse> 
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly IDbContext _dbContext;
    private readonly ILogger<TransactionBehavior<TRequest, TResponse>> _logger;

    public TransactionBehavior(IDbContext dbContext, ILogger<TransactionBehavior<TRequest, TResponse>> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async ValueTask<TResponse> HandleAsync(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        await using var transaction = await _dbContext.BeginTransactionAsync(cancellationToken);

        try
        {
            _logger.LogInformation("Transaction started for {RequestName}", typeof(TRequest).Name);

            var response = await next();

            await transaction.CommitAsync(cancellationToken);
            _logger.LogInformation("Transaction committed for {RequestName}", typeof(TRequest).Name);

            return response;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            _logger.LogError(ex, "Transaction rolled back for {RequestName}", typeof(TRequest).Name);
            throw;
        }
    }
}

// Enregistrement (seulement pour les commandes qui modifient la DB)
services.AddPipelineBehavior<CreateOrderRequest, Order, TransactionBehavior<CreateOrderRequest, Order>>();
services.AddPipelineBehavior<UpdateInventoryRequest, Unit, TransactionBehavior<UpdateInventoryRequest, Unit>>();
```

---

## Migration depuis MediatR

### Avant (MediatR)

```csharp
// Startup.cs / Program.cs
services.AddMediatR(cfg => 
    cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly()));

// Controller
public class OrdersController : ControllerBase
{
    private readonly IMediator _mediator;

    public OrdersController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost]
    public async Task<IActionResult> CreateOrder([FromBody] CreateOrderRequest request)
    {
        var result = await _mediator.Send(request);
        return Ok(result);
    }
}
```

### Après (ChannelMediator) - Changement Minimal

```csharp
// Startup.cs / Program.cs
services.AddChannelMediator(
    config => config.Strategy = NotificationPublishStrategy.Parallel,
    Assembly.GetExecutingAssembly());

// Controller - AUCUN CHANGEMENT NÉCESSAIRE !
public class OrdersController : ControllerBase
{
    private readonly IMediator _mediator; // ← Même interface

    public OrdersController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost]
    public async Task<IActionResult> CreateOrder([FromBody] CreateOrderRequest request)
    {
        var result = await _mediator.Send(request); // ← Même appel !
        return Ok(result);
    }
}
```

---

## Patterns Avancés

### CQRS (Command Query Responsibility Segregation)

```csharp
// Marker interfaces
public interface ICommand<TResponse> : IRequest<TResponse> { }
public interface IQuery<TResponse> : IRequest<TResponse> { }

// Commands (write)
public record CreateProductCommand(string Name, decimal Price) : ICommand<int>;
public record UpdateProductCommand(int Id, string Name, decimal Price) : ICommand<Unit>;

// Queries (read)
public record GetProductQuery(int Id) : IQuery<ProductDto>;
public record ListProductsQuery(int PageSize, int PageNumber) : IQuery<List<ProductDto>>;

// Behaviors spécifiques aux Commands
public class CommandTransactionBehavior<TCommand, TResponse> 
    : IPipelineBehavior<TCommand, TResponse>
    where TCommand : ICommand<TResponse>
{
    // Transactions seulement pour les commands
}

services.AddPipelineBehavior<CreateProductCommand, int, CommandTransactionBehavior<CreateProductCommand, int>>();
```

### Result Pattern (Railway Oriented Programming)

```csharp
// Result type
public record Result<T>
{
    public T? Value { get; init; }
    public bool IsSuccess { get; init; }
    public string? Error { get; init; }

    public static Result<T> Success(T value) => new() { IsSuccess = true, Value = value };
    public static Result<T> Failure(string error) => new() { IsSuccess = false, Error = error };
}

// Request
public record CreateUserRequest(string Email, string Password) : IRequest<Result<User>>;

// Handler
public class CreateUserHandler : IRequestHandler<CreateUserRequest, Result<User>>
{
    public async ValueTask<Result<User>> HandleAsync(
        CreateUserRequest request, 
        CancellationToken cancellationToken)
    {
        if (await _userRepo.EmailExistsAsync(request.Email))
            return Result<User>.Failure("Email already exists");

        var user = new User { Email = request.Email };
        await _userRepo.AddAsync(user);

        return Result<User>.Success(user);
    }
}

// Utilisation
var result = await mediator.InvokeAsync(new CreateUserRequest("test@example.com", "password"));

if (result.IsSuccess)
    Console.WriteLine($"User created: {result.Value.Email}");
else
    Console.WriteLine($"Error: {result.Error}");
```

### Event Sourcing

```csharp
// Event base
public record DomainEvent(Guid AggregateId, DateTime OccurredAt) : INotification;

// Events
public record OrderCreatedEvent(Guid OrderId, int UserId, decimal Total) 
    : DomainEvent(OrderId, DateTime.UtcNow);

public record OrderPaidEvent(Guid OrderId, decimal Amount) 
    : DomainEvent(OrderId, DateTime.UtcNow);

// Event handlers
public class OrderCreatedEventHandler : INotificationHandler<OrderCreatedEvent>
{
    private readonly IEventStore _eventStore;

    public async ValueTask HandleAsync(OrderCreatedEvent @event, CancellationToken ct)
    {
        await _eventStore.AppendAsync(@event, ct);
    }
}

// Command handler qui publie des events
public class CreateOrderHandler : IRequestHandler<CreateOrderRequest, Guid>
{
    private readonly IMediator _mediator;

    public async ValueTask<Guid> HandleAsync(CreateOrderRequest request, CancellationToken ct)
    {
        var orderId = Guid.NewGuid();
        
        // ... créer la commande ...

        // Publier l'event
        await _mediator.PublishAsync(new OrderCreatedEvent(orderId, request.UserId, total), ct);

        return orderId;
    }
}
```

---

## 🎓 Best Practices

1. **Nommer les Requests clairement**: `CreateOrderRequest`, `GetUserQuery`
2. **Handlers simples**: Une seule responsabilité
3. **Behaviors pour cross-cutting concerns**: Logging, validation, transactions
4. **Parallel notifications** pour handlers indépendants
5. **Sequential notifications** pour workflows avec dépendances
6. **CancellationToken** toujours propagé
7. **Exceptions** pour les erreurs, `Result<T>` pour la logique métier

---

**Pour plus d'informations, consultez les autres documents:**
- [README.md](./README.md)
- [MEDIATR_COMPATIBILITY.md](./MEDIATR_COMPATIBILITY.md)
- [PIPELINE_BEHAVIORS.md](./PIPELINE_BEHAVIORS.md)
