# ADR-0024: Define the CacheService L2-outage contract (transparent writes; reads and removes propagate)

- **Status:** Accepted
- **Date:** 2026-07-13
- **Deciders:** Maintainer
- **Related:** ADR-0009 (two-level cache & distributed serialization — this ADR adds its outage contract)

## Context

`CacheService.GetOrCreateAsync` had no defined behavior for `IDistributedCache` (L2) provider faults,
which are routine in production (a Redis connection blip, a timeout). Two hazards (#62):

1. **A read fault fails the whole call.** If `_distributedCache.GetAsync` throws on an L1 miss, the call
   fails even though the factory could have produced the value.
2. **A post-factory write fault discards a computed value.** The factory runs first, then L2 is written;
   if that `SetAsync` throws, the successfully-computed value is thrown away and the caller gets an
   exception instead of the value — the cache is not transparent. A token cancelling between factory
   completion and the write did the same via `OperationCanceledException`.

`RemoveAsync` clears L1 then L2, so an L2 delete fault leaves L2 stale after L1 is already cleared. The
XML docs listed only `JsonException`; ADR-0009 never mentioned outage behavior.

## Decision

Adopt a **transparent-write** outage contract; reads and removes still surface faults:

- **Post-factory L2 write faults are swallowed.** Once the factory has produced a value, an exception
  from the L2 `SetAsync` is caught and the value is returned (and still promoted to L1) — a distributed-cache
  blip never turns a successful computation into a business-operation failure; the entry is simply not
  stored in L2 that time and will be re-written on a later miss.
- **Cancellation is honored.** The catch filter excludes `OperationCanceledException`, so a cancellation
  from the supplied token propagates rather than being masked as an outage.
- **Serialization errors still surface.** The value is serialised *before* the guarded write, so a
  serialization/contract error (a programming error, e.g. a non-round-trippable type per ADR-0023)
  propagates rather than being swallowed as an "outage".
- **Reads and removes propagate.** An L2 fault *reading* (before the factory runs) fails the call, and an
  L2 fault in `RemoveAsync` propagates (L1 has already been cleared, so L2 may be left stale). Both are
  now documented on `CacheService`.

## Alternatives Considered

- **Fail-open everywhere** (degrade L2 read faults to a factory call, make `RemoveAsync` best-effort).
  Rejected for now — maximal availability, but it masks outages, can hammer the backing store via the
  factory during an outage (no L2 to serialise misses), and lets "successful" removes leave L2 stale.
- **Document only, propagate all faults.** Rejected — it leaves the worst defect (discarding a
  successfully-computed value on a transient write blip) in place.
- **Log the swallowed write fault via an injected `ILogger`.** Deferred — it would add a constructor
  overload (additive public surface). Optional observability can be added later as a non-breaking
  enhancement, matching `EventBroker`'s optional-logger pattern; the contract itself does not require it.

## Consequences

- A transient L2 write blip no longer fails `GetOrCreateAsync`; the value is returned and served from L1.
  **Behavior change:** callers that previously saw an exception on an L2 write fault now get the value.
  It surfaces only latent fragility (no correct usage wanted the value discarded); the release's SemVer
  level is the maintainer's call (weigh as at least a PATCH-level fix / MINOR resilience change).
- Write faults are currently swallowed **silently** (no logger); operators rely on the L2 provider's own
  monitoring until optional logging is added.
- Read and `RemoveAsync` faults are unchanged (they propagate), now explicitly documented.
- No public API or signature change; covered by fault-injection tests.

## References

- Issue #62; ADR-0009 (outage contract added here); `CacheService`.
