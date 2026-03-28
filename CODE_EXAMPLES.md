# 📖 ChannelMediator - Code Examples

This document contains practical examples for all common use cases.

## Table of Contents
- [Basic Configuration](#basic-configuration)
- [Requests & Responses](#requests--responses)
- [Notifications](#notifications)
- [Pipeline Behaviors](#pipeline-behaviors)
- [Migrating from MediatR](#migrating-from-mediatr)
- [Advanced Patterns](#advanced-patterns)

---

## Basic Configuration

### Minimal Setup

```csharp
using ChannelMediator;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

var services = new ServiceCollection();

// Minimal configuration
services.AddChannelMediator(Assembly.GetExecutingAssembly());

var provider = services.BuildServiceProvider();
var mediator = provider.GetRequiredService<IMediator>();
```

### Setup with Behaviors

```csharp
var services = new ServiceCollection();

// Configuration with parallel notifications
services.AddChannelMediator(
    config => config.Strategy = NotificationPublishStrategy.Parallel,
    Assembly.GetExecutingAssembly());

// Global behaviors (all requests)
services.AddOpenPipelineBehavior(typeof(LoggingBehavior<,>));
services.AddOpenPipelineBehavior(typeof(PerformanceMonitoringBehavior<,>));

// Specific behaviors
services.AddPipelineBehavior<CreateOrderRequest, Order, ValidationBehavior<CreateOrderRequest, Order>>();

var provider = services.BuildServiceProvider();
var mediator = provider.GetRequiredService<IMediator>();
```

---

## Requests & Responses

### Simple Example

```csharp
// 1. Define the request and response
public record GetUserRequest(int UserId) : IRequest<User>;

public record User(int Id, string Name, string Email);

// 2. Create the handler
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

// 3. Use the mediator
// Native API
var user = await mediator.InvokeAsync(new GetUserRequest(123), cancellationToken);

// MediatR API
var user = await mediator.Send(new GetUserRequest(123), cancellationToken);
```

### Example with Dependencies

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

        // 1. Check inventory
        await _inventoryService.ReserveAsync(request.Items, cancellationToken);

        // 2. Calculate total
        var total = request.Items.Sum(i => i.Price * i.Quantity);

        // 3. Create the order
        var orderId = await _orderRepo.CreateAsync(request.UserId, request.Items, total, cancellationToken);

        // 4. Process payment
        await _paymentService.ProcessAsync(orderId, total, cancellationToken);

        _logger.LogInformation("Order {OrderId} created successfully", orderId);

        return new OrderCreated(orderId, total);
    }
}

// Usage
var result = await mediator.InvokeAsync(
    new CreateOrderRequest(userId: 123, items: cartItems),
    cancellationToken);

Console.WriteLine($"Order {result.OrderId} created: {result.TotalAmount:C}");
```

### Example with Cache

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

        // Try cache first
        if (_cache.TryGetValue<Product>(cacheKey, out var cachedProduct))
        {
            return cachedProduct!;
        }

        // Cache miss - fetch from DB
        var product = await _repository.GetByCodeAsync(request.ProductCode, cancellationToken);

        // Cache for 5 minutes
        _cache.Set(cacheKey, product, TimeSpan.FromMinutes(5));

        return product;
    }
}
```

---

## Notifications

### Simple Notification

```csharp
// 1. Define the notification
public record UserRegisteredNotification(int UserId, string Email) : INotification;

// 2. Create the handlers (multiple handlers supported)
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

// 3. Publish the notification
await mediator.PublishAsync(
    new UserRegisteredNotification(userId: 123, email: "user@example.com"),
    cancellationToken);
```

### Parallel vs Sequential Notifications

```csharp
// PARALLEL configuration (recommended for independent handlers)
services.AddChannelMediator(config => 
    config.Strategy = NotificationPublishStrategy.Parallel);

// All 3 handlers execute in PARALLEL
await mediator.PublishAsync(new UserRegisteredNotification(123, "user@example.com"));
// → SendWelcomeEmailHandler       } In parallel
// → CreateUserProfileHandler      } with Task.WhenAll
// → LogUserRegistrationHandler    }

// SEQUENTIAL configuration (for handlers with dependencies)
services.AddChannelMediator(config => 
    config.Strategy = NotificationPublishStrategy.Sequential);

// Handlers execute one AFTER another
await mediator.PublishAsync(new OrderCreatedNotification(orderId));
// → ReserveInventoryHandler    (first)
// → ProcessPaymentHandler      (then)
// → SendConfirmationHandler    (finally)
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

// Registration (applies to ALL requests)
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

// Registration
services.AddOpenPipelineBehavior(typeof(PerformanceMonitoringBehavior<,>));
```

### Validation Behavior (Specific)

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
        // Validate the request
        var validationResult = await _validator.ValidateAsync(request, cancellationToken);

        if (!validationResult.IsValid)
        {
            throw new ValidationException(validationResult.Errors);
        }

        return await next();
    }
}

// Registration (only for CreateOrderRequest)
services.AddPipelineBehavior<CreateOrderRequest, Order, ValidationBehavior<CreateOrderRequest, Order>>();
```

### Retry Behavior (Specific)

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

        // Last attempt without catch
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

### Transaction Behavior (Specific)

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

// Registration (only for commands that modify the DB)
services.AddPipelineBehavior<CreateOrderRequest, Order, TransactionBehavior<CreateOrderRequest, Order>>();
services.AddPipelineBehavior<UpdateInventoryRequest, Unit, TransactionBehavior<UpdateInventoryRequest, Unit>>();
```

---

## Migrating from MediatR

### Before (MediatR)

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

### After (ChannelMediator) - Minimal Change

```csharp
// Startup.cs / Program.cs
services.AddChannelMediator(
    config => config.Strategy = NotificationPublishStrategy.Parallel,
    Assembly.GetExecutingAssembly());

// Controller - NO CHANGES NEEDED!
public class OrdersController : ControllerBase
{
    private readonly IMediator _mediator; // ← Same interface

    public OrdersController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost]
    public async Task<IActionResult> CreateOrder([FromBody] CreateOrderRequest request)
    {
        var result = await _mediator.Send(request); // ← Same call!
        return Ok(result);
    }
}
```

---

## Advanced Patterns

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

// Behaviors specific to Commands
public class CommandTransactionBehavior<TCommand, TResponse> 
    : IPipelineBehavior<TCommand, TResponse>
    where TCommand : ICommand<TResponse>
{
    // Transactions only for commands
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

// Usage
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

// Command handler that publishes events
public class CreateOrderHandler : IRequestHandler<CreateOrderRequest, Guid>
{
    private readonly IMediator _mediator;

    public async ValueTask<Guid> HandleAsync(CreateOrderRequest request, CancellationToken ct)
    {
        var orderId = Guid.NewGuid();

        // ... create the order ...

        // Publish the event
        await _mediator.PublishAsync(new OrderCreatedEvent(orderId, request.UserId, total), ct);

        return orderId;
    }
}
```

---

## 🎓 Best Practices

1. **Name Requests clearly**: `CreateOrderRequest`, `GetUserQuery`
2. **Simple Handlers**: Single responsibility
3. **Behaviors for cross-cutting concerns**: Logging, validation, transactions
4. **Parallel notifications** for independent handlers
5. **Sequential notifications** for workflows with dependencies
6. **CancellationToken** always propagated
7. **Exceptions** for errors, `Result<T>` for business logic

---

**For more information, see the other documents:**
- [README.md](./README.md)
- [MEDIATR_COMPATIBILITY.md](./MEDIATR_COMPATIBILITY.md)
- [PIPELINE_BEHAVIORS.md](./PIPELINE_BEHAVIORS.md)
