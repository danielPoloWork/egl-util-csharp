# 2026-07-14 — Fix: variance-aware EventBroker dispatch (#71)

A `[fix]` (correctness) with a maintainer design decision, so it carries ADR-0027 (extending ADR-0007) and
a CHANGELOG `Fixed` entry.

## Done

- **Bug:** `IEventHandler<in TEvent>` is declared contravariant, but `EventBroker.DispatchAsync` resolved
  `IEnumerable<IEventHandler<runtimeType>>` from DI — and MS.DI applies no generic variance to
  `IEnumerable<T>` — so a handler registered for a base type, or a catch-all `IEventHandler<IDomainEvent>`,
  **silently never fired** for a derived event. The `in` modifier advertised consumption the broker did not
  deliver (#71).
- **Decision (maintainer via AskUserQuestion): make dispatch variance-aware** (over removing `in` or
  documenting the gap).
- **Change:** `EventDispatch.ForEventType` now enumerates the handler types an event is assignable to — its
  runtime type, each base class up to `object`, and every interface — most-specific first, cached per
  runtime type. `DispatchAsync` resolves `IEnumerable<IEventHandler<T>>` for each and invokes via a compiled
  `HandleAsync` delegate (casting the event to `T`); per-handler failure isolation and the single per-event
  DI scope are unchanged. Updated the `EventBroker`/`IEventHandler` XML docs; ADR-0027 extends ADR-0007.
- **Tests:** a `DogEvent : AnimalEvent, IAudited` published through the broker reaches handlers registered
  for its exact type, its base type (`AnimalEvent`), and its interface (`IAudited`) — but **not** a sibling
  (`CatEvent`) handler. The handler is an **open** generic `RecordingEventHandler<TEvent>`, which the
  `AddD4npUtil` scan skips (#65), so closed instances are registered manually and the scan-based end-to-end
  test is unperturbed. Existing exact-dispatch tests still pass (regression).
- **Verified:** build `-warnaserror` clean; `dotnet format` clean; EventBroker suite 15 pass; full suite
  green. Mutation-checked — see below.

## State

- v1.0.0 released. Production change in `EventBroker.cs` (+ `IEventHandler`/`EventBroker` doc); ADR-0027;
  CHANGELOG `Fixed`. **No public-API change**; **behavior change** (base-typed handlers now fire) flagged
  for the SemVer call (≥ MINOR — a previously-advertised capability now works). PR drafted on
  `fix/variance-aware-event-dispatch`, awaiting merge. CI still billing-blocked (verified locally). No local
  Python → `consistency_lint` hand-verified: ADR index gains ADR-0027, no version/lockfile change, README
  unaffected (no i18n).

## Next

- Last of the design-decision `[fix]` Mediums: **#25/#26** (Entity transient-identity / EF-proxy equality —
  property-tested; the riskiest, touching core equality). Then the audit backlog's substantive items are
  effectively drained.
