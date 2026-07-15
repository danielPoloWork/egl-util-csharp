# ADR-0039: Add opt-in per-key coalescing to CacheService (amends ADR-0009)

- **Status:** Accepted
- **Date:** 2026-07-15
- **Deciders:** Maintainer
- **Related:** ADR-0009 (two-level cache), ADR-0024 (L2-outage contract), #50

## Context

`CacheService` does not serialise concurrent misses for the same key: each concurrent `GetOrCreateAsync`
that misses runs the factory and writes both levels (last write wins). ADR-0009 accepted this as a deliberate
trade-off — the cache is lock-free on the read path. For an **expensive** factory under contention this is the
classic cache-stampede / thundering-herd a two-level cache is often meant to prevent (#50, Info-severity): a
burst of concurrent requests for a cold key each recompute the value. The maintainer wants coalescing
available **without** paying for it (or changing behaviour) when it is not needed.

## Decision

Add **opt-in** per-key coalescing, off by default:

- A new `CacheService` constructor overload takes `bool coalesceConcurrentMisses`; the existing
  two-argument constructor delegates with `false`, so the default behaviour and surface are unchanged. The
  `ICacheService` abstraction and the `GetOrCreateAsync` signature are untouched — the opt-in is purely a
  construction-time choice (additive; no breaking change).
- When enabled, a miss (after the lock-free L1 check) is resolved behind a **per-key gate**: the caller
  acquires a per-key `SemaphoreSlim`, then re-checks L1 (the winner populates it) before the shared miss path
  re-checks L2 and runs the factory. So concurrent callers for the same key serialise, and the factory runs
  **at most once per key per node** — the others return the winner's value. The gate is **ref-counted** and
  removed (its semaphore disposed) when its last waiter leaves, so an unbounded key space does not leak
  semaphores. Cancellation while waiting on the gate propagates (the caller's token is honoured).
- The gate is **per-instance and per-node**: it does not coordinate across processes, so a distributed
  stampede (many nodes, cold key) is still possible — consistent with the lock-free, no-invalidation-bus
  design of the two-level cache (ADR-0009, and the cross-node caveats in #42).
- Consumers opt in by registering `CacheService` with the flag before `AddD4npUtil` (the documented
  "override a default" path), e.g.
  `services.AddSingleton<ICacheService>(sp => new CacheService(mem, dist, coalesceConcurrentMisses: true))`.

## Alternatives Considered

- **Always-on coalescing.** Rejected — it changes ADR-0009's documented default and adds a per-key lock to
  every miss (even uncontended), for a behaviour many callers do not need. Opt-in keeps the default lock-free.
- **A `Lazy<Task<T>>` (or `Task<T>`) registry keyed by cache key.** Rejected — `GetOrCreateAsync<T>` is generic
  per call, so a single key can be requested at different `T`s; a strongly-typed task registry is awkward and
  a boxed/object-typed one is error-prone. A per-key `SemaphoreSlim` is type-agnostic and simpler to reason
  about (the value is fetched inside the gate through the normal typed path).
- **Decline / keep as documented trade-off.** A legitimate outcome for an Info item, but the maintainer chose
  to offer the opt-in.

## Consequences

- Opt-in, so **no behaviour or performance change unless enabled**; ADR-0009's lock-free default stands. When
  enabled, concurrent misses for a hot-cold key coalesce to one factory run per node, at the cost of a per-key
  lock + semaphore for the duration of the contended miss (removed afterwards). Amends ADR-0009's stampede
  trade-off from "always accepted" to "accepted by default, avoidable per instance."
- Additive public surface (a new `CacheService` constructor overload); the `ICacheService`/`GetOrCreateAsync`
  contract is unchanged. Accrues toward the next **MINOR**.
- Cross-process coalescing is explicitly **not** provided (per-node only), documented on the constructor.

## References

- Issue #50; ADR-0009, ADR-0024; #42.
- `src/main/csharp/it/d4np/util/Infrastructure/CacheService.cs` (the `coalesceConcurrentMisses` overload,
  `ResolveOnMissAsync`, and the ref-counted `KeyGate`);
  `src/test/csharp/it/d4np/util/CacheServiceCoalescingTests.cs`.
