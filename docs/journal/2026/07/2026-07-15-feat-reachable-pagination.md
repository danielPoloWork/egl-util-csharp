# 2026-07-15 — Feat: make PaginatedList reachable (criteria-only + ToPaginatedListAsync) (#70)

Roadmap PR 18 (refactor/API cluster). Maintainer chose (AskUserQuestion) **non-breaking now; defer
IRepository**. CHANGELOG `Added`; ADR-0035; spec §5 amended. Additive MINOR.

## Done

- **Gap (#70):** three disconnected pagination pieces — `PaginatedList<T>` needs a `totalCount`,
  `SpecificationEvaluator.GetQuery` always applied `Skip`/`Take` (no criteria-only mode to count the full
  set), and `IRepository` had no count/paged method. `PaginatedList` was orphaned; the `GetQuery` doc
  ("ready to enumerate or count") was misleading (counting a paged query gives the page size).
- **Fix (additive, Infrastructure):**
  - `GetQuery(source, spec, bool evaluateCriteriaOnly)` — criteria-only mode applies just the filter Where
    and returns before includes/ordering/paging, so a caller can count the full matching set. The 2-arg
    overload delegates to it unchanged.
  - `ToPaginatedListAsync(source, spec, pageIndex, pageSize, ct)` — counts via the criteria-only query,
    materializes the 1-based page via the full query + `Skip`/`Take`, returns `PaginatedList<T>`. It owns the
    paging, so the spec supplies filter + a required ordering but must NOT enable its own paging (both
    guarded, mirroring the evaluator's paged-but-unordered guard).
  - Corrected the misleading `GetQuery` `<returns>` doc.
- **Deferred (per maintainer):** `IRepository` is a consumer-implemented interface with no concrete impl in
  the library, so adding `CountAsync`/`ListPagedAsync` would be **source-breaking** → left to the
  maintainer-assembled 2.0. A consumer's repository already composes paging by calling `ToPaginatedListAsync`.
- **Tests (7, in `SpecificationEvaluatorTests`):** criteria-only ignores ordering/paging (full=2 vs
  criteria-only=4, InMemory + SQLite; mutation-verified — removing the short-circuit reddens it);
  `ToPaginatedListAsync` returns the page + the FULL total (not the page size) with correct
  HasNext/HasPrevious/TotalPages across two pages; and the no-paging / required-ordering / positive-page
  guards.
- New Infrastructure code passes the analyzer gate (#166; `ConfigureAwait(false)` on the async EF calls).
  `dotnet build -warnaserror` + `dotnet format` clean; full suite **263 pass / 3 Redis skip** (+7).

## State

- v1.0.0 released. `PaginatedList` is reachable via the evaluator (criteria-only overload + async paged
  helper); `IRepository` unchanged (repo-level paging members deferred to 2.0). CHANGELOG `Added`; ADR-0035
  (+ index); spec §5 amended. README untouched (SpecificationEvaluator/PaginatedList aren't in the README
  surface → no i18n churn). PR drafted on `feat/reachable-pagination`, awaiting merge. CI billing-blocked
  (verified locally).
- **2.0 follow-up tracked:** add `CountAsync(spec)` + `ListPagedAsync(spec, page, size)` to `IRepository`
  at the 2.0 cut (deferred half of #70).

## Next

- Roadmap refactor/API cluster (**ASK each**): #38 (split NuGet packages — MAJOR), #97 (adopt .NET 8
  TimeProvider vs document), #95 (seal Result<TValue> — minor). Then Docs/Low batches (#78+#79, #87+#89+#93,
  #50). Next up: **#97 or #95** (smaller) — AskUserQuestion before implementing; #38 is a MAJOR packaging
  split likely for the 2.0 line.
