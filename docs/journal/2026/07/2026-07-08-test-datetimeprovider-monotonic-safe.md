# 2026-07-08 — Test: make the DateTimeProvider test monotonic-safe (#104)

Fourth from the zero-production-risk [test] tier — a test-flakiness fix (removes a rare CI red). No
production change, so no CHANGELOG and no ADR.

## Done

- **Gap:** `DateTimeProviderTests` bracketed `provider.UtcNow` with `Assert.InRange(value, before, after)`
  over three consecutive `DateTime.UtcNow` reads. The wall clock is **not monotonic**: an NTP step/slew
  or manual adjustment landing between the reads makes `after < value` (or `value < before`) and fails
  the test spuriously — a rare, unreproducible red on long-lived CI agents with aggressive time sync
  (#104).
- **Fix:** replaced the strict range with a **tolerance-based** assertion — `Kind == Utc` plus
  `(value - systemNow).Duration() < TimeSpan.FromMinutes(1)`. One minute is orders of magnitude larger
  than the sub-millisecond gap between two consecutive reads (so no realistic clock movement flakes it),
  yet far smaller than a wrong-source bug (local time, epoch, or a constant), so it stays a real check.
  Renamed the test to `UtcNow_returns_the_current_utc_time`.
- **Meaningfulness verified by mutation:** a *compiling* mutation making `DateTimeProvider.UtcNow` return
  `DateTime.UtcNow.AddMinutes(5)` fails the test; the correct source passes. `dotnet build -warnaserror`
  clean; `dotnet format` clean; DateTimeProvider test green (full suite unaffected).

## State

- v1.0.0 released. Test-only → no shipped change. PR drafted on `test/datetimeprovider-monotonic-safe`,
  awaiting merge. CI still billing-blocked (jobs don't start; verified locally) — this fix improves CI
  reliability once billing is cleared. No local Python → `consistency_lint` hand-verified; touches only a
  test file, this journal (+ index row), and the ROADMAP checkpoint pointer — no lint invariant affected.

## Next

- Continue zero-risk [test]/[docs] safest-first: #103 (strengthen the exactly-once broker test to check
  the Sequence payload, not just the count), #55 (EventBroker post-shutdown rejection), #102 (zero-arg
  AddD4npUtil default-scan coverage), #51/#52 (cache/validation concurrency — flakier), or docs-accuracy
  (#42/#45/#46/#94/#98). The remaining `[fix]` Mediums (#31/#33/#25/#26/#62/#71/#61) carry design
  decisions → ADR + a maintainer AskUserQuestion.
