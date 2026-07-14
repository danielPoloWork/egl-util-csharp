# 2026-07-14 — Fix: Error constructor null guards + inlining-safe default scan (#76, #90)

Roadmap PR 7 (correctness cluster). Two tiny, unrelated-but-both-trivial hardening fixes, tight batch.
CHANGELOG `Fixed` (both); no ADR, no frozen-spec amendment (both are behavioral hardening within existing
signatures — no public surface change). Proceeded without asking (non-breaking, no API/MAJOR decision).

## Done

- **#76 — `Error` had no runtime null validation.** `sealed record Error(string Code, string Message)` backed
  its non-nullable annotations with NRT alone, so a caller outside NRT enforcement (`#nullable disable`, an
  explicit `null!`, or System.Text.Json materializing an `Error` from a payload with a missing field via the
  parameterized constructor) could build an `Error` with a null `Code`/`Message` that NRE'd far downstream.
  **Fix:** re-declared the positional properties with a validating initializer —
  `public string Code { get; init; } = Code ?? throw new ArgumentNullException(nameof(Code));` (and likewise
  `Message`). This is the standard idiom for validating positional-record parameters: it keeps the record
  **fully positional** (constructor, `Deconstruct`, `with`, equality) and guards the primary-constructor path
  (which is also the path System.Text.Json uses), while `Error.None`'s empty-string sentinel stays valid
  (empty ≠ null). Additive; no API change.
- **#90 — default scan not inlining-safe.** `AddD4npUtil`'s no-argument default scan reads
  `Assembly.GetCallingAssembly()`; the BCL warns that if the calling method is JIT-inlined it returns the
  caller's caller — silently scanning the wrong assembly and registering nothing. **Fix:** added
  `[MethodImpl(MethodImplOptions.NoInlining)]` (the canonical guard). Also extended the XML docs to warn about
  the wrapper trap: a shared-library convenience/extension method forwarding to `AddD4npUtil()` makes the
  **wrapper's** assembly the scan target, so such wrappers must pass the assemblies explicitly. Metadata +
  docs; no API change.
- **Tests (both mutation-verified non-vacuous):**
  - #76: `Constructor_rejects_a_null_code` / `Constructor_rejects_a_null_message` — assert
    `ArgumentNullException` with the right `ParamName`. Mutation (strip the `?? throw`) → no throw → fail.
  - #90: `The_default_scan_entry_point_is_not_inlinable` — asserts `MethodImplAttributes.NoInlining` is set on
    `AddD4npUtil` (checked via `MethodImplementationFlags`, since it's a pseudo-custom attribute). Mutation
    (remove the attribute) → flag absent → fail. The existing
    `Scans_the_calling_assembly_when_no_assembly_is_given` remains the functional companion.
- `dotnet build -warnaserror` clean; `dotnet format --verify-no-changes` clean; full suite **221 pass / 3
  Redis skip** (+3 new).

## State

- v1.0.0 released. `Error` now guards its components; `AddD4npUtil` is inlining-safe with a documented
  wrapper caveat. CHANGELOG `Fixed`; no ADR. PR drafted on `fix/error-null-guard-and-noinlining-scan`,
  awaiting merge. CI billing-blocked (verified locally). No local Python → `consistency_lint` hand-verified;
  README untouched (no i18n churn).

## Next

- Roadmap PR 8: **#88** — `SpecificationEvaluator` silently prefers `OrderBy` when a custom `ISpecification`
  sets both `OrderBy` and `OrderByDescending`. Decide: apply a deterministic both-orderings rule, or document
  the precedence. Standalone; may fold into a spec-doc PR.
