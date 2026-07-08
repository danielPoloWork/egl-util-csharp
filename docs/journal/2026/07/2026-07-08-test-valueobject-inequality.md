# 2026-07-08 — Test: strengthen ValueObject equality property tests (#54)

Third from the zero-production-risk [test] tier. Pure test-coverage — the `ValueObject` equality
implementation is already correct; only the tests were weak. No production change, so no CHANGELOG and no
ADR.

## Done

- **Gap:** the `ValueObject` property tests only ever built *equal* `Money` instances (same amount and
  currency for x/y/z), so symmetry/transitivity/hash held **vacuously** and could not catch a
  component-ignoring `Equals` (e.g. a `GetEqualityComponents` that drops a field) (#54).
- **Tests:** added `ValueObject_differing_in_a_single_component_is_unequal` — from a baseline it
  constructs one instance that differs **only** in `amount` and one that differs **only** in `currency`
  (deterministically, not relying on random collisions) and asserts inequality (`Equals`, `==`, `!=`).
  Rewrote `ValueObject_equality_is_symmetric` to use two independent component sets (usually unequal, so
  symmetry is exercised, not vacuous). Expanded the hash test to
  `ValueObject_equal_instances_are_equal_and_share_a_hash_code` (equality + `==` + hash). Reflexive and
  transitive kept.
- **Meaningfulness verified by mutation:** making `Money.GetEqualityComponents` omit `currency` (a
  *compiling* mutation — the param is still referenced) makes the single-component test fail; the correct
  source passes. (First mutation attempt gave a false pass: removing the yield left `currency` an unused
  primary-ctor param → `-warnaserror` build error → stale test binary. Lesson: a mutation must still
  compile clean, or the `--no-build` run silently uses the old binary.) `dotnet build -warnaserror` clean;
  `dotnet format` clean; 162 pass / 2 Redis skip.

## State

- v1.0.0 released. Test-only → no shipped change. PR drafted on `test/valueobject-inequality-properties`,
  awaiting merge. CI still billing-blocked (jobs don't start; verified locally). No local Python →
  `consistency_lint` hand-verified; touches only a test file, this journal (+ index row), and the ROADMAP
  checkpoint pointer — no lint invariant affected.

## Next

- Continue zero-risk [test]/[docs] safest-first: #51/#52 (cache/validation concurrency coverage), #55
  (EventBroker post-shutdown rejection / drain-loss), #99–#104 (DI/redis/dateprovider coverage), or
  docs-accuracy (#42/#45/#46/#94/#98). The remaining `[fix]` Mediums (#31/#33/#25/#26/#62/#71/#61) carry
  design decisions → ADR + a maintainer AskUserQuestion. NOTE: #53 is not pure test (Combine NREs on a
  null element → needs a code decision).
