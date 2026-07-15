# 2026-07-15 — Refactor: seal Result<TValue> (#95)

Roadmap PR 19 (refactor/API cluster). Maintainer chose (AskUserQuestion) **seal it** (over promoting the ctor
to protected). CHANGELOG `Changed`; no ADR (documents existing reality — non-breaking), no spec amendment
(signature unchanged).

## Done

- **#95:** `Result<TValue>` was `public` but unsealed, yet its only constructor is `internal` — so no
  consumer could derive from it (de-facto sealed), while every other concrete public type is `sealed`.
  Declared it `sealed`, documenting the reality; the base non-generic `Result` keeps its `protected`
  constructor as the single deliberate extension point. Verified no subclass exists in the library (grep),
  so it is **non-breaking** (derivation was never possible — source or binary).
- **Test:** `Result_of_T_is_sealed` (`typeof(Result<>).IsSealed`) — a structural guard against accidentally
  un-sealing. Non-vacuous by construction.
- `dotnet build -warnaserror` + `dotnet format` clean; full suite **264 pass / 3 Redis skip** (+1).

## State

- v1.0.0 released. `Result<TValue>` is sealed (non-breaking); the extensibility contract is now consistent
  (base `Result` open, valued result closed). CHANGELOG `Changed`; no ADR/spec change. PR drafted on
  `refactor/seal-result-of-t`, awaiting merge. CI billing-blocked (verified locally). README untouched.

## Next

- Roadmap refactor/API cluster (dispositions decided 2026-07-15):
  - PR 20: **#97** — TimeProvider interop (MINOR): register `TimeProvider.System` in DI; `DateTimeProvider`
    delegates to an injected `TimeProvider`; document `IDateTimeProvider` as a thin facade. Non-breaking.
  - PR 21: **#38** — split the MediatR/FluentValidation-coupled types into an opt-in package (MAJOR/2.0);
    large, breaking; will need its own sub-plan (package layout) + likely more asks.
  - Then Docs/Low batches (#78+#79, #87+#89+#93, #50).
