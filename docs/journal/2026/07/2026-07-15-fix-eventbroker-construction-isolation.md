# 2026-07-15 — Fix: isolate event-handler construction failures in the broker (#89)

Makes the broker's documented isolation guarantee cover handler **construction**, not only execution. Carries
ADR-0038 and a CHANGELOG `Fixed` entry (behavior change, ≥ MINOR).

## Done

- **Bug:** `EventBroker`'s "one faulty handler never stops delivery" only wrapped `HandleAsync`. Dispatch
  resolves `IEnumerable<IEventHandler<T>>` from the per-event scope, and MS.DI materializes it **eagerly** —
  **empirically confirmed** with a throwaway probe: a single throwing constructor makes the `GetService` call
  itself throw before any element is yielded. So one handler's bad ctor/DI graph aborted the whole group's
  resolution and the event was dropped for every handler of that type, healthy siblings included (#89).
- **Decision (maintainer via AskUserQuestion):** FIX construction isolation (over document-only). Chosen
  implementation = **dual-path**, to guarantee no regression: keep the eager resolution on the happy path (all
  registered handlers still dispatch, correct DI lifetimes) and, only when it throws, fall back to constructing
  each **scanned** handler individually (isolating the faulty ctor). Recorded in ADR-0038.
- **Change:** new internal `EventHandlerRegistry` (per-app map of closed `IEventHandler<T>` → concrete impl
  types) populated by `AddD4npUtil`'s scan and registered as a singleton. `EventBroker` resolves it from the
  scope (no ctor-signature change), and on a construction failure rebuilds each scanned handler via
  `ActivatorUtilities.CreateInstance` under its own try/catch, invoking the healthy ones and isolating the
  broken one; the failure is reported through the `It.D4np.Util.EventBroker` `EventSource` + logger. `HandlerGroup`
  gained the closed `IEventHandler<T>` type (the registry key). Broker remarks updated.
- **Considered + rejected** (in ADR-0038): document-only; single-path registry-only dispatch (would drop
  handlers registered outside the scan on the happy path); per-descriptor container introspection (reimplements
  the container, mis-handles non-transient lifetimes/factories).
- **Test:** `EventBrokerConstructionIsolationTests` — two handlers for one event via the `AddD4npUtil` scan,
  one with a throwing ctor; the healthy sibling receives both published events. **Mutation-verified**: with the
  fallback removed (pre-fix behavior) the test fails `Expected 2, Actual 0` (event dropped for both); with the
  fix it passes.
- **Verified:** `dotnet build -warnaserror` clean (0 warnings); `dotnet format --verify-no-changes` clean; full
  suite **277 pass / 3 skip** (Redis, no Docker; +1 vs the 276 baseline).

## State

- v1.0.0 released; #38/#78/#79/#87/#93 merged (PRs #172–#174). **Behavior change** (healthy siblings now
  delivered on a construction failure) → ≥ MINOR; no public-API change (internal registry; broker ctor
  unchanged); no version bump. ADR-0038; CHANGELOG `Fixed`. No README/i18n change (broker internals, not
  surfaced in the README). PR drafted on `fix/eventbroker-construction-isolation` (`fix` label, Maintenance
  milestone), awaiting merge. CI billing-blocked → verified locally. No local Python → `consistency_lint`
  hand-traced: ADR index gains 0038, patterns unaffected, spec-map/version/lockfile/i18n unaffected.
- Process note: caught the "stale local main" trap — the branch was first cut from local `main` (pre-#174, the
  file-state reminders showed AggregateRoot/ICacheService without the #87/#93 remarks); recreated it from the
  pulled `origin/main` (8b4b9e9). Always `git pull` main before `checkout -b`.

## Next

- **#50** (opt-in cache-stampede coalescing — an Info enhancement, addable without an API change) is the last
  open backlog item. After it, only the accrued 2.0 breaks (ADR-0020/0027/0028/0030/0037) remain for the
  maintainer-assembled 2.0 cut.
