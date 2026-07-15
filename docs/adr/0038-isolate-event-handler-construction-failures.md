# ADR-0038: Isolate event-handler construction failures in the broker

- **Status:** Accepted
- **Date:** 2026-07-15
- **Deciders:** Maintainer
- **Related:** ADR-0007 (in-memory event broker), ADR-0027 (variance-aware dispatch), #89, #43

## Context

`EventBroker` advertises (class remarks, ADR-0007) that "a handler that throws is isolated … one faulty
handler never stops delivery." That isolation only wrapped handler **execution** — the `try`/`catch` around
`HandleAsync`. Handler **construction** was not covered.

Dispatch resolves `IEnumerable<IEventHandler<T>>` from the per-event DI scope, and Microsoft.Extensions.
DependencyInjection materializes that enumerable **eagerly**: it constructs every registered handler up front
and returns a fully-built array (verified empirically — a single throwing constructor makes the `GetService`
call itself throw, before any element is yielded). So if any one handler's constructor or injected-dependency
graph throws, the exception escapes before *any* handler of that type runs; it is caught only by the
consumer's last-resort `catch`, and the event is dropped for **every** handler of that type — including
perfectly healthy siblings. Failure scenario (#89): two handlers for `OrderPlaced`, one with a misconfigured
dependency whose factory throws — the healthy handler never sees the event either, contradicting the
documented guarantee (and, with no logger, observable only via the #43 `EventSource`).

## Decision

Make the isolation guarantee cover construction as well as execution (maintainer chose *fix*, not *document*,
via AskUserQuestion), with a **dual-path** dispatch that keeps the happy path unchanged:

- **Happy path (unchanged):** resolve `IEnumerable<IEventHandler<T>>` eagerly and invoke each handler with
  the existing per-handler execution isolation. When no constructor fails this dispatches to **every**
  registered handler — whether registered by `AddD4npUtil`'s scan or manually — exactly as before.
- **Fallback (new):** when the eager resolution throws, construct each **scanned** handler for that event
  **individually** and invoke it under its own `try`/`catch`, so a single faulty constructor is isolated and
  its healthy siblings still receive the event. The scanned handler types come from a new per-application
  `EventHandlerRegistry` that `AddD4npUtil` populates from the same scan that registers the handlers; the
  broker resolves it from the scope (no constructor-signature change). Handlers are rebuilt with
  `ActivatorUtilities.CreateInstance` (constructs one handler, resolving its dependencies from the scope,
  without re-resolving — and re-throwing for — the whole set). Scanned handlers are registered transient, for
  which this matches their DI lifetime.
- The isolated construction failure is reported through the `It.D4np.Util.EventBroker` `EventSource`
  unconditionally and through the logger when supplied — the same channels as execution failures (#43).

## Alternatives Considered

- **Document the limitation (narrow the promise to execution-only).** Rejected by the maintainer — the type
  already promises isolation; delivering on it is preferable to weakening it.
- **Single-path, registry-only dispatch** (always construct each scanned handler individually). Rejected: it
  would stop dispatching handlers registered *outside* `AddD4npUtil`'s scan even on the happy path — a
  behavior change beyond #89. The dual-path preserves the exact happy-path dispatch set.
- **Per-descriptor container introspection** to isolate *all* handlers (scanned and manual) on failure.
  Rejected: faithfully resolving an individual `ServiceDescriptor` (honoring scoped/singleton lifetimes,
  implementation factories, and caching) reimplements the container — disproportionate risk for a low-severity
  edge, and `ActivatorUtilities` would still mis-handle non-transient lifetimes.

## Consequences

- The documented "one faulty handler never stops delivery" now holds for **construction** as well as
  execution, for handlers discovered by `AddD4npUtil`. Behavior change: a healthy sibling that was previously
  dropped when a co-registered handler failed to construct now receives the event — weigh as at least a
  **MINOR** at release. No public-API change (the registry is internal; the broker constructor is unchanged).
- **No regression.** The happy path is byte-for-byte the old behavior (all handlers, correct lifetimes). Only
  the already-failing path changes, and strictly for the better. A handler registered outside the scan is not
  in the registry, so it is not *recovered* from a construction failure in its group — but it was fully lost
  before too, so nothing gets worse.
- A healthy handler already constructed during the failed eager attempt is constructed once more in the
  fallback; handler constructors should be side-effect-free (now documented on the broker).
- `ActivatorUtilities.CreateInstance` is trim/AOT-unsafe, consistent with the broker's existing
  `[RequiresDynamicCode]`/`[RequiresUnreferencedCode]` annotations.

## References

- Issue #89; ADR-0007, ADR-0027, #43.
- `src/main/csharp/it/d4np/util/Infrastructure/EventBroker.cs` (dual-path dispatch + fallback),
  `EventHandlerRegistry.cs` (new), `ServiceCollectionExtensions.cs` (registry population);
  `src/test/csharp/it/d4np/util/EventBrokerConstructionIsolationTests.cs` (regression).
