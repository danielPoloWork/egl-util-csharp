# 2026-07-08 — Fix: reject null elements in Result.Failure / Result.Combine (#53)

Filed as `[Test]`, but locking the contract required a small code decision, so it ships as a `[fix]`
(PATCH, no public-API change) with regression tests and a CHANGELOG `Fixed` entry. No ADR — the fix is
the same fail-fast guard already applied for #29/#27/#85, not a design fork.

## Done

- **Bug:** `Result.Failure(params Error[])` and `Result.Combine(params Result[])` guarded the array
  (`ArgumentNullException.ThrowIfNull`) but not its **elements**. A null `Error` passed `Defensive`
  (`null == Error.None` is false) and leaked through the **non-nullable** `Result.Error` property,
  deferring a `NullReferenceException` to the consumer; `Combine` threw an opaque `NullReferenceException`
  when `SelectMany` dereferenced a null `Result` element (#53).
- **Decision (no AskUserQuestion):** unlike the other remaining Mediums (#31 frozen-API, #33 security,
  #25/#26 equality, #62 outage, #71 variance, #61 bridge), #53 has no genuine fork — `Result.Error` is
  typed non-nullable, so a null element is never a valid "locked contract"; the only defensible behavior
  is fail-fast, exactly as #29 rejects `Error.None` and #27 rejects null values (both applied directly).
  So I rejected null elements with `ArgumentException` rather than manufacture a question.
- **Fix:** `Defensive` now rejects a null `Error` element before the `Error.None` check; `Combine`
  rejects a null `Result` element before the `SelectMany`. Updated the XML docs (params + `<exception>`)
  on both `Failure` overloads and `Combine`.
- **Tests:** `Failure_rejects_a_null_error_element` (both overloads, single + mixed) and
  `Combine_rejects_a_null_results_array_and_a_null_element` (array null + single null + null-among-valid).
- **Verified:** build `-warnaserror` clean; `dotnet format` clean; **176 pass / 3 Redis skip** (+2).
  Mutation-checked — removing both guards (a clean-compiling mutation) fails the two new tests (the
  `Combine` NRE and the missing `Failure` throw), and the fix passes them.

## State

- v1.0.0 released. Production fix in `Result.cs` (+ tests + CHANGELOG `Fixed`); no public-API change
  (PATCH). PR drafted on `fix/result-null-element-guards`, awaiting merge. CI still billing-blocked
  (verified locally). No local Python → `consistency_lint` hand-verified; no new ADR (adr-index
  unchanged), README untouched (no i18n digest).

## Next

- The genuine design-decision `[fix]` Mediums remain, each needing an ADR + a maintainer AskUserQuestion:
  #31 (Specification `ThenBy` — additive to the frozen `ISpecification`), #33 (cache polymorphic static-`T`
  loss — `$type`/security), #25/#26 (Entity transient-identity / EF-proxy equality — property-tested),
  #62 (L2-outage contract), #71 (contravariant handler variance), #61 (`DomainEventNotification` runtime
  bridge).
