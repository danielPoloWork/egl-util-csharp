# 2026-07-07 — Fix: remove the implicit Error → Result<TValue> conversion (#28)

Ninth fix from the Medium/Low audit tier; the last of the three core `Result` invariants (#29 → #27 →
this #28). Unlike #29/#27 this is a **deliberate breaking change** to the frozen v1.0 surface — the
maintainer chose full removal (over document-only / defer) via an AskUserQuestion decision, as with #67.

## Done

- **Bug:** `Result<TValue>` had two implicit conversions (spec §2): from `TValue` (success) and from
  `Error` (failure). They collide — `Result<Error> r = someError;` is a compile error (CS0457), and
  `Result<object> r = someError;` prefers the more-specific `Error` source type, silently binding the
  `Error` operator and producing a **failure** instead of a success carrying the error (#28).
- **Decision (maintainer, this session):** remove `implicit operator Result<TValue>(Error)`; keep the
  `TValue` → `Result<TValue>` success conversion (the useful, non-colliding one). Failures are created
  with the explicit `Result.Failure<TValue>(error)` factory. Chosen over "document the footgun only" and
  "defer". Classified as a **breaking change** (not a bug fix): `return someError;` must become
  `return Result.Failure<TValue>(error);`. MAJOR bump decided at release; CHANGELOG records it under
  *Removed* with the migration note.
- **ADR-0020** records the decision and amends ADR-0010's 1.0 API freeze + spec §2 (both annotated /
  updated). Indexed in `docs/adr/README.md`.
- **Frozen spec §2** (`docs/specs/01_spec_util.md:18`) amended: `Result<TValue>` now advertises implicit
  conversion from `TValue` only, with failures via the explicit factory.
- **Tests:** removed `Implicitly_converts_from_an_error_to_failure` (no longer compiles); added
  `A_failure_is_created_explicitly_from_an_error` (explicit factory) and, as the #28 regression,
  `An_error_assigned_to_a_result_is_a_success_carrying_it` (`Result<object> r = error` now binds the value
  operator → **success** carrying the error; verified red pre-fix, where the Error operator made it a
  failure). Also dropped the now-uncompilable implicit-operator assertion from #29's
  `Failure_rejects_Error_None` (that operator path no longer exists — a cross-issue interaction the build
  caught immediately).
- **Docs:** README quick-start no longer uses the implicit `Error` conversion (uses
  `Result.Failure<User>(...)`); the zh/jp translations re-synced (same code, comment dropped) and both
  i18n digests refreshed to `a2639f36acf8bcfe`. CHANGELOG `[Unreleased]` → Removed (BREAKING).
  `dotnet build -warnaserror` clean; `dotnet format` clean; 151 pass / 2 Redis skip.

## State

- v1.0.0 released. **Breaking** change to the frozen surface → MAJOR at release (maintainer's call). PR
  drafted on `fix/drop-implicit-error-operator`, awaiting merge. CI still billing-blocked (jobs don't
  start; verified locally). No local Python → `consistency_lint` hand-verified; touched invariants:
  **adr-index** (0020 added + indexed, sequential 0001–0020) and **i18n-freshness** (README digest →
  `a2639f36acf8bcfe`, both rows updated + translations re-synced). Others unaffected.

## Next

- Core `Result`/`Entity` invariants for this tier are done (#29/#27/#28). Continue Medium/Low safest-first
  among the remaining `[fix]` items (e.g. #25/#26 Entity identity/EF-proxy equality, #30/#31 spec/event
  items), or clear the no-code-change tiers ([test]/[docs]/[ci]). Entity equality (#25/#26) is
  property-tested (`EqualityLawProperties`) — higher blast radius; go carefully.
