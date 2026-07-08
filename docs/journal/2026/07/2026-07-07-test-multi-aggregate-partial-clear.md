# 2026-07-07 — Test: cover multi-aggregate partial-clear on dispatch (#57)

First from the zero-production-risk [test] tier. A pure test-coverage addition — no production code
change, so no CHANGELOG entry (nothing user-visible ships) and no ADR.

## Done

- **Gap:** the change-tracker `DispatchDomainEventsAsync` overload's multi-aggregate partial-clear
  guarantee — earlier aggregates cleared, the failing-and-later ones kept buffered for retry — was never
  asserted; all of its tests used a single aggregate (#57). (The snapshot overload already had a
  multi-aggregate retry test.)
- **Test:** `DispatchDomainEventsAsync_clears_earlier_aggregates_but_retains_the_failing_and_later_ones` —
  three aggregates, `publish` throws on the second dispatched, asserting **by counts** that exactly one
  aggregate is cleared and two retain their events. Count-based so it does not depend on the change
  tracker's aggregate ordering.
- **Meaningfulness verified by mutation:** temporarily clearing all aggregates up front (breaking
  partial-clear) makes the test fail; the fixed source makes it pass — so it is a real guard, not
  vacuous. `dotnet build -warnaserror` clean; `dotnet format` clean; 158 pass / 2 Redis skip.

## State

- v1.0.0 released. Test-only → no shipped change. PR drafted on
  `test/dispatch-multi-aggregate-partial-clear`, awaiting merge. CI still billing-blocked (jobs don't
  start; verified locally). No local Python → `consistency_lint` hand-verified; touches only a test file,
  this journal (+ index row), and the ROADMAP checkpoint pointer — no lint invariant affected.

## Next

- Continue the zero-risk [test]/[docs] tiers safest-first: e.g. #56 (PaginatedList boundary tests),
  #54 (ValueObject equality property tests generate only equal instances — strengthen the generators),
  #51/#52 (cache/validation concurrency coverage), or docs-accuracy items (#42/#45/#46/#94/#98). The
  remaining `[fix]` Mediums (#31/#33/#25/#26/#62/#71/#61) carry design decisions → ADR + a maintainer
  AskUserQuestion.
