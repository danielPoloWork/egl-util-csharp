# 2026-07-13 — Fix: fail fast on non-round-trippable cache types (#33)

A `[fix]` (correctness) with a genuine design decision, so it carries ADR-0023 (amending ADR-0009) and a
CHANGELOG `Fixed` entry. First of the design-decision `[fix]` Mediums.

## Done

- **Bug:** `CacheSerializer` serialises the **declared** type `T` (`SerializeToUtf8Bytes(value, Options)`,
  no `$type`). A base/interface/`object` `T` — `GetOrCreateAsync<IShape>()` storing a `Circle`, or
  `<object>` — wrote only `T`'s (often zero) members and read back lossy/default **with no exception**; a
  runtime subtype (a `Dog` under `<Animal>`) silently lost the subtype's members (#33).
- **Decision (maintainer via AskUserQuestion):** keep static-type serialization but **fail fast** on
  unsupported polymorphic usage. `T` is part of the cache contract: reject `object`/interface/abstract
  declared types and reject writes whose runtime type differs from `T`; any future polymorphic opt-in must
  use an allow-listed discriminator + versioned wire format/cache key; never emit unrestricted `$type` by
  default. No such opt-in ships now, so the guard is unconditional.
- **Fix:** `CacheSerializer.EnsureDeclaredTypeIsRoundTrippable<T>()` (throws `NotSupportedException` for
  `object`/interface/abstract `T`) runs on every `Serialize`/`Deserialize`, and up front in
  `CacheService.GetOrCreateAsync` (before the factory) and `DistributedCacheExtensions.GetAsync` (even on
  an absent key). `Serialize<T>` also rejects a runtime subtype (`value.GetType() != underlying(T)`,
  `Nullable<T>`-aware). Documented the contract on `ICacheService.GetOrCreateAsync`,
  `DistributedCacheExtensions`, and `CacheSerializer`. ADR-0023 amends ADR-0009 (which gained an
  "Amended in part by ADR-0023" note).
- **Tests:** `CacheServiceTests` — polymorphic declared `T` rejected before the factory runs (object /
  interface / abstract), runtime subtype rejected, and a concrete + `Nullable<T>` happy path. Same guard
  tests on `DistributedCacheExtensions.Get/SetAsync` (including the absent-key read).
- **Verified:** build `-warnaserror` clean; `dotnet format` clean; **cache suite 43 pass**; full suite
  green. Mutation-checked — removing the guards (clean-compiling mutation) fails the new tests; the fix
  passes them.

## State

- v1.0.0 released. Production fix in `CacheSerializer.cs` (+ entry guards in `CacheService`/
  `DistributedCacheExtensions`, docs on `ICacheService`); ADR-0023; CHANGELOG `Fixed`. No public-API
  change; **no wire-format change** for valid entries (byte-identical), so no cache-key version bump.
  **Behavior change:** polymorphic misuse now throws (was silent loss) — flagged for the maintainer's
  SemVer call at release. PR drafted on `fix/cache-non-round-trippable-type-guard`, awaiting merge. CI
  still billing-blocked (verified locally). No local Python → `consistency_lint` hand-verified: ADR index
  gains the ADR-0023 row (adr-index check), README untouched (no i18n digest), no version/lockfile change.

## Next

- Remaining design-decision `[fix]` Mediums, each with an ADR + AskUserQuestion: #31 (Specification
  `ThenBy` — additive to the frozen `ISpecification`), #25/#26 (Entity transient-identity / EF-proxy
  equality — property-tested), #62 (L2-outage contract), #71 (contravariant handler variance), #61
  (`DomainEventNotification` runtime bridge).
