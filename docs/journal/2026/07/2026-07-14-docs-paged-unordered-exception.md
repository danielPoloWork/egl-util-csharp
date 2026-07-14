# 2026-07-14 — Docs: document the paged-but-unordered exception (#81)

Back to the zero-production-risk docs-accuracy tier. No code/behavior change, so no CHANGELOG and no ADR.

## Done

- **Gap:** `SpecificationEvaluator.GetQuery` throws `InvalidOperationException` when a specification enables
  paging without a primary ordering (paging an unordered query is non-deterministic across pages), but no
  XML contract documented that throw (#81).
- **Fix (docs-only):** added an `<exception cref="InvalidOperationException">` to `GetQuery`, and a note to
  `ISpecification.IsPagingEnabled` that paging requires a primary ordering (else the evaluator rejects it).
  Both ends of the contract now document the behavior. `dotnet build -warnaserror` clean (the BCL cref
  resolves); `dotnet format` clean; suite unchanged.

## State

- v1.0.0 released (with a 2.0 reshape in flight, PR #148 merged). Docs-only; no behavior/API change. PR
  drafted on `docs/paged-unordered-exception`, awaiting merge. CI still billing-blocked (verified locally).
  No local Python → `consistency_lint` hand-verified; touches `SpecificationEvaluator.cs` +
  `ISpecification.cs` (doc comments), this journal (+ index row), and the ROADMAP pointer.

## Next

- **The audit tracker still has ~41 open issues** (I mis-stated it as drained — only the design-decision
  `[fix]` forks I had enumerated were done). Remaining, safest-first: docs-accuracy [Low] (#78, #79, #87,
  #89, #93, #82, #83), then small `[fix]` [Low] (#43, #44, #49, #72, #74, #76, #86, #88, #90, #91, #92),
  security [Low] (#40, #41, #84), then the Medium build/ci/refactor/security clusters (#35–#38, #63–#70,
  etc.). Several are natural pairs (e.g. the lint-script fixes #72/#74/#82) that could batch.
