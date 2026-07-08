# 2026-07-08 — Fix: ValidationPipelineBehavior shared-ValidationContext race (#52)

A real correctness fix uncovered while writing the #52 coverage test. Filed as `[Test]` but turned out
to be a live data race, so it ships as a `[fix]` (PATCH, no public-API change) with a regression test and
a CHANGELOG `Fixed` entry. No ADR — the maintainer chose the fix that restores the intended behavior
without changing the concurrency model.

## Done

- **Bug:** `ValidationPipelineBehavior.Handle` validated via `Task.WhenAll` over a **single**
  `ValidationContext<TRequest>` shared by every validator. A `ValidationContext` is not safe for
  concurrent use, so validators doing genuinely-async work (e.g. `MustAsync` that awaits) overlapped and
  corrupted each other's failures through the context's shared state — the aggregated failures were
  nondeterministically lost or duplicated. Synchronous validators complete `ValidateAsync` without
  overlapping, so the bug was latent in the common case but live for async validators (#52).
- **Proof:** the regression test (k=8 gated `MustAsync` validators released by a barrier so they truly
  overlap) failed nondeterministically against the shared-context code — all k failures collapsed onto one
  validator's message, a different one each run; a per-validator context passed 3/3.
- **Fix (maintainer chose "fresh context per validator", AskUserQuestion):** `Handle` now constructs a new
  `ValidationContext<TRequest>(request)` inside the `Select`, one per validator, keeping the parallel
  `Task.WhenAll`. One-line change, idiomatic, preserves the intended concurrency; each validator is
  isolated. Added a `<remarks>` on `Handle` documenting the per-context contract.
- **Verified:** regression test red pre-fix (nondeterministic), green post-fix. `dotnet build
  -warnaserror` clean; `dotnet format` clean; **174 pass / 3 Redis skip** (+1).
- **Review:** a 3-lens adversarial pass (fix-correctness / FluentValidation-semantics / test-quality) — all
  three SHIP. Confirmed the fix changes no observable behavior beyond removing the race (each `ValidateAsync`
  returns its own result; the behavior builds contexts internally from the request, so nothing relied on
  validators sharing `RootContextData`). Took the one nit: a race regression test's *failing* direction is
  probabilistic, so the test now repeats the scenario over **32 rounds** (fresh counter/barrier/validators
  each round) — the pass direction stays deterministic and a future re-regression to a shared context is
  caught with near-certainty.

## State

- v1.0.0 released. Production fix in `ValidationPipelineBehavior.cs` (+ regression test + CHANGELOG
  `Fixed`); no public-API change (PATCH, accrues to the next release). PR drafted on
  `fix/validation-shared-context-race`, awaiting merge. CI still billing-blocked (verified locally). No
  local Python → `consistency_lint` hand-verified; no new ADR (adr-index unchanged), README untouched (no
  i18n digest). Touches the behavior, its test, `CHANGELOG.md`, this journal (+ index row), and the ROADMAP
  pointer.

## Next

- The audit backlog's zero-/low-risk tiers are essentially drained. Remaining are the design-decision
  `[fix]` Mediums, each needing an ADR + a maintainer AskUserQuestion: #31 (Specification `ThenBy` — adds
  to the frozen `ISpecification`), #33 (cache polymorphic static-`T` loss — `$type`/security), #25/#26
  (Entity identity / EF-proxy equality — property-tested), #62 (L2-outage contract), #71 (contravariant
  handler variance), #61 (`DomainEventNotification` runtime-typed bridge). #53 (Combine NREs on a null
  element) needs a code decision too.
