# ADR-0023: Reject non-round-trippable cache types (fail fast on polymorphic serialization)

- **Status:** Accepted
- **Date:** 2026-07-13
- **Deciders:** Maintainer
- **Related:** ADR-0009 (two-level cache & distributed serialization — this ADR amends its serialization contract), ADR-0021 (also amends ADR-0009's serialization detail)

## Context

`CacheSerializer` serialises a cached value by its **declared** type `T`
(`JsonSerializer.SerializeToUtf8Bytes(value, Options)` with no `$type` discriminator). When `T` is a
base type, interface, or `object`, `System.Text.Json` writes only the declared type's members and reads
back a lossy/default value **with no exception**: `GetOrCreateAsync<IShape>(…)` storing a `Circle` keeps
none of `Circle`'s members, `GetOrCreateAsync<object>(…)` writes an empty `{}`, and caching a `Dog` under
`GetOrCreateAsync<Animal>(…)` silently drops `Dog`'s members. `ICacheService` never stated that `T` must
be a concrete, round-trippable type, so this is a silent-correctness trap (#33). Both cache entry points
(`CacheService` and `DistributedCacheExtensions`) share `CacheSerializer`, so the gap is uniform.

## Decision

Keep static-type serialization as the default, and make `T` an explicit part of the cache contract by
**failing fast** on usage that cannot round-trip, rather than storing a lossy value:

- **Reject a non-round-trippable declared type.** `CacheSerializer.EnsureDeclaredTypeIsRoundTrippable<T>()`
  throws `NotSupportedException` when `T` is `object`, an interface, or an abstract class. It is invoked
  on every write (`Serialize`) and read (`Deserialize`), and up front in `CacheService.GetOrCreateAsync`
  (before the factory runs) and `DistributedCacheExtensions.GetAsync` (even when the key is absent), so
  the misuse is reported consistently.
- **Reject a runtime subtype.** `Serialize<T>` also throws `NotSupportedException` when a non-null value's
  runtime type differs from `T` (comparing against `Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T)`,
  so a `Nullable<T>` value type is not a false positive). This catches the `Dog`-under-`Animal` case.
- **No unrestricted `$type` metadata.** The default serializer never emits a polymorphic type
  discriminator.
- **Opt-in polymorphism is a future, explicit capability.** If a caller needs to cache a polymorphic
  value, the sanctioned path is to supply their own polymorphic serializer / `JsonTypeInfo`. Any such
  opt-in **must** use an allow-listed discriminator (no open-ended `$type` gadget surface) and a
  **versioned wire format and cache key**, and must invalidate or version affected existing entries —
  fields already discarded by lossy writes cannot be recovered. No such API ships here; the guard is
  therefore unconditional today.

This amends the serialization contract of ADR-0009 (its two-level cache and compact-JSON decisions
otherwise stand; ADR-0021 already amended the null-property detail).

## Alternatives Considered

- **Document the constraint only (no runtime guard).** Rejected — it leaves the silent-loss footgun armed
  for anyone who does not read the docs; fail-fast turns a silent data bug into a loud, immediate error.
- **Enable polymorphic `$type` handling by default.** Rejected — it changes the stored wire format (breaks
  reads of existing entries and enlarges the compact-payload contract) and opens a polymorphic-deserialization
  (gadget) security surface. Inappropriate as an unconditional default for a shared distributed cache.
- **Serialize the runtime type on write only.** Rejected — the read side still deserialises the declared
  `T`, so an interface/base `T` still cannot be reconstructed; the asymmetry does not actually round-trip.

## Consequences

- Polymorphic misuse (`object` / interface / abstract `T`, or a runtime subtype) now throws
  `NotSupportedException` instead of silently caching a lossy value. This is a **behavior change**: code
  that previously "succeeded" with silent loss now fails fast. It surfaces only latent misuse (no correct
  usage relied on the loss), so it is treated as a correctness fix; the release's SemVer level is the
  maintainer's call at release time (weigh as at least MINOR given the new fail-fast semantics).
- The valid path is unchanged: a concrete `T` whose runtime type equals `T` serialises to exactly the same
  bytes as before, so **no wire-format or cache-key change** is needed for this change, and existing valid
  entries keep working. Only already-poisoned lossy entries (if any) now throw on read and should be left
  to expire or be flushed.
- The contract is documented on `ICacheService.GetOrCreateAsync`, `DistributedCacheExtensions`, and
  `CacheSerializer`, and covered by round-trip and failure tests.
- No public API or signature change.

## References

- Issue #33; ADR-0009 (amended), ADR-0021; `CacheSerializer`, `CacheService`, `DistributedCacheExtensions`.
