# ADR-0025: Add ThenBy/ThenByDescending secondary ordering to the Specification

- **Status:** Accepted
- **Date:** 2026-07-13
- **Deciders:** Maintainer
- **Related:** ADR-0006 (persistence patterns / Specification), ADR-0010 (1.0 API freeze — this is the first additive 1.x surface change), spec §2/§5

## Context

`ISpecification<TEntity>` exposed a single sort key — `OrderBy` **xor** `OrderByDescending` — with no
secondary key or tie-breaker. Paging a specification over a **non-unique** primary key therefore returned
rows in an undefined order across pages, the exact hazard the evaluator's "paging requires an ordering"
guard is meant to prevent (#31). EF Core cannot express a multi-column `ORDER BY` through a single
composite/anonymous key expression (it does not translate `OrderBy(x => new { … })` to multiple sort
columns), so a real `ThenBy` chain is the only way to get a deterministic secondary sort against a
relational provider.

## Decision

Add secondary ordering to the Specification pattern, additively (a MINOR change; the first since the 1.0
freeze):

- **`ISpecification<TEntity>` gains `ThenBys`** — an ordered `IReadOnlyList<(Expression<Func<TEntity,
  object>> KeySelector, bool Descending)>` of secondary sort keys, applied after the primary ordering. It
  is declared with a **default interface implementation** returning an empty list, so the change is
  **non-breaking for existing implementors** — they need not provide it. A bare named tuple keeps the new
  surface minimal (no new public type).
- **`Specification<TEntity>` gains `ApplyThenBy` / `ApplyThenByDescending`** protected helpers that append
  secondary keys; they require a primary ordering to have been applied first (else
  `InvalidOperationException`). Re-applying a primary ordering (`ApplyOrderBy` / `ApplyOrderByDescending`)
  clears any accumulated secondary keys, since they belonged to the replaced ordering.
- **`SpecificationEvaluator`** applies the primary ordering, then each `ThenBys` entry as
  `ThenBy`/`ThenByDescending` in order. Secondary keys are ignored when there is no primary ordering (they
  have nothing to chain onto); the existing paging-requires-an-ordering guard is unchanged.

## Alternatives Considered

- **Document the single-key limitation, no API change.** Rejected — it leaves multi-column ordering
  unavailable in EF and the paging-determinism hazard mitigated only by convention (callers remembering to
  order by a unique key).
- **Add `ThenBys` as a plain (non-default) interface member.** Rejected — it would be a source-breaking
  change for any external `ISpecification` implementor. The default interface method achieves the same
  surface without the break.
- **A composite/anonymous single key (`OrderBy(x => new { … })`).** Rejected — EF Core does not translate
  it to a multi-column sort.
- **A dedicated public `OrderingClause<TEntity>` record instead of a tuple.** Deferred — a named tuple with
  element names (`KeySelector`, `Descending`) is self-documenting and adds no new public type; a record can
  be introduced later without a breaking change if richer semantics are needed.

## Consequences

- Specifications can define a deterministic multi-key ordering, making paging over a non-unique primary key
  stable across pages.
- Additive and backward-compatible: existing specifications and external `ISpecification` implementors are
  unaffected (the default `ThenBys` is empty). Accrues toward the next MINOR (1.1.0).
- Amends the frozen public interface (spec §5) and the persistence functional requirements (spec §2), both
  updated; no behavior change for specifications that do not use `ThenBy`.

## References

- Issue #31; ADR-0006; ADR-0010 (API freeze); spec §2/§5; `ISpecification`, `Specification`, `SpecificationEvaluator`.
