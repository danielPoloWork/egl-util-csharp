# 2026-07-06 — Fix: CacheService breaks against a size-limited IMemoryCache (#20)

Second fix from the post-1.0 audit backlog (after #21). High-severity bug: the first read-through
write threw against a size-limited L1 cache.

## Done

- **Root cause:** the consumer supplies the L1 `IMemoryCache` (ADR-0009). A common production
  registration is `AddMemoryCache(o => o.SizeLimit = N)`, which requires every entry to specify a
  `Size`. `CacheService.SetMemory` wrote entries with no `Size`, so the first read-through/promotion
  write threw `InvalidOperationException`, breaking `GetOrCreateAsync` for every uncached key. No test
  covered a size-limited L1.
- **Fix (no public API change):** `SetMemory` now writes each L1 entry via the
  `MemoryCacheEntryOptions` overload with `Size = 1` (plus the optional `AbsoluteExpirationRelativeToNow`).
  A size-limited cache accepts it and its `SizeLimit` bounds the entry count; an unlimited cache ignores
  the size, so existing behaviour is unchanged. Per-entry-size configurability deliberately deferred
  (addable later via a new constructor overload without breaking; ADR-0013 explains why).
- **Tests:** size-limited `CacheServiceTests` (read-through write, the TTL branch, L2 promotion) and an
  end-to-end `AddD4npUtil` DI test resolving `ICacheService` over a size-limited cache. `dotnet build
  -warnaserror` clean; `dotnet format --verify-no-changes` clean; 138 pass, 2 Redis integration tests
  skipped (no local Docker).
- Docs synced: ADR-0013 (amends ADR-0009), CHANGELOG `[Unreleased]` Fixed.

## State

- v1.0.0 released. This fix has no public surface change (source- and binary-compatible), so it is a
  patch-level change accruing toward the next release. Version files unchanged (bumped at release
  time). PR drafted on `fix/cacheservice-size-limited-memorycache`, awaiting maintainer merge.

## Next

- After merge, continue the audit backlog (safest-first). Next candidates: the CI/supply-chain trio
  #23 (`--locked-mode`), #24 (coverage gate), #22 (release pack/push).
