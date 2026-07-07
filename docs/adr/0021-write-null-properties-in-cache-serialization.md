# ADR-0021: Write null properties in the cache serialization format (drop WhenWritingNull)

- **Status:** Accepted
- **Date:** 2026-07-07
- **Deciders:** Maintainer
- **Related:** ADR-0009 (two-level cache & distributed serialization — this ADR amends its serialization-format detail)

## Context

`CacheSerializer.Options` (ADR-0009) set `DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull`
for a more compact stored payload: a `null` property was omitted from the JSON. But `System.Text.Json`
leaves an absent property at its C# constructor/initializer default on read. For any cached type whose
nullable property has a **non-null** default — e.g. `public string? Note { get; set; } = "n/a";` or
`record R(int Id, string? X = "x")` — a value cached with that property explicitly `null` was read back
from L2 with the property reverted to its default. Silent data corruption with no exception: after L1
eviction, identical `GetOrCreateAsync` calls could observe different data (#58). `CacheSerializer` is
internal, so a consumer cannot opt out.

## Decision

Drop `DefaultIgnoreCondition = WhenWritingNull` from `CacheSerializer.Options`. Null properties are now
written explicitly (`"note": null`) and round-trip back as `null`. This amends the serialization-format
detail of ADR-0009 (its two-level cache and "compact UTF-8 System.Text.Json" decisions otherwise stand);
compactness still comes from the Web defaults and no indentation. Round-trip fidelity outweighs the few
bytes saved by omitting nulls in a cache.

## Alternatives Considered

- **Keep WhenWritingNull and document that cached types must not rely on non-null property defaults.**
  Rejected — it pushes a silent-corruption footgun onto every consumer with no way to opt out (the
  serializer is internal), for a marginal size saving.
- **Expose the serializer options so consumers can override.** Rejected as a larger surface and out of
  scope; the correct default (faithful round-trip) removes the need.

## Consequences

- A `null` property round-trips as `null` regardless of the type's C# defaults; no more silent reversion.
- Stored cache payloads are marginally larger when values carry null properties (explicit `null` tokens);
  acceptable for a cache.
- No public API or signature change; the ADR-0009 format detail is superseded here.

## References

- Issue #58; ADR-0009 (amended); `CacheSerializer`.
