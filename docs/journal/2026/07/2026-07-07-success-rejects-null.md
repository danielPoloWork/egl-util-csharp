# 2026-07-07 — Fix: Result.Success rejects a null value (#27)

Eighth fix from the Medium/Low audit tier; the second of the three core `Result` invariants (#29 done →
this #27 → #28 next). Another self-contained validation guard — no signature change, no ADR.

## Done

- **Bug:** `Result.Success<TValue>(value)` and the implicit `TValue` → `Result<TValue>` operator were
  unguarded (the internal ctor stores `value!`), so `Result<string> r = null;` (or a nullable-oblivious
  generic caller) produced `IsSuccess == true` with `r.Value == null` — deferring a
  `NullReferenceException` far downstream even though `Value` is typed non-nullable and the type's premise
  is "no nulls". Nullable-reference-types + warnings-as-errors catches the obvious literal path but not
  generic/nullable-oblivious callers (#27).
- **Fix:** `Success<TValue>` now throws `ArgumentNullException` when `value is null`. Because the implicit
  value operator routes through `Success`, `Result<string> r = someNullString;` throws too. The guard
  uses `if (value is null)` rather than `ArgumentNullException.ThrowIfNull(value)` deliberately:
  `ThrowIfNull` takes `object?` and would **box every value-type success** (`Success(42)`) — an
  allocation on the happy path of an allocation-light type (ADR-0005). `value is null` is allocation-free
  (the JIT elides the box for value-type instantiations), still rejects a null `Nullable<T>`, and emits
  no always-false warning for the unconstrained `TValue`. No API/signature change → no ADR (like #34/#29).
- **Scope check:** nothing in `src/main` calls `Success<TValue>` except the implicit operator itself, and
  the existing tests never pass `null` to `Success`, so nothing breaks. Value types are unaffected
  (`Success(0)` → `0 is null` is false).
- **Test:** `Success_rejects_a_null_value` covers the factory (`Success<string>(null!)`), a null
  `Nullable<T>` (`Success<int?>(null)`), and the implicit-operator path (a typed null reference, since the
  bare `null` literal would bind to the reference type and bypass the operator). Verified red pre-fix
  (no throw), green post-fix. `dotnet build -warnaserror` clean; `dotnet format` clean; 150 pass / 2 Redis
  skip.
- **Docs:** CHANGELOG `[Unreleased]` → Fixed; `<param>`/`<exception>` tags on `Success<TValue>` and the
  implicit operator.

## State

- v1.0.0 released. No public surface change → patch-level. PR drafted on `fix/success-rejects-null`,
  awaiting merge. CI still billing-blocked (jobs don't start; verified locally). No local Python →
  `consistency_lint` hand-verified; touches only `Result.cs` / `ResultOfT.cs`, its test, `CHANGELOG.md`,
  this journal (+ index row), and the ROADMAP checkpoint pointer — no lint invariant affected.

## Next

- #28 (implicit-operator collision: `implicit operator Result<TValue>(TValue)` vs
  `implicit operator Result<TValue>(Error)` — `Result<Error>` can't be built from an `Error`, and
  `Result<object> r = someError;` silently binds the Error overload → a *failure*). This is a **breaking**
  public-operator change on the frozen v1.0 API → needs an ADR + a SemVer decision (ask the maintainer,
  as with #67).
