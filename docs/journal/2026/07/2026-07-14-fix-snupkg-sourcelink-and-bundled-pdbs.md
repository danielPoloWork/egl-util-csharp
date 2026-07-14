# 2026-07-14 — Fix: snupkg SourceLink + bundled Domain/Application PDBs (#36, #66)

Roadmap PR 11 (build/packaging). Two coupled snupkg-debuggability fixes, tight batch. CHANGELOG `Fixed`
(packaging only; library API unchanged); no ADR (no design fork — followed the issues' recommended fixes).
Verified end-to-end by packing and inspecting the archives (dotnet is available locally).

## Done

- **#36 — snupkg had no SourceLink.** The package set `IncludeSymbols`/`SymbolPackageFormat=snupkg` but no
  SourceLink metadata, so symbols carried no source-URL document and no source was fetched. Added
  `Microsoft.SourceLink.GitHub` 8.0.0 (`PrivateAssets=all`) plus `PublishRepositoryUrl=true`,
  `EmbedUntrackedSources=true`, `DebugType=portable` in `Directory.Build.props` — applied to **all** projects
  so the bundled Domain and Application PDBs are source-linked too, not only Infrastructure.
- **#66 — snupkg omitted the bundled PDBs.** The `IncludeReferencedProjectsInPackage` target added only the
  referenced projects' `.dll`/`.xml` to `BuildOutputInPackage`, so the snupkg contained only
  `It.D4np.Util.Infrastructure.pdb` — Domain and Application (the core layers) were undebuggable via the
  symbol server. Added a `.pdb` line to the target; pack's extension filtering routes those PDBs into the
  `.snupkg` (and keeps them out of the `.nupkg`).
- **Verification (empirical).** `dotnet restore` regenerated the four `packages.lock.json` (SourceLink is a
  new build-time dep); `dotnet build -warnaserror` clean (SourceLink 8.0.0 did not conflict with the SDK);
  `dotnet pack -c Release` then:
  - the `.snupkg` contains **all three** PDBs (Infrastructure, Application, Domain);
  - the `.nupkg` ships the three DLLs + three XML docs and **no** PDBs;
  - `*.sourcelink.json` was generated for all three main projects, mapping the repo root to
    `https://raw.githubusercontent.com/danielPoloWork/egl-util-csharp/<commit>/*`.
  Full suite **223 pass / 3 Redis skip** — `LibraryVersionTests` still green, confirming the `+<sha>`
  metadata trim added to `LibraryVersion` in #159 correctly handles SourceLink's informational-version
  suffix. `dotnet format --verify-no-changes` clean.

## State

- v1.0.0 released. The symbol package now carries SourceLink and PDBs for all three shipped assemblies, so a
  consumer can step into Domain/Application/Infrastructure source from the symbol server. CHANGELOG `Fixed`;
  no ADR. Four lockfiles updated (SourceLink build-time dep). PR drafted on
  `fix/snupkg-sourcelink-and-bundled-pdbs`, awaiting merge. CI billing-blocked (verified locally, incl. a
  pack smoke check).

## Next

- Roadmap PR 12: **#37 + #73** — the release workflow should run tests/coverage/pack + assert the packed
  version matches the tag **before** drafting the release, and use curated `docs/releases/` notes rather than
  auto-generated text. Same workflow file, tight batch. CI/release change; verify by reading the workflow
  (GitHub Actions is billing-blocked, so no live run).
