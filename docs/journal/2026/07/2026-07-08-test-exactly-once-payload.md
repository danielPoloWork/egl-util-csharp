# 2026-07-08 — Test: verify exactly-once by payload + FIFO order (#103)

Fifth from the zero-production-risk [test] tier. Pure test-strengthening — no production change, so no
CHANGELOG and no ADR.

## Done

- **Gap:** `Delivers_every_event_exactly_once_under_concurrent_publishers` built unique `Pinged(Sequence)`
  events but the handler only counted; the sole assertion was `Count == publishers * perPublisher`. A
  broker defect that duplicates one event and drops another (net count unchanged) passed, so the test did
  not actually establish "exactly once." The documented single-consumer FIFO order was untested too (#103).
- **Tests:** added a thread-safe `RecordingHandler` (a `ConcurrentQueue<int>` of delivered `Sequence`s in
  delivery order). Strengthened the concurrent test to assert
  `Enumerable.Range(0, total) == handler.Sequences.OrderBy(s => s)` — a single sorted-sequence comparison
  that proves no loss **and** no duplicate (a dup+drop shifts the sorted sequence). Added
  `Delivers_events_from_a_single_publisher_in_publish_order` asserting the recorded sequence is exactly the
  publish order `0..count-1` (the documented FIFO contract). Generalized `BuildProvider` to accept any
  `IEventHandler<Pinged>`.
- **Meaningfulness verified by mutation:** a *compiling* mutation recording sequence 0 as 1 (duplicate 1 /
  drop 0, net count unchanged — the exact defect the old test missed) fails the strengthened exactly-once
  test; the real handler passes. Both assertions are order-robust (sorted / single-publisher), so no
  flakiness (the broker drains on `StopAsync` before assertion). `dotnet build -warnaserror` clean;
  `dotnet format` clean; 163 pass / 2 Redis skip.

## State

- v1.0.0 released. Test-only → no shipped change. PR drafted on `test/eventbroker-exactly-once-payload`,
  awaiting merge. CI still billing-blocked (jobs don't start; verified locally). No local Python →
  `consistency_lint` hand-verified; touches only a test file, this journal (+ index row), and the ROADMAP
  checkpoint pointer — no lint invariant affected.

## Next

- Continue zero-risk [test]/[docs] safest-first: #55 (EventBroker post-shutdown rejection / drain-loss),
  #102 (zero-arg `AddD4npUtil` default-scan coverage), #99–#101 (broker DI-scope / redis-skip / TTL
  coverage), #51/#52 (cache/validation concurrency — flakier), or docs-accuracy (#42/#45/#46/#94/#98). The
  remaining `[fix]` Mediums (#31/#33/#25/#26/#62/#71/#61) carry design decisions → ADR + a maintainer
  AskUserQuestion.

## Process note

Caught (and fixed) a stale-base slip: after `git fetch` I branched before fast-forwarding local `main`, so
the branch initially missed the just-merged #104. Fixed by `git checkout main && git pull --ff-only` then
recreating the branch. Lesson: **`git checkout main && git pull` (not just `fetch`) before `checkout -b`.**
