# Local Build & Test

How to build, test, and check `egl-util-csharp` on your machine. CI runs the same commands
on Linux x64, Windows x64, macOS arm64 (.NET 8); reproducing them locally avoids a red round-trip.

## Prerequisites

- **C# 12 / .NET 8** toolchain.
- **Build system:** dotnet SDK (MSBuild), Directory.Build.props.
- **Package manager:** NuGet (packages.lock.json committed; CI restores in locked mode — see below).
- **Formatter / linter:** dotnet format / CSharpier, Roslyn analyzers + .editorconfig (warnings as errors).
- **Docs:** DocFX (for the API docs build).

## Commands

```bash
# Build
dotnet build -c Release

# Test
dotnet test -c Release

# Format check
dotnet format --verify-no-changes

# Lint
dotnet build -warnaserror
# Cross-artifact congruence (run before drafting any PR)
python tools/consistency_lint.py
```

## Locked-mode restore & lockfiles

CI restores with **locked mode** (`RestoreLockedMode`, enabled in `Directory.Build.props` when
`GITHUB_ACTIONS=true`; ADR-0014), so a `packages.lock.json` that drifts from the project's dependency
graph fails the build instead of being silently rewritten. Consequences for contributors:

- **If you add or change a dependency,** regenerate and commit the lockfile in the same PR:
  `dotnet restore --force-evaluate`.
- **If you add a new project,** commit its `packages.lock.json` too — the `consistency` CI job fails if
  any tracked `.csproj` sits in a directory without a committed lockfile (locked mode only catches
  *drift*, not a *missing* lockfile).
- **Reproduce the CI check locally** before pushing: `dotnet restore --locked-mode` (it fails if any
  lockfile is out of sync). Local builds otherwise restore unlocked, so day-to-day work is unaffected.

## Before you open a PR

1. `dotnet format --verify-no-changes` and `dotnet build -warnaserror` are clean.
2. `dotnet test -c Release` passes; new/changed behavior is covered. CI enforces **≥ 80% line coverage**
   (coverlet.msbuild; ADR-0016) — reproduce it with
   `dotnet test -c Release /p:CollectCoverage=true /p:Threshold=80 /p:ThresholdType=line /p:ThresholdStat=total`.
3. Nullable reference types + Roslyn analyzers (soundness); dotnet-counters (leaks) are green where applicable.
4. `dotnet restore --locked-mode` passes (regenerate a changed lockfile with `dotnet restore --force-evaluate`).
5. `python tools/consistency_lint.py` passes.
6. The relevant docs (README, ROADMAP, ADRs, patterns, changelog) are updated in the same
   PR — see [`../workflow/documentation.md`](../workflow/documentation.md).
