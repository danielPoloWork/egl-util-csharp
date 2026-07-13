# 2026-07-13 — Fix: define the CacheService L2-outage contract (#62)

A `[fix]` (correctness/resilience) with a maintainer design decision, so it carries ADR-0024 (adding
ADR-0009's outage contract) and a CHANGELOG `Fixed` entry.

## Done

- **Bug:** `CacheService.GetOrCreateAsync` had no defined behavior for `IDistributedCache` (L2) provider
  faults. Any fault failed the whole call; worst of all, a fault on the **post-factory** L2 write threw
  away the value the factory had just produced (a Redis blip — or cancellation between factory completion
  and write — turned a successful computation into a business-operation failure). `RemoveAsync` left L2
  stale after clearing L1 with no documented semantics (#62).
- **Decision (maintainer via AskUserQuestion): "transparent writes; reads/remove propagate."** Chosen over
  fail-open-everywhere and document-only.
- **Fix:** serialize the value **before** the guarded write (so a serialization/contract error still
  surfaces), then wrap only the L2 `SetAsync` in `try/catch (Exception) when (not OperationCanceledException)`
  — a write fault is swallowed and the value returned (still promoted to L1); cancellation propagates. L2
  **read** faults and `RemoveAsync` faults are unchanged (propagate), now documented in the type
  `<remarks>`. ADR-0024 records the contract + rejected alternatives; ADR-0009 gains a pointer.
- **Scope:** no public-API change; optional logging of the swallowed fault deferred (would add a ctor
  overload — noted in the ADR as a future non-breaking enhancement, matching `EventBroker`'s optional
  logger).
- **Tests (fault-injecting `FaultyDistributedCache` double):** write fault → value returned + promoted to
  L1; cancellation on write → `OperationCanceledException` propagates; read fault → propagates (factory
  not run); `RemoveAsync` L2 fault → propagates after L1 is cleared.
- **Verified:** build `-warnaserror` clean (the general `catch … when` is fine — `EventBroker` catches
  `Exception` too, CA1031 not enforced); `dotnet format` clean; **186 pass / 3 Redis skip** (+4).
  Mutation-checked — reverting the swallow to a plain `await` (clean-compiling mutation) fails the
  write-fault test while the cancellation test still passes; the fix passes both.

## State

- v1.0.0 released. Production fix in `CacheService.cs`; ADR-0024; CHANGELOG `Fixed`. No public-API change.
  **Behavior change:** an L2 write fault now returns the value instead of throwing — flagged for the
  maintainer's SemVer call. PR drafted on `fix/cache-l2-outage-contract`, awaiting merge. CI still
  billing-blocked (verified locally). No local Python → `consistency_lint` hand-verified: ADR index gains
  the ADR-0024 row, README untouched (no i18n digest), no version/lockfile change.

## Next

- Remaining design-decision `[fix]` Mediums, each with an ADR + AskUserQuestion: #31 (Specification
  `ThenBy` — additive to the frozen `ISpecification`), #25/#26 (Entity transient-identity / EF-proxy
  equality — property-tested), #71 (contravariant handler variance), #61 (`DomainEventNotification`
  runtime bridge).
