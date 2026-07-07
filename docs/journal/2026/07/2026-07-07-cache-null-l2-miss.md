# 2026-07-07 — Fix: JSON-null L2 entry poisons L1 with a null hit (#59)

Third fix from the Medium/Low audit tier. Cache correctness: a JSON `null` in L2 could permanently
poison L1.

## Done

- **Bug:** `DistributedCacheExtensions.SetAsync` had no null guard — a nullable `T` value serialised to
  the 4-byte JSON literal `null`. `CacheService.GetOrCreateAsync` then saw those bytes as a hit
  (`bytes is { Length: > 0 }`), deserialised `null`, and `SetMemory(key, null, ttl)` cached `null` in
  L1. The BCL `TryGetValue` reports a stored `null` as a HIT, so every later read returned `null`
  without the factory — permanently when `ttl` is null. This violated the frozen `ICacheService`
  contract ("a null result is never cached") via the interop path ADR-0009 advertises (#59).
- **Fix (both, defense in depth):**
  - `CacheService.GetOrCreateAsync` treats a deserialized `null` from L2 as a **miss** — it does not
    promote `null` into L1 and falls through to the factory. A default value type (`0`) is not `null`,
    so it is still a real hit (the documented value-type behaviour is preserved).
  - `DistributedCacheExtensions.SetAsync` guards `ArgumentNullException.ThrowIfNull(value)`, so the
    library's own write path never stores a JSON `null`. `GetAsync`'s `<returns>` now documents that a
    stored `null` is indistinguishable from an absent key (for reference types too).
  No ADR — a correctness fix restoring the existing never-cache-null policy, no API/design change.
- **Tests:** `SetAsync_rejects_a_null_value`; `GetOrCreateAsync_treats_a_json_null_L2_entry_as_a_miss`
  (raw `null` bytes written straight to L2 → factory runs once, L1 holds the real value, not poisoned).
  `dotnet build -warnaserror` clean; `dotnet format` clean; 145 pass / 2 Redis skip.
- Docs: CHANGELOG `[Unreleased]` Fixed; XML on `SetAsync`/`GetAsync`/`GetOrCreateAsync`.

## State

- v1.0.0 released. `SetAsync` now throws on a null value (a documented guard for already-invalid input)
  → patch-level. PR drafted on `fix/cache-null-l2-miss`, awaiting maintainer merge. Actions still
  billing-blocked; verified locally.

## Next

- Continue Medium/Low safest-first: #60 (EventBroker disposes the dispatch scope synchronously →
  IAsyncDisposable-only services leak), then the assembly-scan DI fixes (#65/#67), then the core Result
  invariants (#29/#27/#28).
