# 2026-07-14 — Build: make the Roslyn analyzer gate real (#77)

Roadmap PR 15 (build/quality). CHANGELOG `Changed`; ADR-0032. Assessed the fallout empirically (dotnet
available locally) via staged builds, fixing or consciously opting out of each rule that fired.

## Done

- **The gate was vacuous (#77):** `EnableNETAnalyzers`/`AnalysisLevel=latest` were set, but `AnalysisMode`
  was default, `EnforceCodeStyleInBuild` unset, and every `.editorconfig` severity was `suggestion`, so
  `-warnaserror` promoted almost nothing (e.g. CA2007 off; an `EventBroker` `await using` disposed without
  `ConfigureAwait(false)`).
- **Turned it on for the shipped library:** `Directory.Build.props` sets `AnalysisMode=Recommended` +
  `EnforceCodeStyleInBuild=true`, **conditioned on `!$(MSBuildProjectName.EndsWith('Tests'))`** so the gate
  targets the library, not test code (xUnit underscore names / inline data would flood — ~400 CA1707 alone).
  Raised the `.editorconfig` naming rules `suggestion → warning`; enabled `CA2007=warning` for `src/main`.
- **Fixes (internal, behavior-preserving):**
  - CA1859 — `Result.Defensive` returns concrete `Error[]` (the ctor still re-wraps into a `ReadOnlyCollection`
    copy, so the read-only `Errors` view of #85 is intact).
  - CA2007 — both `EventBroker` `await using` sites dispose with `ConfigureAwait(false)`: the
    `CancellationTokenRegistration` (disposal-only → `await using var … .ConfigureAwait(false)`), and the
    per-event `AsyncServiceScope` (used below → disposed in the `finally` with `ConfigureAwait(false)`,
    preserving the #60 async-disposal and the original ordering).
- **Justified opt-outs (inline in `.editorconfig`):** CA1716 (`Error`) + CA1711 (`IEventHandler`) — frozen,
  idiomatic names; renaming is a breaking change and the rules only object on VB-keyword / reserved-suffix
  grounds. CA1848 (LoggerMessage delegates) — a hot-path optimization; the library logs only on rare
  exceptional paths, so the boilerplate buys nothing.
- **Verification (staged local builds):** each `dotnet build -warnaserror` surfaced the next batch of
  diagnostics (fail-fast per project); resolved to **0 warnings / 0 errors** on the whole solution.
  `dotnet format --verify-no-changes` clean (the raised naming rules don't flag the test names). Full suite
  **223 pass / 3 Redis skip** — the EventBroker scope-disposal restructure is behavior-preserving (#60 +
  #99 scope tests green). The gate is verified by the build itself (it enforces the recommended set + IDE
  style on the library and passes).

## State

- v1.0.0 released. The analyzer gate is real for the shipped library (Recommended CA set + IDE style rules +
  CA2007), with three deliberate, documented opt-outs; tests keep default analysis. CHANGELOG `Changed`;
  ADR-0032 (+ index). PR drafted on `build/enforce-analyzer-gate`, awaiting merge. Actions billing-blocked
  (verified locally). No public API/behavior change.

## Next

- Roadmap: the remaining backlog is the **Refactor/API (ASK each)** cluster — #68 (Specification And/Or/Not
  combinators), #69 (Result Map/Bind), #70 (PaginatedList reachability), #38 (split NuGet packages — MAJOR),
  #97 (adopt .NET 8 TimeProvider), #95 (seal Result<TValue>) — then the Docs/Low batches (#78+#79, #87+#89+#93,
  #50). Each refactor/API item is additive-MINOR or MAJOR → AskUserQuestion before implementing.
