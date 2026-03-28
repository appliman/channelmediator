# ChannelMediator - Sequence Diagram

## Processing a Request with Pipeline Behaviors

The following diagram illustrates the complete execution flow of a request through the ChannelMediator, including global and specific behaviors.

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

    Note over Client,Cache: 1. Sending the Request
    Client->>Mediator: InvokeAsync(AddToCartRequest)

    Note over Mediator: Creating the envelope
    Mediator->>Mediator: new RequestEnvelope<CartItem>

    Note over Mediator,Channel: 2. Writing to the Channel (async)
    Mediator->>Channel: Writer.WriteAsync(envelope)
    Mediator-->>Client: Task<CartItem> (non-blocking)

    Note over Channel,Wrapper: 3. Reading from the Channel (background pump)
    Channel->>Channel: ReadAllAsync (background task)
    Channel->>Wrapper: envelope.DispatchAsync()

    Note over Wrapper: 4. Resolving behaviors via DI
    Wrapper->>Wrapper: GetServices<IPipelineBehavior<>>
    Note over Wrapper: Order: Correlation, PerfMon,<br/>Validation, Logging

    Note over Wrapper,Handler: 5. Building the Pipeline (reverse order)
    Wrapper->>Wrapper: Build pipeline chain

    Note over Correlation,Handler: 6. Pipeline Execution (behaviors + handler)
    
    Wrapper->>Correlation: HandleAsync(request, next)
    activate Correlation
    Note over Correlation: Generate correlationId
    Correlation->>Correlation: Log: "[CORRELATION] [abc123] Processing..."
    
    Correlation->>PerfMon: next() → HandleAsync(request, next)
    activate PerfMon
    Note over PerfMon: Start the stopwatch
    PerfMon->>PerfMon: Log: "[PERF-MONITOR] Request started..."
    
    PerfMon->>Validation: next() → HandleAsync(request, next)
    activate Validation
    Note over Validation: Validate ProductCode
    Validation->>Validation: Log: "[VALIDATION] Validating..."
    Validation->>Validation: Check: ProductCode not empty ✓
    Validation->>Validation: Log: "[VALIDATION] Valid"
    
    Validation->>Logging: next() → HandleAsync(request, next)
    activate Logging
    Note over Logging: Start Stopwatch
    Logging->>Logging: Log: "[BEHAVIOR] Handling AddToCartRequest..."
    
    Logging->>Handler: next() → HandleAsync(request)
    activate Handler
    Note over Handler: Business logic
    Handler->>Cache: TryGet("test")
    Cache-->>Handler: false (cache miss)
    Handler->>Handler: await Task.Delay(100ms)
    Handler->>Handler: new CartItem("test", 1, 19.90m)
    Handler->>Cache: Set("test", cartItem)
    Handler-->>Logging: CartItem
    deactivate Handler
    
    Note over Logging: Stop Stopwatch
    Logging->>Logging: Log: "[BEHAVIOR] Handled successfully in 105ms"
    Logging-->>Validation: CartItem
    deactivate Logging
    
    Note over Validation: No post-processing
    Validation-->>PerfMon: CartItem
    deactivate Validation
    
    Note over PerfMon: Calculate total duration
    PerfMon->>PerfMon: Log: "[PERF-MONITOR] 🚀 Completed in 107ms"
    PerfMon-->>Correlation: CartItem
    deactivate PerfMon
    
    Note over Correlation: Finalize tracking
    Correlation->>Correlation: Log: "[CORRELATION] [abc123] Completed"
    Correlation-->>Wrapper: CartItem
    deactivate Correlation
    
    Note over Wrapper,Client: 7. Returning the Result
    Wrapper->>Channel: TaskCompletionSource.SetResult(cartItem)
    Channel-->>Mediator: CartItem
    Mediator-->>Client: CartItem (Task complete)

    Note over Client: 8. Client receives the result
    Client->>Client: Console.WriteLine($"Added {cartItem}...")
```

## Diagram Legend

### Participants
- **Client**: The caller (Program.cs)
- **ChannelMediator**: Entry point of the mediator
- **Request Channel**: Asynchronous channel for background processing
- **RequestHandlerWrapper**: Wrapper that builds and executes the pipeline
- **Behaviors**: Behaviors in execution order
  - CorrelationBehavior (global)
  - PerformanceMonitoringBehavior (global)
  - ValidationBehavior (specific)
  - LoggingBehavior (specific)
- **AddToCartHandler**: The final business handler
- **ProductCache**: Cache service

### Execution Phases

#### Phase 1-2: Asynchronous Dispatch
The request is wrapped in an envelope and sent into the Channel. The client immediately receives a non-blocking `Task<CartItem>`.

#### Phase 3-4: Background Processing
A background task (pump) reads from the Channel and dispatches the request. The wrapper resolves all behaviors via DI.

#### Phase 5: Pipeline Construction
Behaviors are chained in reverse registration order, creating a decorator pattern.

#### Phase 6: Execution
The pipeline executes sequentially:
1. Each behavior calls `next()` to proceed to the next one
2. The final handler processes the business request
3. The result propagates back up the pipeline in reverse order
4. Each behavior can post-process the result

#### Phase 7-8: Result Return
The result is returned via the `TaskCompletionSource`, completing the client's `Task`.

## Behavior Execution Order

```
Configuration (Program.cs):
┌─────────────────────────────────────────┐
│ 1. AddOpenPipelineBehavior(Correlation) │
│ 2. AddOpenPipelineBehavior(PerfMon)     │
│ 3. AddPipelineBehavior(Validation)      │
│ 4. AddPipelineBehavior(Logging)         │
└─────────────────────────────────────────┘

Execution (reverse order = decorator):
┌──────────────────────────────────────────┐
│ → Correlation (start)                    │
│   → PerfMon (start)                      │
│     → Validation (start)                 │
│       → Logging (start)                  │
│         → HANDLER                        │
│       ← Logging (end)                    │
│     ← Validation (end)                   │
│   ← PerfMon (end)                        │
│ ← Correlation (end)                      │
└──────────────────────────────────────────┘
```

## Error Handling

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

## Performance and Asynchronism

### Advantages of the Channel-Based Approach
1. **Non-blocking**: The client immediately receives a Task
2. **Backpressure**: The Channel naturally manages load
3. **Single Reader**: Optimized for a single reader (pump)
4. **Cancellation**: CancellationToken support at every level

### Asynchronous Behavior of Behaviors
- Each behavior uses `ValueTask<TResponse>`
- Behaviors can contain async code (`await`)
- The entire pipeline is async end-to-end
- No synchronous blocking in the flow

## Technical Notes

1. **DI Scope**: A new scope is created in the wrapper for each request
2. **Reverse Order**: Behaviors are reversed (`.Reverse()`) for the correct execution order
3. **Delegate Chain**: Each behavior captures the previous `next` via closure
4. **Exception Handling**: Exceptions propagate back up the pipeline in reverse order
5. **Task Completion**: The `TaskCompletionSource` manages the asynchronous return to the client
