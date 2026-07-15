# 2026-07-15 — Feat: Result functional combinators + non-throwing access (#69)

Roadmap PR 17 (refactor/API cluster). Maintainer chose (AskUserQuestion) the **core sync** set,
**sync-only**. CHANGELOG `Added`; ADR-0034; spec §2 amended. Additive MINOR (instance members — existing
code unaffected).

## Done

- **Gap (#69):** the "functional, exception-free" Result had no composition and only a throwing `Value`
  accessor — every chain degenerated into imperative `if (r.IsFailure) …; var v = r.Value;`, and the sole
  value-extraction path threw (contradicting "no exceptions for expected control flow").
- **Added (sync, instance members, Domain assembly):**
  - `Result<TValue>`: `Match<TResult>(onSuccess, onFailure)` (fold), `Map<TNew>` (functor map — errors
    propagated on failure, delegate runs only on success), `Bind<TNew>` (monadic chain), and non-throwing
    `GetValueOrDefault()` / `GetValueOrDefault(fallback)` / `TryGetValue(out value)`.
  - `Result` (non-generic): `Match<TResult>(onSuccess, onFailure)`.
  - `Map` upholds the no-null invariant (a map returning null fails the `Success` guard, #27); the safe
    accessors never throw for an expected failure.
- **Scope (per maintainer):** core sync only — no `Tap`/`OnSuccess`/`OnFailure`, no typed `Combine<T>`/zip,
  no `async`/`Task<Result<T>>` overloads (deferred to a later MINOR).
- **Tests (15, `ResultCompositionTests`):** Match success/failure; Map transform + error-propagation +
  not-invoked-on-failure + no-null guard; Bind chain/fail/propagate + not-invoked; GetValueOrDefault (value /
  default / fallback); TryGetValue (true+value / false+default); safe accessors never throw on failure;
  null-delegate guards; non-generic Match fold + guards. Behavioral assertions (concrete outputs +
  invoked-flags), non-vacuous by construction.
- New Domain code passes the analyzer gate (#166). `dotnet build -warnaserror` + `dotnet format` clean; full
  suite **256 pass / 3 Redis skip** (+15).

## State

- v1.0.0 released. `Result`/`Result<TValue>` now compose (`Match`/`Map`/`Bind`) and offer exception-free
  access (`GetValueOrDefault`/`TryGetValue`) — the "functional, exception-free" branding is fulfilled, so no
  README/spec claim needs softening. CHANGELOG `Added`; ADR-0034 (+ index); spec §2 amended. README untouched
  (only a generic Result mention → no i18n churn). PR drafted on `feat/result-functional-combinators`,
  awaiting merge. CI billing-blocked (verified locally).

## Next

- Roadmap refactor/API cluster (**ASK each**): #70 (PaginatedList reachability via IRepository/evaluator),
  #38 (split NuGet packages — MAJOR), #97 (adopt .NET 8 TimeProvider vs document), #95 (seal Result<TValue> —
  minor). Then Docs/Low batches (#78+#79, #87+#89+#93, #50). Next up: **#70** — AskUserQuestion before
  implementing.
