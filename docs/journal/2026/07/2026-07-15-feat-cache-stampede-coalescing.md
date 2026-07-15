# 2026-07-15 — Feat: opt-in per-key cache-stampede coalescing (#50)

Adds optional per-key coalescing to `CacheService`, off by default. Carries ADR-0039 (amends ADR-0009) and a
CHANGELOG `Added` entry (additive, next MINOR). **The last open item of the enterprise-audit backlog.**

## Done

- **Enhancement (#50, Info):** `CacheService` did not serialise concurrent misses for the same key — each
  concurrent `GetOrCreateAsync` miss ran the factory (last write wins), the lock-free trade-off ADR-0009
  documented. For an expensive factory under contention this is the classic cache stampede.
- **Decision (maintainer via AskUserQuestion):** OPT-IN coalescing, default OFF (over always-on, or declining
  as by-design). Recorded in ADR-0039 (amends ADR-0009).
- **Change:** new `CacheService(memoryCache, distributedCache, coalesceConcurrentMisses)` overload (the
  2-arg ctor delegates with `false`, so default behaviour/surface unchanged; `ICacheService`/`GetOrCreateAsync`
  untouched — additive). Extracted the L2-check + factory + write into a shared `ResolveOnMissAsync`; when
  coalescing is on, the miss runs behind a per-key gate (acquire a per-key `SemaphoreSlim`, re-check L1, then
  `ResolveOnMissAsync` re-checks L2) so the factory runs at most once per key per node. The gate is a
  **ref-counted** `KeyGate` (guarded by locking `_gates`), removed + disposed when its last waiter leaves, so
  an unbounded key space does not leak semaphores; `KeyGate : IDisposable` (satisfies CA1001). Per-instance,
  per-node (no cross-process coordination — documented). Cancellation while waiting propagates.
- **Considered + rejected** (ADR-0039): always-on (changes the default + per-miss lock overhead);
  `Lazy<Task<T>>` registry (awkward with the generic per-call `T`); decline.
- **Tests:** `CacheServiceCoalescingTests` — gated-factory concurrency, ON ⇒ factory runs **once** under 8
  concurrent misses; OFF ⇒ runs **8×** (the built-in contrast that proves the flag's effect / non-vacuity);
  plus a sanity test (on: value served + cached, factory once). Deterministic (correctness is structural, not
  timing — waiters can only enter the factory after acquiring the gate the winner holds until it has written).
- **Verified:** `dotnet build -warnaserror` clean (0 warnings; the disposable gate needed `IDisposable` for
  CA1001); `dotnet format --verify-no-changes` clean; full suite **280 pass / 3 skip** (+3 vs the 277 baseline).

## State

- v1.0.0 released; #38/#78/#79/#87/#93/#89 merged (PRs #172–#175). **Additive** (opt-in ctor overload); default
  behaviour unchanged; accrues toward the next MINOR; no version bump. ADR-0039 (amends ADR-0009); CHANGELOG
  `Added`. No README/i18n change (a ctor option, not surfaced in the README blurb). PR drafted on
  `feat/cache-stampede-coalescing` (`perf` label, Maintenance milestone), awaiting merge. CI billing-blocked →
  verified locally. No local Python → `consistency_lint` hand-traced: ADR index gains 0039, ADR-0009 amendment
  note updated, patterns/spec-map/version/lockfile/i18n unaffected.

## Next

- **Backlog drained.** Every enterprise-audit finding (rounds 1 + 2, #19–#104) is now resolved or a merged PR.
  The only outstanding work is the accrued **2.0** breaking changes — ADR-0020 (implicit Error→Result),
  ADR-0027 (variance), ADR-0028 (canonical Entity equality), ADR-0030 (const→property version), ADR-0037
  (MediatR package split) — which the maintainer assembles into the 2.0 release when ready (also enabling
  `EnablePackageValidation` with a 2.0.0 baseline, #64). No further per-issue PRs remain.
