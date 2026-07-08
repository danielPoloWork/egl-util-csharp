# 2026-07-08 — Test: cover EventBroker post-shutdown rejection and drain (#55)

Sixth from the zero-production-risk [test] tier. Pure test-coverage — no production change, so no
CHANGELOG and no ADR.

## Done

- **Gap:** the documented post-shutdown behavior (`PublishAsync` throws `InvalidOperationException` once
  the broker has stopped) and the best-effort shutdown drain were untested (#55).
- **Tests:**
  - `PublishAsync_after_shutdown_throws` — after `StopAsync`, the closed channel surfaces as an
    `InvalidOperationException` and the rejected event is never delivered.
  - `StopAsync_drains_a_queued_backlog` — a `GatedRecordingHandler` holds the consumer on the first event
    so events 1..N form a real queued backlog; shutdown then begins, the gate is released, and `StopAsync`
    drains the whole backlog before returning. Asserts all N delivered.
- **Meaningfulness verified by mutation:** an early attempt (`ReadAllAsync(stoppingToken)`) did NOT cause
  loss — `WaitToReadAsync` still drains buffered items once the writer is completed, so the drain is
  robust to the read token (a useful finding). The *real* drain-loss regression is abandoning the drain on
  shutdown; a compiling mutation that `break`s the consume loop when `stoppingToken` is cancelled drops
  the backlog and fails the test, while the real source passes. `dotnet build -warnaserror` clean;
  `dotnet format` clean; 165 pass / 2 Redis skip.
- Deterministic, not timing-based: the gate guarantees the backlog and `StopAsync(None)` fully drains, so
  the assertion (all N delivered) holds regardless of scheduling. Drain-loss under a generic host's
  `HostOptions.ShutdownTimeout` is a host-integration behavior, noted but not unit-tested (it is inherently
  racy).

## State

- v1.0.0 released. Test-only → no shipped change. PR drafted on `test/eventbroker-shutdown-behavior`,
  awaiting merge. CI still billing-blocked (jobs don't start; verified locally). No local Python →
  `consistency_lint` hand-verified; touches only a test file, this journal (+ index row), and the ROADMAP
  checkpoint pointer — no lint invariant affected.

## Next

- Continue zero-risk [test]/[docs] safest-first: #102 (zero-arg `AddD4npUtil` default-scan coverage), #99
  (broker per-event DI-scope / runtime-type dispatch end-to-end), #101 (cache TTL/expiration coverage),
  #100 (Redis-skip silent-vanish), #51/#52 (cache/validation concurrency — flakier), or docs-accuracy
  (#42/#45/#46/#94/#98). The remaining `[fix]` Mediums (#31/#33/#25/#26/#62/#71/#61) carry design decisions
  → ADR + a maintainer AskUserQuestion.
