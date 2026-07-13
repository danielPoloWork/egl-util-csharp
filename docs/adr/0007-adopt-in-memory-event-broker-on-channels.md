# ADR-0007: Adopt an in-memory event broker on System.Threading.Channels

- **Status:** Accepted
- **Date:** 2026-06-30
- **Deciders:** Maintainer
- **Related:** ADR-0003, ADR-0004, spec §2 (requirements 15–17)

## Context

Spec §2 (15–17) calls for an in-memory publish/subscribe surface: `IEventBroker` (asynchronous
publish), `EventBroker` (a `System.Threading.Channels` implementation), and `IEventHandler<TEvent>`.
The publish must be non-blocking to the caller, safe under concurrent publishers, and have a clear
delivery and failure contract — this is the foundation later milestones build on.

## Decision

- **Abstractions in Application** (`IEventBroker`, `IEventHandler<TEvent>`); **implementation in
  Infrastructure** (`EventBroker : BackgroundService, IEventBroker`).
- A **bounded** `Channel<object>` with `FullMode.Wait` (back-pressure) and a **single** background
  consumer (`SingleReader`). Each event is dispatched from a fresh DI scope to the handlers registered
  for its **runtime** type, via a per-type **cached compiled delegate** (no reflection per dispatch).
- **Resilience:** a throwing handler is isolated — logged through an optional
  `ILogger<EventBroker>` and the consumer continues; one faulty handler never stops delivery to
  siblings or to later events. Handlers run with a non-cancelled token (run-to-completion; the broker
  does not force-cancel in-flight handlers). `PublishAsync` after shutdown throws
  `InvalidOperationException` rather than leaking `ChannelClosedException`.
- **Shutdown:** the broker stops accepting and makes a best-effort drain, bounded by the host's
  `ShutdownTimeout`.
- Adopt the **Producer-Consumer** (Concurrency) and **Publish-Subscribe** (EIP) patterns.

## Alternatives Considered

- **Publish via MediatR `INotification`.** Rejected for the broker — MediatR's publish awaits handlers
  inline (synchronous to the caller); the spec wants asynchronous, non-blocking in-memory queueing.
  (`DomainEventNotification<TEvent>` from ADR-0004 still bridges domain events onto MediatR's pipeline;
  its runtime-type factory — the part that makes the bridge usable from a dispatch callback — was added in
  ADR-0026.)
- **Unbounded channel.** Rejected — no back-pressure means unbounded memory growth under load; a
  bounded channel with `Wait` applies natural back-pressure.
- **Multiple consumers.** Deferred — a single consumer gives simple ordering and exactly-once consume;
  sharded/parallel consumers are a future option.
- **`MethodInfo.Invoke` per dispatch.** Rejected — it wraps synchronous handler throws in
  `TargetInvocationException`; a cached compiled delegate calls `HandleAsync` directly so exceptions
  surface as themselves.

## Consequences

- Infrastructure depends on `Microsoft.Extensions.Hosting.Abstractions`,
  `…DependencyInjection.Abstractions`, and `…Logging.Abstractions` (the allowed `Microsoft.Extensions`
  abstractions).
- Dispatch is by the event's runtime type (documented on `IEventBroker.PublishAsync`).
- Registration as a singleton `IEventBroker` **and** a hosted service is wired by the DI helper in
  Milestone 8.
- An adversarial review of the diff hardened the implementation before merge: per-handler exception
  isolation + logging, a non-cancelled token for the drain, compiled-delegate dispatch, the
  shutdown-timeout caveat, and the post-shutdown `PublishAsync` contract.

## References

- spec §2 (requirements 15–17); ADR-0003; ADR-0004.
- `docs/patterns/design-patterns.md` — Concurrency: *Producer-Consumer*; EIP: *Publish-Subscribe*.
