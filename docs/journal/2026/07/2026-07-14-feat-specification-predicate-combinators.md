# 2026-07-14 — Feat: Specification And/Or/Not combinators + IsSatisfiedBy (#68)

Roadmap PR 16 (refactor/API cluster — first item). Maintainer chose (AskUserQuestion) the full option: add
`And`/`Or`/`Not` + `IsSatisfiedBy` as default interface members. CHANGELOG `Added`; ADR-0033; spec §5 amended;
catalogue Specification row cites the new ADR. Additive MINOR (DIMs — existing implementers unaffected).

## Done

- **Gap (#68):** `ISpecification` advertised "composable" but had no combinators — `Criteria` was a single
  ctor-set predicate, and combining two specs needed hand-written expression rebinding.
- **Fix:** four default interface members on `ISpecification<TEntity>`:
  - `And`/`Or`/`Not` return a new predicate-only specification (an internal `CriteriaSpecification<TEntity>`
    exposed as `ISpecification`) whose `Criteria` is the combined predicate. A `null` `Criteria` = "match all"
    acts as the algebraic identity (And→other, Or→all, Not→none). Facets (includes/ordering/paging) are not
    combined.
  - Predicates are merged over a **single `ParameterExpression`** via a rebinding `ExpressionVisitor`
    (`SpecificationCriteria`), not `Expression.Invoke`, so the composed tree stays EF-translatable and flat.
  - `IsSatisfiedBy(entity)` evaluates the criteria in memory (compiles per call; documented to cache/override
    for hot loops).
- **Tests (18, `SpecificationCompositionTests`):** in-memory truth tables for And/Or/Not; null-criteria
  identities; guards (null `other`/`entity`); combined spec carries no facets; EF-translatable execution
  through `SpecificationEvaluator` on **InMemory + SQLite** (theory); and a structural guard that the combined
  `Criteria` has **no `InvocationExpression`** (a flat rebound tree).
- **Notable finding:** the EF-translatability theory alone did NOT distinguish the rebinding from
  `Expression.Invoke` — EF Core 8's SQLite provider now inlines `Invoke`, so both translate. Added the
  no-`Invoke` structural test as the real regression guard (mutation-verified: swapping in `Expression.Invoke`
  reddens it). The rebinding is kept for portability (older EF / other providers) per ADR-0033.
- New Application code passes the now-active analyzer gate (#166: AnalysisMode=Recommended). `dotnet build
  -warnaserror` + `dotnet format` clean; full suite **241 pass / 3 Redis skip** (+18).

## State

- v1.0.0 released. `ISpecification` is now the real Specification pattern (And/Or/Not + IsSatisfiedBy),
  additive via DIMs. CHANGELOG `Added`; ADR-0033 (+ index); spec §5 amended; catalogue updated. README
  untouched (only a generic "specifications" mention → no i18n churn). PR drafted on
  `feat/specification-combinators`, awaiting merge. CI billing-blocked (verified locally).

## Next

- Roadmap refactor/API cluster continues (**ASK each** — additive-MINOR or MAJOR): #69 (Result Map/Bind),
  #70 (PaginatedList reachability via IRepository/evaluator), #38 (split NuGet packages — MAJOR), #97 (adopt
  .NET 8 TimeProvider vs document), #95 (seal Result<TValue>). Then Docs/Low batches (#78+#79, #87+#89+#93,
  #50). Next up: **#69** — AskUserQuestion on the functional-combinator surface before implementing.
