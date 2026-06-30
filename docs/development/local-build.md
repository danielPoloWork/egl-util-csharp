# Local Build & Test

How to build, test, and check `egl-util-csharp` on your machine. CI runs the same commands
on Linux x64, Windows x64, macOS arm64 (.NET 8); reproducing them locally avoids a red round-trip.

## Prerequisites

- **C# 12 / .NET 8** toolchain.
- **Build system:** dotnet SDK (MSBuild), Directory.Build.props.
- **Package manager:** NuGet (packages.lock.json committed).
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

## Before you open a PR

1. `dotnet format --verify-no-changes` and `dotnet build -warnaserror` are clean.
2. `dotnet test -c Release` passes; new/changed behavior is covered (≥ 80% line).
3. Nullable reference types + Roslyn analyzers (soundness); dotnet-counters (leaks) are green where applicable.
4. `python tools/consistency_lint.py` passes.
5. The relevant docs (README, ROADMAP, ADRs, patterns, changelog) are updated in the same
   PR — see [`../workflow/documentation.md`](../workflow/documentation.md).
