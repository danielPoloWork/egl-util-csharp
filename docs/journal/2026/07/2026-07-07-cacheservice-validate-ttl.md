# 2026-07-07 — Fix: validate non-positive TTL in GetOrCreateAsync (#34)

First fix from the post-1.0 audit **Medium/Low** tier (the six High findings are done). Safest-first:
a self-contained input-validation guard, no core-primitive or DI risk.

## Done

- **Bug:** `CacheService.GetOrCreateAsync` never validated `ttl`. A non-positive `ttl` (e.g.
  `deadline - now` where the deadline already passed) reached the entry-options setter, which throws
  `ArgumentOutOfRangeException` with no context — *after* the factory ran and its result was discarded
  (#34, Medium).
- **Fix:** validate `ttl <= TimeSpan.Zero` at the top of `GetOrCreateAsync` and throw
  `ArgumentOutOfRangeException` with a clear message before running the factory or touching either
  cache level. Documented the strictly-positive constraint (+ the exception) on
  `ICacheService.GetOrCreateAsync`. No ADR — a routine guard, no API/design change (the call already
  threw; now it fails fast, earlier, with context).
- **Tests:** `GetOrCreateAsync_rejects_a_non_positive_ttl` ([Theory] TimeSpan.Zero + negative) asserts
  the throw *and* that the factory never ran. `dotnet build -warnaserror` clean; `dotnet format` clean;
  142 pass / 2 Redis skip.
- Docs: CHANGELOG `[Unreleased]` Fixed.

## State

- v1.0.0 released. No public surface change (added a documented exception for already-invalid input) →
  patch-level. PR drafted on `fix/cacheservice-validate-ttl`, awaiting maintainer merge.
- Pipeline still GitHub Actions billing-blocked; verified locally.

## Next

- Continue the Medium/Low tier safest-first. Candidate self-contained `[fix][Medium]` next: #32
  (idempotent `ValidationPipelineBehavior` registration), #29 (`Result.Failure(Error.None)` invariant),
  #59 (JSON-null L2 entry poisons L1).
