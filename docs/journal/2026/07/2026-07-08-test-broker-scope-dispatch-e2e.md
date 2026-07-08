# 2026-07-08 — Test: broker per-event scope, runtime-type dispatch, end-to-end (#99)

Eighth from the zero-production-risk [test] tier (Medium). Pure test-coverage — no production change, so
no CHANGELOG and no ADR.

## Done

- **Gap:** `EventBroker` documents handlers resolved from a **fresh per-event DI scope** by the event's
  **runtime** type, and `AddD4npUtil` registers scanned handlers as **transient** — yet every prior broker
  test used pre-built **singleton** handlers, so scope-per-event lifetime, runtime-type dispatch, the
  no-handler path, and end-to-end delivery through the shipped wiring were all untested (#99).
- **Tests (4):**
  - `Resolves_a_fresh_scope_per_event_and_disposes_it` — a scoped handler depends on a scoped `IDisposable`
    recording create/dispose on a singleton tracker; two events yield two distinct dependency instances,
    each disposed when its per-event scope closes (Created==2, distinct==2, Disposed==2).
  - `Dispatches_by_the_events_runtime_type_not_its_static_type` — publishes `object baseTyped = new
    Pinged(1)` (so `PublishAsync<object>`), asserting the `Pinged` handler runs (runtime-type dispatch).
  - `An_event_with_no_registered_handler_does_not_stop_later_delivery` — an `Unhandled` event is a no-op
    (empty handler set); a later `Pinged` still delivers.
  - `End_to_end_delivers_to_a_scanned_transient_handler_wired_by_AddD4npUtil` — `AddD4npUtil(assembly)` →
    resolve `EventBroker` → Start → `PublishAsync(new E2E(42))` → the scanned **transient** `E2EHandler`
    (ctor resolves a singleton signal) observes it → Stop.
- **Determinism:** tests 1–3 synchronize via `StopAsync` (drains + disposes per-event scopes before the
  assertion — no sleeps); test 4 awaits a `TaskCompletionSource` (30s timeout bound). The e2e scan runs on
  its own `ServiceCollection`; other handlers with un-resolvable ctors (e.g. `GatedRecordingHandler`) are
  registered but never constructed because only `IEventHandler<E2E>` is resolved (no `ValidateOnBuild`).
- **Verification:** `dotnet build -warnaserror` clean; `dotnet format` clean; 170 pass / 2 Redis skip.
  Verified by a 2-lens adversarial review (meaningfulness/behavior + flakiness/e2e-scan): both clean; the
  one nit — a redundant `Created.Distinct()` assertion — was removed. (Skipped
  per-assertion mutation for the disposal path: removing `await using` trips CA2000 → build error → stale
  binary, per the #54 lesson; the adversarial review covers meaningfulness instead.)

## State

- v1.0.0 released. Test-only → no shipped change. PR drafted on `test/eventbroker-scope-and-dispatch`,
  awaiting merge. CI still billing-blocked (jobs don't start; verified locally). No local Python →
  `consistency_lint` hand-verified; touches only a test file, this journal (+ index row), and the ROADMAP
  checkpoint pointer — no lint invariant affected.

## Next

- Continue zero-risk [test]/[docs] safest-first: #101 (cache TTL/expiration coverage), #100 (Redis-skip
  silent-vanish), #51/#52 (cache/validation concurrency — flakier), or docs-accuracy (#42/#45/#46/#94/#98).
  The remaining `[fix]` Mediums (#31/#33/#25/#26/#62/#71/#61) carry design decisions → ADR + a maintainer
  AskUserQuestion.
