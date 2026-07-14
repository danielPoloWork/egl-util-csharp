# 2026-07-14 — Fix: RemoveAsync removes L2 before L1 (#91)

Roadmap PR 3 (correctness cluster). A cache-concurrency fix; no API change; CHANGELOG `Fixed`, no new ADR
(refines the order documented by ADR-0024).

## Done

- **Bug:** `CacheService.RemoveAsync` cleared L1 synchronously, then awaited the L2 delete. During that L2
  round-trip a concurrent same-node `GetOrCreateAsync` missed the just-cleared L1, hit the still-present L2,
  and re-promoted the removed value into L1 — the removing node then served the invalidated value from L1
  until its TTL (indefinitely with `ttl = null`). Same-node, purely from the operation order (distinct from
  #42's unavoidable cross-node staleness) (#91).
- **Fix:** swap to L2-first, L1-last (standard cache-aside order). Removing L1 last means a concurrent read
  hits L1 for the whole L2 round-trip and never re-reads L2, so it cannot re-promote — the window shrinks
  from a full round-trip to the gap between the two calls. A narrow residual remains (a read that had
  independently missed L1 could finish its promotion after the L1 removal) — the same lock-free two-level
  limitation as #42, documented.
- **Interaction with #62/ADR-0024:** the swap inverts the RemoveAsync-fault behavior — an L2 delete fault
  now propagates **before** L1 is touched, so **both** levels retain the entry (was: L1 cleared, L2 stale).
  Updated the `CacheService` "L2 outages" remarks, ADR-0024's reads/removes clause, and the #62 fault test
  (renamed `..._before_clearing_L1`, asserting L1 is untouched).
- **Verified:** build `-warnaserror` clean; `dotnet format` clean; CacheService suite 30 pass; full suite
  green. Mutation-checked — see below.

## State

- v1.0.0 released. Cache-concurrency fix in `CacheService.cs` (+ remarks, ADR-0024 text, #62 test);
  CHANGELOG `Fixed`; no API change (PATCH), no new ADR. PR drafted on `fix/removeasync-l2-before-l1`,
  awaiting merge. CI billing-blocked (verified locally). No local Python → `consistency_lint` hand-verified
  (adr-index unchanged — no new ADR). README untouched.

## Next

- Roadmap PR 4: **#49** — trim/AOT `[RequiresDynamicCode]`/`[RequiresUnreferencedCode]` on the reflective
  public entry points (`EventBroker` dispatch, `ServiceCollectionExtensions` scan). Additive annotations,
  no behavior change, no design fork → proceed without a question.
