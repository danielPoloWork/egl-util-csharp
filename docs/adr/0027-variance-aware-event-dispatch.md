# ADR-0027: Make EventBroker dispatch honor IEventHandler contravariance

- **Status:** Accepted
- **Date:** 2026-07-14
- **Deciders:** Maintainer
- **Related:** ADR-0007 (in-memory event broker — this ADR extends its dispatch), #71

## Context

`IEventHandler<in TEvent>` is declared **contravariant**, advertising that a handler registered for a
base type or interface can consume derived events — the natural way to write a catch-all
`IEventHandler<IDomainEvent>` audit/outbox handler. But `EventBroker` resolved handlers by the event's
**exact runtime type**: it built `IEnumerable<IEventHandler<runtimeType>>` (via `MakeGenericType`) and
resolved it from DI, and `Microsoft.Extensions.DependencyInjection` applies **no generic variance** when
materializing `IEnumerable<T>`. So a registration of `IEventHandler<BaseEvent>` (or a catch-all
`IEventHandler<IDomainEvent>`) was **never returned** for a published `DerivedEvent` — the handler
silently received nothing, no error and no log. The `in` modifier promised polymorphic consumption the
broker did not deliver (#71).

## Decision

Make dispatch variance-aware, so the interface's contravariance is honored (chosen over removing `in` or
merely documenting the gap):

- For each event runtime type, `EventDispatch.ForEventType` now enumerates the **set of handler types the
  event is assignable to** — its runtime type, each base class up to `object`, and every interface it
  implements — most-specific first. This set is computed once and cached per runtime type.
- For each such type `T`, the broker resolves `IEnumerable<IEventHandler<T>>` from the per-event scope and
  invokes each handler via a compiled `HandleAsync` delegate (casting the event to `T`). Handler failures
  stay isolated (logged; siblings and the consumer continue), and each event still dispatches in a single
  DI scope.

The public API is unchanged; the `in` modifier now means what it says.

## Alternatives Considered

- **Remove the `in` modifier** (`IEventHandler<TEvent>`). Rejected — a source-breaking API change that
  *removes* a capability, and it forecloses the useful base-typed/catch-all handler pattern the interface
  advertises.
- **Document that only exact-type handlers are dispatched.** Rejected — it leaves a public interface
  advertising (via `in`) a behavior it does not provide; documenting a contradiction is not resolving it.

## Consequences

- A handler registered for a base type or interface now receives derived events, so a catch-all
  `IEventHandler<IDomainEvent>` (audit/outbox) observes every published event, as `in` advertises. Exact-type
  dispatch is unchanged.
- **Behavior change:** base-typed/interface handlers that previously (silently) never fired now fire.
  This surfaces only latent, already-broken registrations (no correct behavior relied on the silence);
  weigh the release as at least a MINOR (a previously-advertised capability now works). No API signature
  change.
- A tiny per-event cost: each event resolves `IEnumerable<IEventHandler<T>>` for each base type/interface
  (empty sets are no-ops). The type list is cached per runtime type; the reflective build already existed
  and remains trim/AOT-unsafe (tracked by #49).

## References

- Issue #71; ADR-0007; `EventBroker`, `IEventHandler<TEvent>`.
