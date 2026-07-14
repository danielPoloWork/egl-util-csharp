# ADR-0032: Make the Roslyn analyzer gate real (AnalysisMode + EnforceCodeStyleInBuild)

- **Status:** Accepted
- **Date:** 2026-07-14
- **Deciders:** Maintainer
- **Related:** ADR-0016 (coverage gate), the quality bar in AGENTS.md §10, #77

## Context

The quality bar (AGENTS.md §10) promises "Roslyn analyzers + .editorconfig (warnings as errors) clean", but
the gate was mostly vacuous (#77): `Directory.Build.props` set `EnableNETAnalyzers=true` and
`AnalysisLevel=latest` but left `AnalysisMode` at its default (only the small default-enabled CA subset ran),
never set `EnforceCodeStyleInBuild` (so no `IDExxxx` style rule could fail `dotnet build -warnaserror`), and
every `.editorconfig` severity was `suggestion`. `TreatWarningsAsErrors=true` therefore promoted almost
nothing — e.g. `CA2007` (the ConfigureAwait discipline the library otherwise follows everywhere) was off, and
an `await using` in `EventBroker` awaited disposal without `ConfigureAwait(false)`.

## Decision

Turn the gate on for the **shipped library** and fix or consciously opt out of what it surfaces:

- `AnalysisMode=Recommended` and `EnforceCodeStyleInBuild=true` in `Directory.Build.props`, **conditioned on
  non-`*Tests` projects** (`!$(MSBuildProjectName.EndsWith('Tests'))`). Test code follows looser, idiomatic
  conventions (xUnit underscore method names, inline test data) that these rules flag as noise; the gate
  targets the library whose SemVer promise it protects.
- Raise the `.editorconfig` naming rules from `suggestion` to `warning` (the library already complies), and
  enable `CA2007=warning` scoped to `src/main`.
- **Fixes:** `Result.Defensive` returns the concrete `Error[]` (CA1859; the constructor still re-wraps it in
  a read-only copy, so the read-only `Errors` view of ADR/​#85 is intact); both `EventBroker` `await using`
  sites now dispose with `ConfigureAwait(false)` (CA2007).
- **Justified opt-outs** (documented inline in `.editorconfig`): `CA1716` (`Error`) and `CA1711`
  (`IEventHandler`) — deliberate, idiomatic names on the frozen 1.0 surface; renaming would be a breaking
  change and the rules only object on cross-language / reserved-suffix grounds. `CA1848` ("use LoggerMessage
  delegates") — a hot-path logging optimization; the library logs only on rare exceptional paths, so the
  source-generated-delegate boilerplate buys nothing.

## Alternatives Considered

- **`AnalysisMode=All`.** Rejected for now — `All` enables every CA rule (localization, globalization,
  defensive-arg rules) that would demand many low-value suppressions. `Recommended` is the curated,
  high-signal set; `All` can be revisited later.
- **Strict gate on all projects (including tests).** Rejected — it floods on ~400 xUnit underscore method
  names (CA1707) and inline test data (CA1861), none of which are defects. Scoping to the library keeps the
  gate high-signal.
- **Rename `Error`/`IEventHandler` to satisfy CA1711/CA1716.** Rejected — a breaking change to the frozen
  surface for opinionated naming rules; the names are idiomatic for their patterns.
- **Adopt `LoggerMessage` source-generated logging (satisfy CA1848).** Rejected as unwarranted boilerplate
  for rare error-path logging; reconsider if hot-path logging is ever added.

## Consequences

- `dotnet build -warnaserror` on the library now enforces the recommended CA set + the IDE style rules at
  their `.editorconfig` severity, so the quality bar in §10 is real rather than aspirational. Test projects
  keep the default analysis. `dotnet format --verify-no-changes` stays green.
- Three rules are deliberately off, each with an inline rationale, so the opt-outs are visible and auditable
  rather than silent. No public API or runtime-behavior change (the CA1859/CA2007 fixes are internal and
  behavior-preserving; the full suite is unchanged at 223 pass / 3 Redis skip).

## References

- Issue #77; ADR-0016; `Directory.Build.props`, `.editorconfig`, `EventBroker.cs`, `Result.cs`.
