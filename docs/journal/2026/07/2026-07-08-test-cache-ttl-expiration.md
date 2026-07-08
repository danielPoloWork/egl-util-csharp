# 2026-07-08 — Test: cover cache TTL/expiration (#101)

Ninth from the zero-production-risk [test] tier (Medium). Pure test-coverage — no production change, so
no CHANGELOG and no ADR.

## Done

- **Gap:** no test ever passed a `ttl` to `CacheService.GetOrCreateAsync`, and none asserted expiration —
  a regression applying the ttl to only one level, swapping absolute for sliding expiration, or dropping
  the ttl on the L2 write would have passed the whole suite (#101).
- **Unit tests (deterministic, no waiting):** capturing spies — `CapturingMemoryCache` (always-miss
  `IMemoryCache` returning a `CapturingCacheEntry` that records the options) and
  `CapturingDistributedCache` (captures `SetAsync`'s `DistributedCacheEntryOptions`).
  `..._applies_a_positive_ttl_as_absolute_expiration_to_both_levels` asserts both levels get
  `AbsoluteExpirationRelativeToNow == ttl` with `SlidingExpiration`/`AbsoluteExpiration` null;
  `..._without_a_ttl_writes_entries_with_no_expiration` asserts neither level gets an expiration.
- **Redis integration test** (`[SkippableFact]`, skips without Docker): sets `ttl = 1s`, drops L1, polls
  `redis.GetAsync<Sample>` until the key expires, then re-fetches and asserts the factory ran again
  (`calls == 2`).
- **Meaningfulness verified by mutation:** a compiling absolute→sliding swap on the L2 write fails the
  positive-ttl unit test; the real source passes. `dotnet build -warnaserror` clean; `dotnet format`
  clean; 172 pass / 3 Redis skip. Verified by a 1-lens adversarial review.
- **Caveat:** the Redis integration test cannot be run locally (Docker unavailable → skipped, like the
  two existing Redis tests); it mirrors their established `[SkippableFact]` + `Skip.IfNot(_dockerAvailable)`
  pattern and only executes in a Docker-enabled CI cell.

## State

- v1.0.0 released. Test-only → no shipped change. PR drafted on `test/cache-ttl-expiration`, awaiting
  merge. CI still billing-blocked (jobs don't start; verified locally). No local Python →
  `consistency_lint` hand-verified; touches only two test files, this journal (+ index row), and the
  ROADMAP checkpoint pointer — no lint invariant affected.

## Next

- Continue zero-risk [test]/[docs] safest-first: #100 (Redis-skip silent-vanish — make the skip visible),
  #51/#52 (cache/validation concurrency), or docs-accuracy (#42/#45/#46/#94/#98). The remaining `[fix]`
  Mediums (#31/#33/#25/#26/#62/#71/#61) carry design decisions → ADR + a maintainer AskUserQuestion.
