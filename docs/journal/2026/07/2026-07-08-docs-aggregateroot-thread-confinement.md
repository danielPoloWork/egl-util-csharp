# 2026-07-08 — Docs: AggregateRoot thread-confinement contract (#46)

Twelfth from the zero-production-risk tier; a docs-accuracy clarification. No code/behavior change, so
no CHANGELOG and no ADR.

## Done

- **Gap:** `AggregateRoot<TId>` buffers domain events in a plain, unsynchronized `List<IDomainEvent>`.
  `RaiseDomainEvent`, `DomainEvents`, and `ClearDomainEvents` all read/mutate it without locking. This is
  idiomatic (one unit of work owns an aggregate), but no XML doc stated the single-thread / UoW
  confinement, so a reader could assume it was safe to mutate one aggregate from several threads (#46).
- **Fix:** added a `<b>Thread-safety.</b>` `<remarks>` to `AggregateRoot<TId>` spelling out the contract —
  the buffer is unsynchronized; the aggregate is confined to a single unit of work (typically the thread
  owning that UoW's EF Core `DbContext`, itself not thread-safe), so the domain code raising events and the
  infrastructure boundary enumerating/clearing them never touch the buffer concurrently; mutating from
  multiple threads is an unguarded data race. Added a shorter cross-referencing note to `IHasDomainEvents`
  (the surface the dispatcher programs against). Matches the house `<b>Threading.</b>`-lead-in style of the
  `EventBroker` / `DomainEventSnapshot` docs. Verified against the real dispatch path: the
  drain-to-fixed-point boundary (#30/ADR-0022) runs handler-raised events synchronously on the dispatch
  flow, and the usual `publish` (EventBroker) only enqueues — so no cross-thread buffer access exists to
  contradict the contract. `dotnet build -warnaserror` clean (all crefs resolve); `dotnet format` clean.
- **Review catch:** a 3-lens adversarial pass (accuracy / consistency / hostile-skeptic) flagged that my
  first draft's clause *"raising an event while a boundary is enumerating or clearing the buffer … is a
  data race"* dropped the cross-thread qualifier and so read, literally, as forbidding the **supported**
  re-entrant raise a handler makes during drain-to-fixed-point dispatch (ADR-0022) — exactly the reader
  that contract targets. Fixed: the hazard now explicitly requires **another thread** ("one thread raise …
  while another concurrently enumerates or clears"), plus an affirmative carve-out that a same-flow
  re-entrant raise is not concurrent access and is safe. Also aligned the lead-in to the house
  `<b>Threading.</b>` style (was `Thread-safety.`) and stopped asserting "the buffer" as an
  `IHasDomainEvents` interface-level detail (it is an `AggregateRoot<TId>` implementation detail).

## State

- v1.0.0 released. Docs-only (two XML `<remarks>`); no behavior/API change. PR drafted on
  `docs/aggregateroot-thread-confinement`, awaiting merge. Wording adversarially reviewed (3 lenses:
  accuracy / consistency / hostile-skeptic) before drafting. CI still billing-blocked (verified locally).
  No local Python → `consistency_lint` hand-verified; touches only `AggregateRoot.cs` + `IHasDomainEvents.cs`
  (doc comments), this journal (+ index row), and the ROADMAP pointer — no lint invariant affected.

## Next

- Continue docs-accuracy safest-first: #42 (cross-node L1 staleness after RemoveAsync), #98
  (IDomainEvent.OccurredOnUtc DateTimeKind is contract-only), #94 (SECURITY.md supported-versions table
  stale post-1.0 — a small policy call). Then #51/#52 (cache/validation concurrency). The remaining `[fix]`
  Mediums (#31/#33/#25/#26/#62/#71/#61) carry design decisions → ADR + a maintainer AskUserQuestion.
