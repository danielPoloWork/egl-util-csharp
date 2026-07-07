# ADR-0022: Drain domain events to a fixed point during dispatch

- **Status:** Accepted
- **Date:** 2026-07-07
- **Deciders:** Maintainer
- **Related:** ADR-0007 (in-memory event broker); ADR-0012 (capture-before-save dispatch — this ADR amends its delivery contract); spec §4

## Context

Both `DispatchDomainEventsAsync` overloads published an aggregate's buffered events and then called
`ClearDomainEvents()`, which wipes the aggregate's **entire** buffer. If publishing an event causes a
handler to raise a further event on the same aggregate (a synchronous, in-process `publish`), that event
is appended to the buffer but was not in the snapshot being iterated — so it was cleared without ever
being published. Silent, exception-free domain-event loss; the documented at-least-once contract did not
cover handler-raised events (#30).

## Decision

Each aggregate is now **drained to a fixed point**: after publishing its current events, the dispatcher
re-reads the aggregate's buffer and publishes any events that appeared while handlers ran, repeating until
none remain, and only then clears the buffer. Published events are tracked by reference identity so the
whole-buffer clear is deferred until everything — including handler-raised events — has been delivered.
This applies to both the change-tracker and the snapshot overload.

The existing **at-least-once + per-aggregate clear-on-success** contract (ADR-0012) is preserved: on a
publish failure the exception propagates before the clear, so the failing aggregate and every aggregate
after it keep their events for a retry, and already-cleared aggregates are not re-published. This ADR
**amends ADR-0012's delivery contract** to additionally cover events a handler raises on the aggregate
currently being dispatched.

## Alternatives Considered

- **Clear only the published events (leave handler-raised ones buffered).** Rejected — it requires adding
  a remove-specific-events member to the frozen `AggregateRoot` / `IHasDomainEvents` surface, and it does
  not itself deliver the handler-raised events (they sit buffered until some later re-dispatch).
- **Document the loss as a known limitation (no behavior change).** Rejected by the maintainer — it leaves
  a silent-loss footgun in the at-least-once contract.

## Consequences

- An event a handler raises on the aggregate currently being dispatched is delivered in the same pass;
  at-least-once now covers handler-raised events.
- A handler that raises events on the same aggregate **without bound** will not terminate — a consumer
  error (unbounded event generation), analogous to any non-terminating recursion.
- The drain is **per-aggregate**, not global. An event a handler raises on the aggregate currently being
  dispatched is delivered; an event raised on a *different* aggregate is delivered only if that aggregate
  is still pending later in the same pass. Two residuals are therefore not delivered by the current pass:
  an event raised on an aggregate already dispatched-and-cleared, and an event raised on an aggregate that
  had **no** events when the pass began (so it was never in the dispatch set). Raise cross-aggregate
  follow-ups in a new unit of work.
- No public API or signature change; the clear-on-success retry semantics are unchanged.

## References

- Issue #30; ADR-0007; ADR-0012 (amended); spec §4.
