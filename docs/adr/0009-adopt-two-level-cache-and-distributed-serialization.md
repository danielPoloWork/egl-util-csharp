# ADR-0009: Adopt a two-level cache and compact distributed serialization

- **Status:** Accepted
- **Amended in part by:** [ADR-0021](0021-write-null-properties-in-cache-serialization.md) — the `WhenWritingNull` serialization detail below is dropped (null properties are now written); and [ADR-0023](0023-reject-non-round-trippable-cache-types.md) — declared-type serialization now fails fast on a non-round-trippable `T` (`object`/interface/abstract, or a runtime subtype) instead of silently storing a lossy value. The rest of this ADR stands.
- **Date:** 2026-07-01
- **Deciders:** Maintainer
- **Related:** ADR-0003, ADR-0006, ADR-0007, ADR-0008, spec §2 (requirements 21–22), §3, §4

## Context

Spec §2 (21–22) calls for the caching slice: `ICacheService` — a two-level (in-memory + distributed)
cache with `GetOrCreateAsync`, TTL, and key-based invalidation — and `DistributedCacheExtensions` —
typed get/set over `IDistributedCache` using compact `System.Text.Json`. Spec §6 requires the Redis
layer to be integration-tested via Testcontainers. The building blocks must fit the layered
architecture (ADR-0003): abstraction in Application, technology in Infrastructure, depending only on
the `Microsoft.Extensions` abstractions where possible.

## Decision

- **Abstraction in Application.** `ICacheService` exposes `GetOrCreateAsync<T>(key, factory, ttl?, ct)`
  and `RemoveAsync(key, ct)` — the minimal surface the spec names (read-through create + TTL +
  invalidation).
- **Two-level implementation in Infrastructure.** `CacheService` composes an L1 `IMemoryCache` in front
  of an L2 `IDistributedCache`. A read checks L1 first (`TryGetValue`, an unambiguous `bool`), then L2;
  an L2 hit is promoted into L1; a miss invokes the factory and writes **L2 then L1**. **L2 presence is
  decided from the stored bytes, not the deserialised value**, so a stored `default(T)` (e.g. `0`) is
  never mistaken for a miss. A `null` factory result is returned but not cached. TTL is applied as
  `AbsoluteExpirationRelativeToNow` at both levels. `CacheService` depends only on
  `Microsoft.Extensions.Caching.Abstractions`. It realises the **Proxy** pattern (a caching proxy over
  the factory and the distributed store, implementing the cache-aside / read-through strategy).
- **Compact serialization.** `DistributedCacheExtensions.GetAsync<T>` / `SetAsync<T>` serialise as
  compact UTF-8 `System.Text.Json` (Web defaults, no indentation, `WhenWritingNull`). An internal
  `CacheSerializer` holds the single `JsonSerializerOptions` shared by both the extensions and
  `CacheService`, so a value written by one is readable by the other. `GetAsync` returns `default` for
  an absent key and lets `JsonException` propagate on malformed bytes (a programmer/data-contract error,
  not expected control flow — consistent with the spec's error-model NFR).
- **Redis integration test via Testcontainers.** A `[SkippableFact]` (Xunit.SkippableFact) suite starts
  a real Redis container. Because Testcontainers contacts Docker eagerly in `Build()`, both `Build()` and
  `StartAsync()` are guarded so the tests **skip** rather than fail when Docker is unavailable (the
  macOS/Windows CI cells and dev boxes without Docker); the Linux CI cell runs them for real.

## Alternatives Considered

- **In-memory-only or distributed-only cache.** Rejected — the spec requires two levels: L1 for latency,
  L2 for cross-instance sharing.
- **Binary serialization (MessagePack / `BinaryFormatter`).** Rejected — `System.Text.Json` is in the
  BCL (no extra dependency), portable, and human-inspectable; `BinaryFormatter` is obsolete and unsafe.
- **Propagating the L2 remaining TTL to L1 on promotion.** Rejected — `IDistributedCache` exposes no
  remaining-TTL API; the current call's `ttl` is used for the promoted L1 entry (documented). An L1
  entry therefore may outlive the matching L2 entry by at most one `ttl` window.
- **Deserialising to decide L2 presence.** Rejected — it cannot distinguish an absent key from a stored
  `default(T)` for value types; the raw-bytes check can.
- **Serialising cache-stampede protection (per-key lock).** Deferred — under concurrent misses for the
  same key the factory may run more than once (last write wins); this is documented on `CacheService`
  and can be added later without an API change.
- **A cross-node L1 invalidation bus (e.g. Redis pub/sub on removal).** Deferred — it would let a
  `RemoveAsync` evict peers' L1 immediately rather than waiting out their TTL, but it adds a broadcast
  dependency and delivery-failure semantics the minimal spec surface does not require. The documented
  bounded-`ttl` guidance caps staleness without it, and it can be added later as opt-in without changing
  the `ICacheService` contract.
- **An embedded/mock Redis instead of Testcontainers.** Rejected — the spec mandates a real Redis via
  Testcontainers; the Docker-gated skip keeps the matrix green where Docker is absent.

## Consequences

- Infrastructure gains `Microsoft.Extensions.Caching.Abstractions` (8.0.0) — abstractions only; the
  consumer supplies concrete `IMemoryCache` / `IDistributedCache` (e.g. Redis) via DI.
- The test project gains `Microsoft.Extensions.Caching.Memory` (8.0.1, for `MemoryCache` /
  `MemoryDistributedCache` fast unit tests), `Microsoft.Extensions.Caching.StackExchangeRedis` (8.0.28),
  `Testcontainers.Redis` (4.12.0), and `Xunit.SkippableFact` (1.5.61) — all MIT-licensed.
- The distributed hash format is compact JSON; entries written via `DistributedCacheExtensions` and via
  `CacheService` are interchangeable because they share `CacheSerializer`.
- DI wiring (registering `ICacheService`, `IMemoryCache`, and a distributed cache) is deferred to the
  Milestone 8 DI helper, consistent with ADR-0007 and ADR-0008.
- **L1 is per process; cross-node invalidation is not provided.** `RemoveAsync` and writes update the
  caller's L1 and the shared L2, but no channel invalidates other instances' L1, so a peer serves its own
  L1 copy until that entry expires or is evicted (indefinitely with `ttl = null`). This is expected for a
  two-level cache without an invalidation bus; a bounded per-call `ttl` caps the staleness window. This is
  documented on `CacheService` and `ICacheService.RemoveAsync`.
- Adopts the **Proxy** (Structural) pattern; catalogued.

## References

- spec §2 (requirements 21–22), §3, §4, §6; ADR-0003; ADR-0006; ADR-0007; ADR-0008.
- `docs/patterns/design-patterns.md` — Structural: *Proxy*.
