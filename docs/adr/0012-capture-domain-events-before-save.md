# ADR-0012: Capture domain events before SaveChanges so removed aggregates are not dropped

- **Status:** Accepted
- **Date:** 2026-07-06
- **Deciders:** Maintainer
- **Related:** ADR-0006 (amends its domain-event dispatch guidance), spec §2, §5, issue #21

## Context

ADR-0006 introduced `DbContextExtensions.DispatchDomainEventsAsync(this DbContext, …)` and
documented calling it **after** a committed `SaveChangesAsync`, so events are never published for a
rolled-back unit of work. That method sources the pending events by re-scanning
`context.ChangeTracker.Entries<IHasDomainEvents>()` at dispatch time.

EF Core detaches `Deleted` entities from the change tracker once `SaveChanges` completes
(`AcceptAllChangesOnSave`, on by default). An aggregate that raises an event and is then removed in
the same unit of work — the canonical `order.Cancel()` (buffers `OrderCancelledEvent`) →
`repository.Remove(order)` → `SaveChangesAsync` sequence — is therefore gone from the change tracker
by the time the post-save dispatch runs. Its events are **silently dropped**: no exception, no
diagnostic. This is verified to reproduce on both the EF Core InMemory and SQLite providers
(issue #21, High severity), and it contradicts the intent that every raised domain event is
delivered at-least-once.

The published behaviour cannot be fixed inside the existing post-save method alone, because the
information it needs (the removed aggregate and its events) no longer exists in the change tracker
once the save has committed. The events must be captured **while the aggregate is still tracked**,
i.e. before `SaveChanges`.

## Decision

Add a capture-then-dispatch flow to `DbContextExtensions`, additive to the frozen 1.0 surface (a
minor version bump under SemVer):

- `CaptureDomainEvents(this DbContext)` returns a `DomainEventSnapshot` — a point-in-time capture
  taken **before** `SaveChangesAsync`. The snapshot holds a strong reference to each aggregate
  together with a copy of its buffered events, so nothing is lost when the save removes and detaches
  an aggregate. The snapshot is drained as it is dispatched (only its `Events` view is stable across
  dispatch), which is what lets a re-dispatch after a failure re-drive just the outstanding aggregates.
- `DispatchDomainEventsAsync(this DomainEventSnapshot, publish, ct)` publishes the captured events
  **after** the commit and clears each source aggregate's buffer on success. It preserves the
  established at-least-once contract: events publish in the captured order, an aggregate's buffer is
  cleared only once all of its events are published, and re-dispatching the same snapshot after a
  failure re-drives only the still-undelivered aggregates.

This extends the existing Unit of Work / domain-event dispatch mechanism (ADR-0006) so it survives
deletes; it also strengthens the *outbox-style* dispatch that the `PeekDomainEvents` /
`ClearDomainEvents` primitives already enabled. It is **not** the durable Outbox pattern: nothing is
persisted to an outbox table, so delivery remains in-process and at-least-once only within the
process lifetime — a crash between the commit and the dispatch loses the captured events, exactly as
with the change-tracker overload. The existing change-tracker overload is retained for backward
compatibility and for units of work that never delete aggregates, but its XML documentation now
states the removed-aggregate limitation loudly and points to the capture flow as the safe default.

## Alternatives Considered

- **Fix the post-save method in place.** Rejected — impossible: once the save commits, the removed
  aggregate is already detached, so no post-save re-scan can recover its events.
- **Dispatch before `SaveChanges`.** Rejected — publishing before the commit would deliver events for
  a unit of work that later rolls back, breaking the "never publish for a rolled-back transaction"
  guarantee ADR-0006 established.
- **Override `SaveChangesAsync` in a library-provided `DbContext` base to auto-capture and
  auto-dispatch.** Rejected — it forces a base class on consumers and couples persistence to a
  publisher, contradicting ADR-0006's decision to keep `DbContextExtensions` bus-agnostic (the
  publisher is an injected delegate).
- **Return the raw `IReadOnlyList<(IHasDomainEvents, IReadOnlyList<IDomainEvent>)>` from capture.**
  Rejected — a named `DomainEventSnapshot` gives a smaller, documented public surface, hides the
  drain-on-retry bookkeeping, and reads better at call sites.
- **Deprecate the change-tracker overload with `[Obsolete]`.** Deferred — an `[Obsolete]` warning
  would break consumers building warnings-as-errors, a de-facto breaking change unfit for a minor
  release. Documenting the limitation and providing the safe alternative is the minimal, non-breaking
  step; deprecation can be reconsidered for a future major.

## Consequences

- **API / compatibility:** additive only — one new public type (`DomainEventSnapshot`) and two new
  extension methods. No existing signature or behaviour changes, so the change is a minor
  (1.0 → 1.x) version bump under SemVer. Spec §2 and §5 are updated in this PR.
- **Recommended usage** becomes: `var events = context.CaptureDomainEvents();` →
  `await context.SaveChangesAsync(ct);` → `await events.DispatchDomainEventsAsync(publish, ct);`.
- **Testing:** the limitation is pinned by a regression test proving the change-tracker overload drops
  a removed aggregate's event (both providers), and the fix is covered by tests showing the snapshot
  delivers it (both providers), preserves publish order, clears on success, and re-drives only
  undelivered work after a failure.
- **Known limitations:** the snapshot dispatches exactly the events captured at snapshot time; events
  raised after capture are not included (by design — capture is a point-in-time boundary around the
  save). Delivery is in-process only: a crash between the committed save and the dispatch loses the
  captured events (no durable outbox table). Consumers needing cross-crash durability still build a
  persisted outbox on top of `PeekDomainEvents` / `ClearDomainEvents`.

## References

- spec §2 (`DbContextExtensions`), §5 (public interface); ADR-0006; issue #21.
- `docs/patterns/design-patterns.md` — Data & Persistence: *Unit of Work* (already catalogued);
  this complements ADR-0006's outbox-style `PeekDomainEvents` / `ClearDomainEvents` primitives.
