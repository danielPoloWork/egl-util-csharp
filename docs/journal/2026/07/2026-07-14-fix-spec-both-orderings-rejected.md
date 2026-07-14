# 2026-07-14 — Fix: SpecificationEvaluator rejects both-orderings specs (#88)

Roadmap PR 8 (correctness cluster). Standalone evaluator guard + interface-contract doc. CHANGELOG `Fixed`
(behavior change); no ADR, no frozen-spec amendment (the spec §5 already describes the primary ordering as
"a primary key" — singular — so the guard enforces the existing contract). Proceeded without asking: the fix
mirrors an existing guard and touches no public signature.

## Done

- **Gap:** `SpecificationEvaluator.GetQuery` applied `OrderBy` and only fell through to `OrderByDescending`
  when `OrderBy` was null. The `Specification` base class enforces mutual exclusivity
  (`ApplyOrderBy`/`ApplyOrderByDescending` reset each other), but the public `ISpecification<TEntity>`
  interface — a supported, assembly-scanned extension point — carried no such contract. A direct
  implementation that set **both** got its descending ordering **silently dropped**, yielding
  wrongly-ordered (and, when paged, wrongly-sliced) results with no diagnostic — inconsistent with the
  evaluator already throwing for the analogous paged-but-unordered state (#88).
- **Fix:** added a guard in `GetQuery` — if both `OrderBy` and `OrderByDescending` are non-null, throw
  `InvalidOperationException` (mirroring the paged-unordered guard, placed with the ordering logic). Expanded
  the method's `<exception>` doc to cover both invalid ordering states, and documented the exclusivity on
  `ISpecification.OrderBy`/`OrderByDescending` (base class enforces it automatically; a direct implementation
  must uphold it; use `ThenBys` for secondary keys).
- **Test:** `Supplying_both_orderings_is_rejected` — a direct `ISpecification<Product>` double
  (`BothOrderingsSpecification`, bypassing the base class) sets both orderings; asserts
  `InvalidOperationException`. Mutation-verified: disabling the guard (`if (false && …)`) makes it apply
  `OrderBy` and return a valid query → no throw → test fails.
- `dotnet build -warnaserror` clean; `dotnet format --verify-no-changes` clean; full suite **222 pass / 3
  Redis skip** (+1 new).

## State

- v1.0.0 released. `SpecificationEvaluator` now rejects the ambiguous both-orderings state loudly (behavior
  change: only a direct `ISpecification` impl that set both — already producing wrong results — is affected;
  base-class specs can't reach it). CHANGELOG `Fixed`; no ADR/spec change. PR drafted on
  `fix/spec-both-orderings-rejected`, awaiting merge. CI billing-blocked (verified locally). No local Python
  → `consistency_lint` hand-verified; README untouched (no i18n churn).

## Next

- Roadmap PR 9: **#72 + #74 + #82 + #96** — four `tools/consistency_lint.py` fixes (bidirectional
  milestone checks, i18n file-exists, `check_patterns` Planned rows, dead `git()` helper). Same file, tight
  batch. NOTE: no local Python → hand-verify the script changes by reading, not running.
