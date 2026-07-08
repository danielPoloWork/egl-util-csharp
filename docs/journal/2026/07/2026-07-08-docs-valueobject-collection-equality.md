# 2026-07-08 — Docs: ValueObject collection-component equality caveat (#45)

Eleventh from the zero-production-risk tier; a docs-accuracy clarification. No code/behavior change, so
no CHANGELOG and no ADR.

## Done

- **Gap:** `ValueObject` equality is `GetEqualityComponents().SequenceEqual(...)` and hashing is
  `HashCode.Add` per component — both use each component's **default** equality. A subclass that yields a
  collection as a single component (`yield return _tags;`) therefore compares that component by reference,
  so two content-equal value objects with distinct list instances are unequal and hash differently. The
  docs gave no guidance (#45).
- **Fix:** added a `<remarks>` to `GetEqualityComponents` spelling out the default-equality-per-component
  behavior and instructing authors to yield collection **elements** individually
  (`foreach (var tag in _tags) yield return tag;`) or wrap them in a structurally-equal type. No shipped
  type does the wrong thing; this is guidance for consumers. `dotnet build -warnaserror` clean (the
  `object.Equals(object)` cref resolves); `dotnet format` clean; 172 pass / 3 Redis skip (unchanged).

## State

- v1.0.0 released. Docs-only (an XML `<remarks>`); no behavior/API change. PR drafted on
  `docs/valueobject-collection-equality`, awaiting merge. CI still billing-blocked (verified locally). No
  local Python → `consistency_lint` hand-verified; touches only `ValueObject.cs` (doc comment), this
  journal (+ index row), and the ROADMAP pointer — no lint invariant affected.

## Next

- Continue docs-accuracy safest-first: #46 (AggregateRoot's unsynchronized event buffer / thread-confinement
  contract), #42 (cross-node L1 staleness after RemoveAsync), #98 (IDomainEvent.OccurredOnUtc DateTimeKind
  is contract-only), #94 (SECURITY.md supported-versions table stale post-1.0 — involves a small policy
  call). Then #51/#52 (cache/validation concurrency). The remaining `[fix]` Mediums (#31/#33/#25/#26/#62/#71/#61)
  carry design decisions → ADR + a maintainer AskUserQuestion.
