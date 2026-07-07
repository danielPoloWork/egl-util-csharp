# 2026-07-07 — Fix: Result.Failure rejects Error.None (#29)

Seventh fix from the Medium/Low audit tier, and the first of the core `Result` invariants (#29 → then
#27 → #28). A small, self-contained validation guard — no public API or signature change, no ADR
(analogous to #34's ttl guard).

## Done

- **Bug:** `Result.Failure(Error.None)` (or an error structurally equal to it — empty `Code` *and*
  `Message`) produced `IsFailure == true` yet `Error == Error.None`. Since `Error.None` is the documented
  sentinel carried by *successful* results, a caller using the doc-invited
  `if (result.Error == Error.None)` success check misclassified this genuine failure as success and
  swallowed the error (#29). The `Failure` factories validated only error *count*, never content.
- **Fix:** the shared `Defensive` guard (used by both `Failure(params Error[])` and
  `Failure<TValue>(params Error[])`) now throws `ArgumentException` if any supplied error equals
  `Error.None`, so a failure always carries a distinguishable error. The `Failure` XML docs gained the
  constraint and `<exception>` tags, and the implicit `Error` → `Result<TValue>` operator (which routes
  through `Failure<TValue>`, so `Result<int> r = Error.None;` now throws too) gained the same
  `<exception>` note. No API/signature change; behaviour only tightens for a nonsensical input, so no ADR.
- **Scope check:** nothing in `src/main` calls `Result.Failure`, and the `Combine` monoid only aggregates
  errors *from failures* — which post-fix can never contain `Error.None` — so `Combine` and the
  monoid property tests (`ResultCombineProperties`, which build failures only from real `Error("E"+seed,…)`
  values) are unaffected. Empty-`Code`-but-non-empty-`Message` errors are a different value that does not
  equal `Error.None` and is out of scope (runtime `Error` validation is #76).
- **Test:** `Failure_rejects_Error_None` asserts `Failure(Error.None)`, `Failure(real, Error.None)`, and
  `Failure<int>(Error.None)` all throw. Verified red pre-fix (no exception), green post-fix.
  `dotnet build -warnaserror` clean; `dotnet format` clean; 149 pass / 2 Redis skip.
- **Docs:** CHANGELOG `[Unreleased]` → Fixed.

## State

- v1.0.0 released. No public surface change → patch-level. PR drafted on `fix/failure-rejects-error-none`,
  awaiting merge. CI still billing-blocked (jobs don't start; verified locally). No local Python →
  `consistency_lint` hand-verified; this change touches only `Result.cs` / `ResultOfT.cs`, its test,
  `CHANGELOG.md`, this journal (+ its index row), and the ROADMAP checkpoint pointer — none of the lint
  invariants are affected.

## Next

- Continue the core `Result` invariants: #27 (`Success(null)` / implicit operator yields a success whose
  non-nullable `Value` is null — guard with `ThrowIfNull`), then #28 (implicit-operator collision for
  `Result<Error>` / `Result<object>` — a **breaking** operator removal on the frozen API → needs an ADR +
  a SemVer decision, like #67).
