# 2026-07-06 — Fix: domain events dropped on removed aggregates (#21)

First fix from the post-1.0 audit backlog. High-severity correctness bug: domain events raised on an
aggregate that is then removed in the same unit of work were silently dropped.

## Done

- **Root cause:** `DbContextExtensions.DispatchDomainEventsAsync(this DbContext, …)` re-scans
  `ChangeTracker.Entries<IHasDomainEvents>()` after the save, but EF Core detaches `Deleted` entities
  from the change tracker once `SaveChanges` completes — so a removed aggregate's events are gone by
  the time dispatch runs. Verified to reproduce on both the InMemory and SQLite providers.
- **Fix (additive, minor bump):** added `CaptureDomainEvents(this DbContext) → DomainEventSnapshot`
  (a point-in-time capture taken *before* the save, holding strong aggregate references + event copies)
  and `DispatchDomainEventsAsync(this DomainEventSnapshot, …)` (publishes after the commit, clears each
  source aggregate on success, re-drives only undelivered aggregates on a retry). Delivery stays
  in-process/at-least-once — not a durable outbox table (ADR-0012 spells out the limits).
- The existing change-tracker overload is retained for backward compatibility; its XML docs now state
  the removed-aggregate limitation loudly and point to the capture flow.
- Tests: pinned the limitation (change-tracker overload drops a removed aggregate's event — both
  providers) and covered the fix (snapshot delivers it — both providers), plus publish order,
  clear-on-success, and retry-re-drives-only-undelivered. `dotnet build -warnaserror` clean; 133 pass,
  2 Redis integration tests skipped (no local Docker).
- Docs synced: ADR-0012 (amends ADR-0006's dispatch guidance), spec §2/§5, CHANGELOG `[Unreleased]`.

## State

- v1.0.0 released; this is the first SemVer-maintenance fix on top of it. Additive public surface
  (`DomainEventSnapshot` + two extension methods) → a future release cuts as **1.1.0**. Version files
  unchanged (bumped at release time). PR drafted on `fix/domain-events-dropped-on-delete`, awaiting
  maintainer merge.

## Next

- After merge, continue the audit backlog one PR at a time (order: safest-first). Next candidate: #20
  (`CacheService` breaks against a size-limited `IMemoryCache`).
