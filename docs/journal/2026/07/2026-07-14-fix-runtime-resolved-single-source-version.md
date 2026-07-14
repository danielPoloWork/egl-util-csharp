# 2026-07-14 — Fix: runtime-resolved, single-source library version (#48, #86) — breaking (2.0)

Roadmap PR 10 (build/packaging + api-design). Fixes #48 and #86 **together** — the maintainer chose (via
AskUserQuestion) to do both now as one 2.0-tagged change, since the clean #86 fix (resolve `Version` at
runtime from the assembly) also removes #48's hand-duplication. CHANGELOG `Changed` **BREAKING**; ADR-0030;
no frozen-spec amendment (`LibraryVersion` is not in the frozen spec surface).

## Done

- **#48 — single-source the version.** Moved `<Version>1.0.0</Version>` into `Directory.Build.props` (applied
  to every assembly; the SDK stamps AssemblyVersion/FileVersion/InformationalVersion from it and the packable
  project inherits it as its NuGet `<Version>`). Removed the duplicated `<Version>` from the Infrastructure
  csproj. There is no longer a hand-maintained literal in two places.
- **#86 — resolve the version at runtime.** `LibraryVersion.Version` changed from `public const string`
  (compile-time inlined into consumers → stale after an in-place upgrade) to a `public static string { get; }`
  resolved once from `typeof(LibraryVersion).Assembly`'s `AssemblyInformationalVersionAttribute` (trimming any
  `+build.metadata`), with the assembly version as a fallback. Because only the packable project set
  `<Version>` before, the Domain assembly that hosts `LibraryVersion` wasn't even stamped with the real
  version — moving `<Version>` to props fixes that too, so the runtime read returns the true version.
- **Lint.** `version-lockstep` now reads the single `<Version>` from `Directory.Build.props`
  (`version_file` + `<Version>(…)</Version>` regex) and drops `package_version_file` (the package inherits
  the source, so there is no second literal to cross-check). Docstring updated.
- **Breaking (2.0):** `const → property` changes IL from `ldstr` to a getter call — a binary break; a
  consumer compiled against the 1.x const must recompile, and `Version` can no longer sit in a `const`
  context. Accrues toward the maintainer-assembled 2.0 (with ADR-0020/0027/0028). ADR-0030 records it.
- **Tests / verification.** `LibraryVersionTests`: kept `Version == "1.0.0"` (proves the runtime read yields
  the right value — Domain stamped from props) and added a structural guard that `Version` is a **property,
  not a const field** (`GetProperty` non-null, `GetField` null) — mutation-verified (reverting to `const`
  reddens it). `dotnet build -warnaserror` + `dotnet format` clean; full suite **223 pass / 3 Redis skip**
  (+1). `dotnet pack` produces `It.D4np.Util.1.0.0.nupkg` — package version correctly inherited from props
  (the csproj no longer pins it). Lint hand-traced (no local Python): source `1.0.0` == README badge ==
  changelog v1.0.0 == release v1.0.0; `package_version_file=None` skipped cleanly.

## State

- v1.0.0 released. Version is single-sourced in `Directory.Build.props` and `LibraryVersion.Version` is
  runtime-resolved — a 2.0-bound breaking change. CHANGELOG `Changed` **BREAKING**; ADR-0030 (+ index). PR
  drafted on `fix/runtime-resolved-single-source-version`, awaiting merge. CI billing-blocked (verified
  locally); no local Python → lint hand-verified. README untouched (no i18n churn).

## Next

- Roadmap PR 11: **#36 + #66** — snupkg SourceLink + PDBs for the bundled Domain/Application assemblies, so
  the shipped symbol package steps into all three layers. Packaging, tight batch. (Note: once SourceLink is
  enabled, the SDK appends `+<git-sha>` to InformationalVersion — already handled by `LibraryVersion`'s
  metadata trim added here.)
