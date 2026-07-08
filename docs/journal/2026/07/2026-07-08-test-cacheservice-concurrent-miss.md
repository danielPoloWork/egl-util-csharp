# 2026-07-08 — Test: CacheService concurrent-miss last-write-wins coverage (#51)

Zero-production-risk [test] tier; test-only, so no CHANGELOG and no ADR.

## Done

- **Gap:** `CacheService`'s documented concurrent-miss contract — "does not serialise concurrent misses
  for the same key, so the factory may run more than once per key (last write wins)" — was only ever
  exercised single-threaded, so the very hazard the remarks call out was unverified (#51).
- **Test:** `GetOrCreateAsync_under_concurrent_misses_runs_the_factory_per_call_and_converges_to_one_value`
  fires N=16 `GetOrCreateAsync` calls for one key. A `TaskCompletionSource` barrier (the Nth entrant
  releases all) forces every call **past the L1/L2 miss check before any writes**, so the hazard is
  exercised **deterministically** — no wall-clock race. Asserts: the factory ran once per call
  (`calls == n`, i.e. not serialised), each caller received its own distinct value (`{1..n}`), and the
  storm converged — later reads hit L1 (a throwing factory proves it is not re-invoked) and return one
  coherent value, stably.
- **Meaningful (mutation intuition):** if `CacheService` were changed to serialise misses (run the
  factory once), `calls` would be 1, not 16 → the test fails. Not vacuous.
- `dotnet build -warnaserror` clean; `dotnet format` clean; **173 pass / 3 Redis skip** (was 172; +1).

## State

- v1.0.0 released. Test-only (`CacheServiceTests.cs`); no production change. PR drafted on
  `test/cacheservice-concurrent-miss`, awaiting merge. CI still billing-blocked (verified locally). No
  local Python → `consistency_lint` hand-verified; touches one test file, this journal (+ index row), and
  the ROADMAP pointer — no lint invariant affected.

## Next

- **#52 is NOT a mere test gap — it is a live bug.** Writing the sibling test uncovered a real data race:
  `ValidationPipelineBehavior` runs every validator via `Task.WhenAll` over ONE
  `ValidationContext<TRequest>`, and FluentValidation's context is not concurrency-safe, so
  genuinely-overlapping async validators corrupt each other's failures (proven: shared context fails
  nondeterministically; per-validator context passes 3/3). Escalated to the maintainer for the fix
  approach (fresh context per validator [preferred] vs sequential validation) — becomes a `[fix]` PR with
  the regression test + likely CHANGELOG. Then the design-decision `[fix]` Mediums (#31/#33/#25/#26/#62/#71/#61).
