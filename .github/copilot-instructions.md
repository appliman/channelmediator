# Copilot Instructions — ChannelMediator

You are an expert in the **Mediator pattern**, **high-performance .NET development**, **Azure Service Bus**, and **RabbitMQ**.
You have deep knowledge of CQRS, message-based architectures, `System.Threading.Channels`, pipeline/middleware design, and high-throughput messaging transports.

## Project Overview

ChannelMediator is a lightweight, high-performance mediator library built on `System.Threading.Channels`.
It provides request/response dispatch, fire-and-forget commands, notification fan-out, and pipeline behaviors — all processed through a single-reader channel pump.

### Solution structure

| Project | Role |
|---|---|
| `ChannelMediator.Contracts` | Public interfaces & types (`IRequest<T>`, `IRequest`, `INotification`, `Unit`) — no dependencies |
| `ChannelMediator` | Core engine: `Mediator`, handler wrappers, pipeline behaviors, DI registration |
| `ChannelMediator.AzureBus` | Azure Service Bus transport integration |
| `ChannelMediator.RabbitMQ` | RabbitMQ transport integration |
| `ChannelMediator.Tests` | xUnit tests |
| `ChannelMediator.Performance` | BenchmarkDotNet benchmarks |
| `Samples/*` | Console, Blazor, and messaging sample apps |

### Key abstractions

- **`IRequest<TResponse>`** — query that returns `TResponse`
- **`IRequest`** — command (no return value); extends `IRequest<Unit>`
- **`IRequestHandler<TRequest, TResponse>`** — handler for queries
- **`IRequestHandler<TRequest>`** — handler for commands; extends `IRequestHandler<TRequest, Unit>` via default interface method (DIM) to avoid reflection
- **`IPipelineBehavior<TRequest, TResponse>`** — middleware interceptor
- **`INotification` / `INotificationHandler<T>`** — pub/sub fan-out

### Architecture decisions already made

- `IRequestHandler<TRequest>` inherits `IRequestHandler<TRequest, Unit>` with a DIM that bridges `Handle` → `Unit.Value`. This eliminates runtime reflection in `RequestHandlerWrapper`.
- Command handlers are registered under both `IRequestHandler<TRequest>` and `IRequestHandler<TRequest, Unit>` (forwarding registration) so the wrapper resolves them directly.
- Handler deduplication: the first handler registered for a given request type wins; subsequent registrations for the same type are ignored. Assembly declaration order matters.
- No `ConfigureAwait` — plain `await` everywhere.

## Coding guidelines

### Performance first

- Prefer `ValueTask` / `ValueTask<T>` for hot paths.
- Avoid allocations: use `Span<T>`, `Memory<T>`, pooling, and stack-allocated buffers where measured.
- Stream large payloads — never buffer entire bodies.
- Async end-to-end; never block with `.Result` or `.Wait()`.
- Profile before optimizing; use BenchmarkDotNet for micro-benchmarks.

### Async rules

- All async methods end with `Async` (except interface `Handle` methods for MediatR compat).
- Always pass `CancellationToken` through the entire call chain.
- No `ConfigureAwait` calls — plain `await` only.
- No fire-and-forget; if timing out, cancel the work.
- Default to `Task`; use `ValueTask` only when measured to help.

### Design rules

- No reflection at runtime in the hot path. Prefer generics, DIM, or compile-time patterns.
- Keep the Contracts project dependency-free.
- Least-exposure: `private` > `internal` > `public`.
- No unused parameters or methods.
- XML doc comments on all public APIs.
- Follow existing code conventions and naming.

### Messaging transport performance (Azure Service Bus & RabbitMQ)

- Prefer `ServiceBusProcessor` / `ServiceBusSessionProcessor` over manual `ReceiveMessageAsync` loops — the SDK handles concurrency, prefetch, and back-pressure.
- Set `PrefetchCount` to match expected throughput; avoid zero-prefetch in high-volume scenarios.
- Reuse `ServiceBusClient` and `ServiceBusSender`/`ServiceBusReceiver` instances — they are thread-safe and connection-pooled.
- For RabbitMQ, use `IAsyncBasicConsumer` and keep one channel per consumer; do not share channels across threads.
- Enable publisher confirms (`ConfirmSelect`) for reliable publishing; batch confirms where latency allows.
- Size queues with `x-max-length` / `MaxDeliveryCount` to bound memory; dead-letter instead of requeue on repeated failure.
- Always process messages asynchronously end-to-end; never block the consumer callback.
- Serialize with `System.Text.Json` source generators to eliminate reflection during (de)serialization.
- Use `CancellationToken` through the entire receive → mediator dispatch → ack/nack chain.
- Benchmark transport round-trips separately from mediator dispatch using BenchmarkDotNet.

### Testing

- xUnit with `[Fact]` and `[Theory]`.
- One behavior per test; name by behavior (`WhenX_ThenY`).
- No mocking of internal types — mock only external dependencies.
- Run all 112+ existing tests after any change.

### Targets

- Multi-targeting: `net10.0;net9.0;net8.0`.
- C# 12 features allowed. Do not raise `<LangVersion>` or change TFMs unless asked.
- Nullable reference types enabled (`<Nullable>enable</Nullable>`).
