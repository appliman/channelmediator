# Instructions du projet

## Instructions générales Appliman

Avant toute intervention, lire et appliquer le fichier général [AGENTS.md d'Appliman](../../AGENTS.md) situé à `E:\S5\Appliman\AGENTS.md`, ainsi que les documents thématiques qu'il impose selon la tâche.

Les instructions spécifiques de ce projet et les fichiers `AGENTS.md` plus proches des fichiers modifiés restent prioritaires en cas de conflit.

## Instructions spécifiques reprises de Copilot

Source : [.github/copilot-instructions.md](.github/copilot-instructions.md).

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

## Code review language
When performing a code review, always write comments in French first and English.

## Vérification quotidienne des images Docker

- Appliquer la règle générale Appliman : vérifier les images Docker une seule fois par jour civil dans le fuseau `Europe/Paris`, pour les mêmes Dockerfile et références d'images. Lors des demandes de commit et push suivantes le même jour, réutiliser la trace persistante sans relancer la résolution des tags, la construction ou le scan des mêmes images.
- Conserver hors du dépôt le manifeste `V:\Codex\cache\docker-security\<projet>\<AAAA-MM-JJ>\daily-verification.json` et les rapports du scanner. Y enregistrer le dépôt, les chemins et empreintes SHA-256 des Dockerfile, les références et digests des images, la date UTC, la date locale Europe/Paris, l'état de la vérification et les vulnérabilités détectées, sans secret. Consulter cette trace avant toute nouvelle vérification et indiquer sa réutilisation dans le compte rendu.
- Les vulnérabilités résiduelles ne justifient pas de refaire le même scan ; conserver leur signalement et les règles de décision du fichier général. Un échec ou une vérification incomplète reste identifié comme tel, sans être présenté comme une réussite. Un Dockerfile ou une référence d'image réellement ajouté ou modifié constitue un nouvel ensemble à vérifier ; sinon, refaire la vérification au prochain besoin un autre jour.
