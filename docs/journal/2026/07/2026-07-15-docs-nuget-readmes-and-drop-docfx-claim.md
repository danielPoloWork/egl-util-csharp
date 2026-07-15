# 2026-07-15 — Docs: package-specific NuGet READMEs (#80) + drop the unimplemented DocFX claim (#83)

The final two open backlog items, both Low: a packaging-docs fix and a governance-docs accuracy fix. Batched
as one PR (tight, jointly-verifiable, no code change). No CHANGELOG/ADR — repo/packaging docs, not the shipped
API surface (the NuGet README change is package metadata; verified by inspecting the packed nupkg).

## Done

- **#80 — broken NuGet README.** Both packages packed the repo `README.md`, whose repo-relative links
  (`docs/…`, `AGENTS.md`) and translation switcher render as **broken links on nuget.org**. Added a
  package-specific `PackageReadme.md` per project (core + MediatR) with **absolute github.com URLs**, the
  package's own install/quick-start, and no switcher; pointed each csproj's `PackageReadmeFile` + packed
  `None` at it (dropping the `$(RepoRoot)README.md` packing). The repo `README.md` is unchanged and keeps its
  relative links + switcher for GitHub. Maintainer chose (AskUserQuestion) a dedicated README per package.
  Verified by `dotnet pack`: each nupkg ships `PackageReadme.md` (nuspec `<readme>PackageReadme.md</readme>`)
  with **0 relative links** (all absolute). Timely — lands before the maintainer tags/publishes 2.0.0.
- **#83 — DocFX claim with no DocFX.** The quality bar advertised "DocFX builds without warnings" but no
  DocFX project/config/CI step exists. Maintainer chose (AskUserQuestion) to **drop the unfulfilled claim**
  (over building a DocFX site). Replaced the DocFX references in `AGENTS.md` §9/§10, `CLAUDE.md`,
  `docs/development/local-build.md`, and `docs/workflow/documentation.md` with the actual practice: public
  symbols carry XML-doc comments emitted via `GenerateDocumentationFile` and shipped in the NuGet package
  (verified debuggable/shipped in #66), plus Markdown narrative under `docs/`; there is no generated API-docs
  site.
- **Verified:** `dotnet build -warnaserror` clean; `dotnet format --verify-no-changes` clean; no `.cs` changed
  so the suite is unaffected (280 pass baseline). `consistency_lint` hand-traced: repo `README.md` untouched
  (i18n digest still matches), no ADR/patterns/spec-map/version/bundled-deps input changed — all checks hold.

## State

- v2.0.0 prepared and merged (PR #177). This PR drafted on `docs/nuget-readmes-and-drop-docfx-claim`
  (`docs` label, Maintenance milestone), awaiting merge; closes #80 and #83. CI billing-blocked → verified
  locally. **With this, all open issues are resolved** (the tracker had 2 stragglers, #80/#83, beyond the
  #19–#104 clusters — now addressed).

## Next

- **Backlog fully drained** (open issues: 0 after merge). Remaining is the maintainer's release mechanics for
  2.0.0 (merge #177 + this PR, clear Actions billing, tag `v2.0.0`, publish to NuGet) and the small
  post-publish follow-up to set `PackageValidationBaselineVersion=2.0.0`. Otherwise ordinary SemVer
  maintenance.
