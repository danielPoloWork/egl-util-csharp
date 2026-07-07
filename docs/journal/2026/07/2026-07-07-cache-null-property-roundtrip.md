# 2026-07-07 — Fix: cache serializer preserves null properties (drop WhenWritingNull) (#58)

Eleventh fix from the Medium/Low audit tier. A small serialization-format correctness fix — it amends a
documented ADR-0009 detail, so it carries a short ADR (ADR-0021) but no code-signature change.

## Done

- **Bug:** `CacheSerializer.Options` set `DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull`, so
  a `null` property was omitted from the stored JSON. `System.Text.Json` leaves an absent property at its
  C# constructor/initializer default on read, so a cached type with a **non-null** property default (e.g.
  `string? Note { get; set; } = "n/a";`, or `record R(int Id, string? X = "x")`) cached with that property
  explicitly `null` came back from L2 with the property reverted to its default — silent, exception-free
  corruption. After L1 eviction, identical `GetOrCreateAsync` calls could observe different data. The
  serializer is internal, so consumers could not opt out (#58).
- **Fix:** dropped `DefaultIgnoreCondition = WhenWritingNull` (and the now-unused
  `using System.Text.Json.Serialization;`). Null properties are written explicitly and round-trip as
  `null`; compactness still comes from the Web defaults + no indentation. No API/signature change.
- **ADR-0021** records the trade-off (round-trip fidelity over a few bytes) and **amends the
  serialization-format detail of ADR-0009** (which now carries an "Amended in part by ADR-0021" header
  note); the two-level-cache and compact-STJ decisions otherwise stand. Indexed.
- **Test:** the existing `Round_trips_an_object_with_a_nullable_property` used `record Optional(int Id,
  string? Note)` whose default is already `null`, so it passed *vacuously*; its comment is corrected. New
  `An_explicitly_null_property_survives_the_round_trip_despite_a_non_null_default` uses a class with a
  `Note = "n/a"` initializer, caches it with `Note = null`, and asserts it reads back `null`. Verified red
  pre-fix (came back `"n/a"`), green post-fix. `dotnet build -warnaserror` clean; `dotnet format` clean;
  154 pass / 2 Redis skip.
- **Docs:** CHANGELOG `[Unreleased]` → Fixed.

## State

- v1.0.0 released. No public surface change → patch-level. PR drafted on
  `fix/cache-null-property-roundtrip`, awaiting merge. CI still billing-blocked (jobs don't start; verified
  locally). No local Python → `consistency_lint` hand-verified; touched invariant: **adr-index** (0021
  added, indexed, sequential 0001–0021). README/spec/i18n untouched (WhenWritingNull was only named in
  ADR-0009, now amended); others unaffected.

## Next

- Continue Medium/Low safest-first. Cache-serialization sibling **#33** (polymorphic static-`T` member
  loss) remains — but its real fix (runtime-type / `$type` polymorphic handling) has security implications
  and is a document-vs-enable design decision → would need an ADR + a maintainer AskUserQuestion. Other
  candidates: #30 (handler-raised events cleared before publish), #31 (Specification single sort key / no
  `ThenBy`). Entity-equality #25/#26 are property-tested (higher blast radius). Several Mediums (#62, #71,
  #61) carry design decisions → ADR + AskUserQuestion.
