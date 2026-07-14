# ADR-0033: Add And/Or/Not predicate combinators (and IsSatisfiedBy) to ISpecification

- **Status:** Accepted
- **Date:** 2026-07-14
- **Deciders:** Maintainer
- **Related:** ADR-0006 (persistence patterns — this extends the `ISpecification` surface), ADR-0025 (ThenBy, the same additive-DIM approach), spec §5, #68

## Context

`ISpecification<TEntity>` and the pattern catalogue advertised "a declarative, **composable** description of a
query" and "compose predicates declaratively", but the classical composability of the Specification pattern
was absent (#68): `Criteria` is a single `Expression<Func<TEntity, bool>>` settable only through the
constructor — no `And`/`Or`/`Not`, no in-memory `IsSatisfiedBy`. Two specifications could not be combined
without hand-writing expression-tree parameter rebinding, and the naive composition (`Expression.Invoke`)
historically failed EF Core query translation. As shipped, the type was a query-object base, not the
Specification pattern its docs claimed.

## Decision

Add predicate composition to `ISpecification<TEntity>` as **default interface members** (non-breaking, the
same additive approach as ADR-0025's `ThenBys`):

- **`And` / `Or` / `Not`** return a new specification whose `Criteria` is the combined predicate. They
  operate on the **filter predicate only** — a combined specification carries no includes, ordering, or
  paging (those are query-shaping facets orthogonal to boolean composition). A `null` `Criteria` means
  "match all" and behaves as the algebraic identity: `And` with an unfiltered side yields the other side;
  `Or` with an unfiltered side matches all; `Not` of unfiltered matches nothing.
- **EF-translatability:** predicates are merged over a **single** `ParameterExpression` via a rebinding
  `ExpressionVisitor` (`SpecificationCriteria`), not `Expression.Invoke`, so the composed tree is a flat
  expression that translates to SQL across providers and EF versions. The combined spec is an internal
  `CriteriaSpecification<TEntity>` exposed only as `ISpecification<TEntity>`.
- **`IsSatisfiedBy(entity)`** evaluates the criteria in memory (a `null` criteria matches everything). The
  default implementation compiles the criteria per call; its XML doc notes to cache/override for hot loops.

## Alternatives Considered

- **Document composition as out-of-scope** (amend the interface summary + `design-patterns.md`). Rejected —
  the maintainer chose to make the advertised pattern real rather than retract the claim.
- **`Expression.Invoke` composition.** Rejected as the mechanism — although EF Core 8 happens to inline it,
  it is not portable across providers/older versions and produces a nested tree. A flat rebound tree is the
  robust choice (asserted by a test that the combined `Criteria` contains no `InvocationExpression`).
- **Combining all facets** (union of includes, merged ordering). Rejected — And/Or/Not are boolean predicate
  algebra; includes/ordering/paging have no meaningful boolean composition, so a combined spec is
  predicate-only and the caller composes facets on the result.
- **Base-class methods instead of interface DIMs.** Rejected — DIMs make composition available to any
  `ISpecification` (including direct interface implementations), and are additive/non-breaking, consistent
  with ADR-0025.

## Consequences

- Specifications compose (`spec.And(other).Or(third)`, `spec.Not()`), and predicates can be evaluated in
  memory (`IsSatisfiedBy`) — the type is now the Specification pattern, not just a query object. Existing
  implementers are unaffected (default members); accrues toward the next MINOR (1.1.0). Spec §5 amended; the
  catalogue's Specification row cites this ADR. Covered by tests: in-memory truth tables, null-criteria
  identities, guards, EF-translatable execution on InMemory + SQLite, and the flat-tree (no-Invoke)
  structural guard.

## References

- Issue #68; ADR-0006/0025; spec §5; `ISpecification`, `Specification`, `SpecificationComposition`
  (`CriteriaSpecification` / `SpecificationCriteria`), `SpecificationEvaluator`.
