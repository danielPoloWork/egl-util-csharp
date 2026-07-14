# 2026-07-14 — Feat: IPasswordHasher rehash-detection + span plaintext (#35, #41)

Roadmap PR 2 (security cluster). Additive API on the frozen `IPasswordHasher` via default interface
members → MINOR; carries ADR-0029, a frozen-spec §5 amendment, an ADR-0008 "extended by" note, and a
CHANGELOG `Added` entry.

## Done

- **Gaps:** `IPasswordHasher` had only `Hash(string)`/`Verify(string,string)`, so (#35) a caller couldn't
  tell a just-verified hash was under-strength — the promised verify-then-rehash upgrade path was
  unwritable; and (#41) the plaintext could only be passed as an immutable `string` that lingers on the
  heap.
- **Decisions (maintainer via AskUserQuestion):** #35 → add `NeedsRehash` (DIM); #41 → add
  `ReadOnlySpan<char>` overloads.
- **Change (all default interface members → non-breaking):** `IPasswordHasher` gains `NeedsRehash(string)`,
  `Hash(ReadOnlySpan<char>)`, `Verify(ReadOnlySpan<char>, string)`. Defaults: `NeedsRehash` → `false`; the
  span overloads forward to the `string` overloads. `PasswordHasher` overrides all three — the span
  overloads are now the **primitives** (the `string` overloads delegate to them, deriving directly via
  `Rfc2898DeriveBytes` span overloads), and `NeedsRehash` returns true for an older scheme, a stored
  iteration count below the configured work factor, or a malformed hash. Documented the
  verify-then-conditionally-rehash pattern. ADR-0029 extends ADR-0008; spec §5 amended.
- **Tests:** span round-trip; string/span interchangeability; `NeedsRehash` current/below/malformed; the
  full verify→NeedsRehash→rehash flow; null guards; and the **default-member behavior** via a string-only
  `IPasswordHasher` implementer (proving non-breaking). NeedsRehash-only tests use `ComposeHash` (parse-only,
  no PBKDF2) to stay fast.
- **Verified:** build `-warnaserror` clean (disambiguated the now-overloaded `Hash`/`Verify` crefs);
  `dotnet format` clean; PasswordHasher suite 33 pass; full suite green.

## State

- v1.0.0 released. **Additive** public surface on `IPasswordHasher` (3 default members) + `PasswordHasher`
  overrides; backward-compatible → accrues toward **1.1.0**. ADR-0029; ADR-0008 note; spec §5 amended;
  CHANGELOG `Added`. README doesn't detail the hasher API (blurb only) → no README/i18n change. PR drafted
  on `feat/passwordhasher-rehash-and-span`, awaiting merge. CI billing-blocked (verified locally). No local
  Python → `consistency_lint` hand-verified: ADR index gains ADR-0029, spec-map unaffected.

## Next

- Roadmap PR 3: **#91** — `RemoveAsync` clears L1 before L2, letting a concurrent same-node read
  re-promote the removed value into L1. Cache concurrency, standalone. No API change → proceed without a
  design question.
