# ADR-0013: Make CacheService compatible with a size-limited IMemoryCache

- **Status:** Accepted
- **Date:** 2026-07-06
- **Deciders:** Maintainer
- **Related:** ADR-0009 (amends its "consumer supplies the IMemoryCache" note), spec §2, issue #20

## Context

ADR-0009 established that the consumer supplies the L1 `IMemoryCache` via DI. A common production
registration is `AddMemoryCache(o => o.SizeLimit = N)`. When a `SizeLimit` is set, `MemoryCache.Set`
requires **every** entry to specify `MemoryCacheEntryOptions.Size`; an entry without one throws
`InvalidOperationException` ("Cache entry must specify a value for Size when SizeLimit is set").

`CacheService.SetMemory` wrote entries with no `Size` (`_memoryCache.Set(key, value)` /
`Set(key, value, expiration)`), so against a size-limited cache the **first** read-through or
promotion write threw — breaking `GetOrCreateAsync` for every uncached key (issue #20, High
severity). No test exercised a size-limited L1, so the regression shipped unnoticed.

## Decision

`CacheService.SetMemory` now writes every L1 entry with an explicit `MemoryCacheEntryOptions.Size`
of **one unit** (using the `MemoryCacheEntryOptions` overload, which also carries the optional
`AbsoluteExpirationRelativeToNow`). A size-limited `IMemoryCache` accepts the entry, and its
`SizeLimit` therefore bounds the **number** of entries this service keeps in L1. An unlimited
`IMemoryCache` ignores the `Size`, so the fix is safe to apply unconditionally and changes no existing
behaviour. This is an internal change: the public surface (`ICacheService`, the `CacheService`
constructor) is unchanged, so it is fully source- and binary-compatible.

Per-entry-size **configurability is deliberately deferred**. A uniform unit size is the only size
`CacheService` can assign without measuring or serialising each value (which would defeat L1's
latency purpose), and it can be made configurable later via a new constructor overload without a
breaking change if a real need arises.

## Alternatives Considered

- **Document that the supplied `IMemoryCache` must not set `SizeLimit`.** Rejected — it makes the
  library incompatible with a common, reasonable production configuration and merely shifts the
  problem onto every consumer.
- **Add an optional entry `Size` / size callback now.** Deferred — it expands the frozen 1.0 surface
  for a speculative need; a constant size adds little over the unit default, and a per-value callback
  needs the value the non-generic constructor cannot cleanly capture. Addable non-breakingly later.
- **Serialise each value to compute a byte-accurate L1 size.** Rejected — double-serialising on every
  write defeats the point of a fast in-memory L1, and the L2 write already serialises once.

## Consequences

- `CacheService` works against both size-limited and unlimited `IMemoryCache` registrations. For a
  size-limited cache, `SizeLimit` bounds the count of this service's L1 entries (documented on the
  type). This **amends** ADR-0009's note that "the consumer supplies concrete `IMemoryCache`" with the
  size contract.
- No public API or behavioural change for unlimited caches; existing tests are unaffected.
- Tests: added `CacheServiceTests` cases over `new MemoryCache(new MemoryCacheOptions { SizeLimit = N })`
  (read-through write, the TTL branch, and L2-promotion), plus an end-to-end `AddD4npUtil` DI test that
  resolves `ICacheService` over a size-limited cache and reads through it.

## References

- spec §2 (`ICacheService`, `CacheService`); ADR-0009; issue #20.
- `docs/patterns/design-patterns.md` — Structural: *Proxy* (unchanged).
