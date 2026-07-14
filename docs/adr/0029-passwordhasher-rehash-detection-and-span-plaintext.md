# ADR-0029: Add rehash-detection and span-based plaintext to IPasswordHasher

- **Status:** Accepted
- **Date:** 2026-07-14
- **Deciders:** Maintainer
- **Related:** ADR-0008 (password hashing & validation — this ADR extends its `IPasswordHasher` surface), spec §2 (requirements 18–20)/§5, #35, #41

## Context

`IPasswordHasher` exposed only `Hash(string)` and `Verify(string, string)`. Two gaps (a Medium + a Low):

- **#35 — no rehash detection.** `Verify` re-derives with the work factor read from the stored hash and
  the docs promise stale hashes are upgraded "on next use", but there is no member to tell a caller that a
  just-verified hash was produced below the current work factor. The promised verify-then-rehash upgrade
  path is unwritable, so under-strength credentials stay under-strength forever.
- **#41 — plaintext only as `string`.** `Hash`/`Verify` take the secret as an immutable `string`, which
  lingers on the managed heap (and can be duplicated by GC compaction) until collection. A caller holding
  the plaintext in a clearable buffer cannot bound its lifetime, even though `Rfc2898DeriveBytes` offers
  `ReadOnlySpan` overloads.

Both surfaces are frozen at 1.0.

## Decision

Extend `IPasswordHasher` additively, using **C# default interface methods** so existing implementers do
not break (source/binary compatible; a MINOR change):

- **`bool NeedsRehash(string hash)`** — returns `true` when the stored hash uses an older scheme, a lower
  work factor than the current configuration, or an unrecognised/malformed format; `false` for a
  current-strength hash. Default implementation returns `false` (a base implementer cannot know its work
  factor). `PasswordHasher` overrides it (scheme mismatch, malformed, or stored iterations < configured).
  The XML docs describe the verify-then-conditionally-rehash pattern.
- **`string Hash(ReadOnlySpan<char> password)`** and **`bool Verify(ReadOnlySpan<char> password, string hash)`**
  — let a caller that controls a clearable buffer derive without materializing a `string`. Default
  implementations forward to the `string` overloads (materializing a string); `PasswordHasher` overrides
  them as the **primitives** (the `string` overloads delegate to the span ones), deriving directly via
  `Rfc2898DeriveBytes`'s span overloads.

## Alternatives Considered

- **Document the gaps only.** Rejected for #35 (the upgrade path stays unwritable — the Medium is
  unresolved). For #41 the maintainer chose to add the overloads rather than only document, giving
  buffer-controlling callers a real zero-copy path.
- **Add the members as non-default (abstract) interface members.** Rejected — source-breaking for every
  external `IPasswordHasher` implementer. Default interface methods achieve the additive surface without
  the break.
- **A separate value-type work-factor descriptor / `TryReadParameters`.** Deferred — `NeedsRehash(hash)`
  is the minimal member the upgrade flow needs; a richer parameter-inspection API can come later.

## Consequences

- Callers can implement the verify-then-conditionally-rehash upgrade path, and buffer-controlling callers
  can bound the plaintext lifetime. Existing implementers and callers are unaffected (default members);
  accrues toward the next MINOR (1.1.0).
- The `string` and `ReadOnlySpan<char>` overloads are interchangeable (same derivation); `NeedsRehash`
  never derives (parse-only). Extends the ADR-0008 surface; spec §5 (the `IPasswordHasher` entry) is
  amended. Covered by tests, including the default-member behavior via a string-only implementer.

## References

- Issues #35, #41; ADR-0008; spec §2 (18–20)/§5; `IPasswordHasher`, `PasswordHasher`.
