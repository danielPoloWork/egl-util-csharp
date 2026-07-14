# 2026-07-14 — CI: pin setup-dotnet to a SHA + quality-job timeout (#84, #75)

Roadmap PR 13 (build/CI hygiene) — **split**. The roadmap batched #47 + #84 + #75, but reading the issues
showed #47 (CodeQL + dependency-review + SBOM) is a substantive multi-part supply-chain feature — a new
workflow file plus a change to the sensitive release/pack path — materially different from these two
one-line hardening tweaks. Per the maintainer's directive (keep bigger/riskier interventions as separate
PRs; re-triage as issues close), this PR ships the trivial, fully read-verifiable hardening (#84, #75) and
#47 becomes its own focused PR next. CHANGELOG `Changed` (repo/CI only); no ADR. Actions is billing-blocked,
so verified by reading + grep (no live run).

## Done

- **#84 — pin `actions/setup-dotnet`.** It was the only action referenced by a mutable tag (`@v5`) while
  `actions/checkout`, `actions/setup-python`, and `softprops/action-gh-release` are all SHA-pinned with a
  `# vX.Y.Z` comment. A moved/compromised `v5` tag would run arbitrary code in every CI run — and inside
  `release.yml`, whose `GITHUB_TOKEN` has `contents: write` (release forgery / content tampering). Resolved
  the current `v5` commit via the GitHub API (`26b0ec14cb23fa6904739307f278c14f94c95bf1`, = v5.4.0) and
  pinned all **four** call sites (`ci.yml` ×2, `release.yml` ×2) as `@26b0ec14… # v5.4.0`. Dependabot's
  github-actions group (`.github/dependabot.yml`) maintains it automatically.
- **#75 — `quality` job timeout.** `build` sets `timeout-minutes: 30` and `consistency` sets 15, but
  `quality` (format + analyzers + coverage) set none, so a hung `dotnet test --collect` or wedged restore
  would hold a runner for GitHub's 6-hour default — and since the concurrency group only cancels superseded
  PR runs, a hang on a main push blocks required-check feedback for the whole window. Added
  `timeout-minutes: 30`, matching the build job.
- **Verification (no live Actions).** `grep` confirms all four `setup-dotnet` references are SHA-pinned and
  no `@v5` remains; `ci.yml` now has three `timeout-minutes` (build 30, consistency 15, quality 30). No
  .NET/source changes — build/suite untouched. The `setup-dotnet` SHA was resolved from
  `repos/actions/setup-dotnet` (not guessed).

## State

- v1.0.0 released. Every referenced action is now SHA-pinned and every CI job is time-bounded. CHANGELOG
  `Changed`; no ADR. PR drafted on `ci/pin-setup-dotnet-and-quality-timeout`, awaiting merge. CI
  billing-blocked; verified by reading.

## Next

- Roadmap PR 13b: **#47** — the deferred supply-chain feature: a CodeQL (csharp) workflow, an
  `actions/dependency-review-action` job on PRs, and a CycloneDX SBOM generated and attached to the release
  (packaging.md already calls for provenance/SBOM). Its own focused PR; verify by reading (Actions
  billing-blocked). Pin any new actions to SHAs, consistent with #84.
