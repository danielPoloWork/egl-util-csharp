# ADR-0035: Make PaginatedList reachable via a criteria-only evaluation and ToPaginatedListAsync

- **Status:** Accepted
- **Date:** 2026-07-15
- **Deciders:** Maintainer
- **Related:** ADR-0006 (persistence patterns), ADR-0025/0033 (additive SpecificationEvaluator/ISpecification), spec §5, #70

## Context

The library shipped three pagination pieces that did not connect (#70): `PaginatedList<T>` needs a
`totalCount`; `IRepository<TEntity,TId>` exposed only `ListAsync(ISpecification)` — no count or paged query;
and `SpecificationEvaluator.GetQuery` unconditionally applied `Skip`/`Take` when the specification enabled
paging, with **no criteria-only mode**. So a repository could not compute the total for a paged spec by
reusing the evaluator, and the `GetQuery` doc ("ready to enumerate or count") was misleading — counting the
returned query of a paged spec counts the page (≤ `Take`), not the matching set. `PaginatedList` was an
orphaned type in the frozen 1.0 API.

`IRepository` is a consumer-implemented interface with no concrete implementation in the library, so adding
`CountAsync`/`ListPagedAsync` to it would be a **source-breaking** change for every implementer (MAJOR).

## Decision

Make `PaginatedList` reachable through the evaluator with **purely additive** members (a MINOR), and leave
`IRepository` untouched:

- **`GetQuery(source, specification, bool evaluateCriteriaOnly)`** — an overload that, when
  `evaluateCriteriaOnly` is `true`, applies only the filter criteria (skipping includes, ordering, and
  paging), so the caller can count the full matching set. The two-argument overload delegates to it
  (`evaluateCriteriaOnly: false`), unchanged.
- **`ToPaginatedListAsync(source, specification, pageIndex, pageSize, ct)`** — counts the full match set (via
  the criteria-only query) and materializes the requested 1-based page (via the full query, then
  `Skip`/`Take`), returning a `PaginatedList<TEntity>`. It **owns** the paging, so the specification supplies
  the filter and a **required ordering** but must not enable its own paging (both are guarded); paging an
  unordered query is rejected, consistent with the evaluator's existing paged-but-unordered guard.
- Corrected the `GetQuery` two-argument `<returns>` doc: a paged query is already `Skip`/`Take`-sliced, so
  count with the criteria-only overload or page with `ToPaginatedListAsync`.

**Deferred:** adding `CountAsync`/`ListPagedAsync` to `IRepository`. It is source-breaking, so it is left to
the maintainer-assembled 2.0 (a consumer's repository can already expose paging by calling
`ToPaginatedListAsync`).

## Alternatives Considered

- **Add `CountAsync`/`ListPagedAsync` to `IRepository` now.** Rejected for this MINOR — source-breaking for
  every implementer; deferred to 2.0.
- **Default interface members on `IRepository`.** Rejected — a meaningful `CountAsync`/`ListPagedAsync`
  needs the data layer, so it cannot be defaulted on the interface.
- **Only the criteria-only overload + doc fix (no helper).** Rejected as insufficient — `PaginatedList`
  would still have no one-call path in the library.
- **Let the specification carry the paging (`Skip`/`Take`) and derive `pageIndex`/`pageSize`.** Rejected —
  `Skip` is an offset, not a page index; an explicit 1-based `pageIndex`/`pageSize` is unambiguous, and the
  no-self-paging guard prevents accidental double paging.

## Consequences

- `PaginatedList<T>` is reachable via the evaluator in one call, with a correct total (the match count, not
  the page size). The change is additive — existing `GetQuery` callers and `IRepository` implementers are
  unaffected; accrues toward the next MINOR (1.1.0). Spec §5 amended. Covered by tests: criteria-only
  ignores ordering/paging (InMemory + SQLite), `ToPaginatedListAsync` returns the page + full total, and the
  no-paging / required-ordering / positive-page guards.

## References

- Issue #70; ADR-0006/0025/0033; spec §5; `SpecificationEvaluator`, `PaginatedList<T>`, `IRepository`.
