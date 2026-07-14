# ADR-0031: Adopt CodeQL, dependency review, and a CycloneDX SBOM

- **Status:** Accepted
- **Date:** 2026-07-14
- **Deciders:** Maintainer
- **Related:** ADR-0014 (lockfile supply-chain guard), ADR-0016 (coverage gate), ADR-0017 (release pack/publish), #47

## Context

The CI/release pipeline gated build, test, format, analyzers, coverage, and the consistency lint, and pinned
lockfiles (ADR-0014) — but it had **no static application security testing (SAST), no PR-time dependency
vetting, and emitted no software bill of materials**, even though `docs/workflow/packaging.md` says to
"attach provenance/SBOM where the ecosystem supports it". So a vulnerable transitive dependency introduced
through a grouped Dependabot PR was never statically scanned, code-level defects (injection, unsafe
crypto/deserialization) had no automated detector, and downstream consumers received no dependency inventory
(#47). Grouped Dependabot keeps dependencies *current* but does not *gate* a PR on a known-vulnerable or
disallowed dependency.

## Decision

Add three supply-chain controls, each as additive CI/release configuration (no library-code change):

- **CodeQL** (`.github/workflows/codeql.yml`) — SAST over the C# source on push-to-main, on PRs, and on a
  weekly schedule, using the **buildless** analysis (`build-mode: none`) so the job needs no compile or
  restore. Findings surface under the repository's code-scanning alerts. The project has no source
  generators, so buildless extraction is complete.
- **Dependency review** (`.github/workflows/dependency-review.yml`) — `actions/dependency-review-action` on
  every pull request, failing the PR if it introduces a dependency with a known vulnerability (and, once
  configured, a disallowed license), with a summary comment on failure.
- **CycloneDX SBOM** — the release workflow generates a CycloneDX JSON SBOM of the packable project (which
  bundles Domain + Application) with the pinned CycloneDX .NET tool and attaches it to the drafted GitHub
  Release alongside the `.nupkg`/`.snupkg`.

All new GitHub Actions are pinned to full commit SHAs with a version comment, consistent with the pinning
policy reaffirmed in the same maintenance round (#84); the CycloneDX .NET tool is pinned to an exact version.

## Alternatives Considered

- **CodeQL `build-mode: manual`/`autobuild`.** Rejected as the default — buildless C# analysis is complete
  here (no source generators) and avoids running a locked-mode restore + build inside the CodeQL job. Manual
  build remains an easy switch if generated code ever needs coverage.
- **A single combined `security.yml`.** Rejected — separate `codeql.yml` and `dependency-review.yml` match
  GitHub's own templates, keep each trigger set (push/PR/schedule vs PR-only) and permission scope minimal,
  and read more clearly.
- **SPDX instead of CycloneDX; or an SBOM in the main `.nupkg`.** CycloneDX matches the ecosystem's common
  tooling and the packaging.md wording; attaching to the release (not embedding in the `.nupkg`) keeps the
  runtime package lean while still publishing the inventory.
- **Rely on Dependabot alone.** Rejected — Dependabot updates but does not block a PR, and provides neither
  SAST nor an SBOM.

## Consequences

- PRs are gated on dependency review; the default branch and PRs are scanned by CodeQL; every release ships a
  CycloneDX SBOM. Repo/CI change only — the shipped library API and package contents (beyond the added
  release-attached SBOM) are unchanged.
- These workflows require Actions billing (currently disabled) and code-scanning/Dependency-Graph to be
  enabled; until then they are configured-but-dormant. Their correctness was verified by review, and the SBOM
  command was verified locally (the CycloneDX tool produced a valid CycloneDX 1.7 JSON BOM for the package).
- New actions are Dependabot-maintained via the existing github-actions group.

## References

- Issue #47; ADR-0014/0016/0017; `.github/workflows/codeql.yml`, `.github/workflows/dependency-review.yml`,
  `.github/workflows/release.yml`; `docs/workflow/packaging.md`.
