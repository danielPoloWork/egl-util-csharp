# 2026-07-14 — CI: CodeQL + dependency review + CycloneDX SBOM (#47)

Roadmap PR 13b (supply-chain) — the feature split out of the #47+#84+#75 batch. #84+#75 shipped as PR #163;
this is the substantive supply-chain half. CHANGELOG `Added`; ADR-0031. Actions is billing-blocked, so the
workflows are verified by reading; the SBOM command was verified by running the CycloneDX tool locally.

## Done

- **CodeQL** — new `.github/workflows/codeql.yml`: SAST over C# on push-to-main, PRs, and a weekly schedule,
  using **buildless** analysis (`build-mode: none`) so the job needs no restore/compile (the project has no
  source generators, so buildless extraction is complete). `security-events: write` to upload results.
- **Dependency review** — new `.github/workflows/dependency-review.yml`: `actions/dependency-review-action`
  on `pull_request`, failing a PR that introduces a known-vulnerable (or disallowed-license) dependency, with
  a summary comment on failure (`pull-requests: write`). Complements Dependabot (which updates but does not
  gate).
- **CycloneDX SBOM** — a step in `release.yml`'s `pack-and-draft` generates a CycloneDX JSON SBOM of the
  packable project (which bundles Domain + Application) with the pinned CycloneDX .NET tool, and the draft
  step attaches `./artifacts/*.sbom.json` alongside the `.nupkg`/`.snupkg`.
- **Pinning** — CodeQL `github/codeql-action@e5d2f324… # v3.37.0`, dependency-review
  `actions/dependency-review-action@2031cfc0… # v4.9.0`, checkout/setup-dotnet reuse the existing pins; the
  CycloneDX tool is `--version 6.2.0`. All SHAs resolved from the GitHub API (not guessed).
- **Verification.** The SBOM command was run **locally** end-to-end: `dotnet CycloneDX <csproj> --output …
  --output-format Json --filename … --set-name It.D4np.Util --set-version 1.0.0` found 23 packages and wrote
  a valid CycloneDX **1.7** JSON BOM (bomFormat `CycloneDX`); the tool was then uninstalled. The two new
  workflows are read-verified (Actions billing-blocked): standard CodeQL/dependency-review shapes, minimal
  scoped permissions, SHA-pinned actions. No .NET/source changes — build/suite untouched.

## State

- v1.0.0 released. CI now runs CodeQL + dependency review, and every release ships a CycloneDX SBOM.
  CHANGELOG `Added`; ADR-0031 (+ index). PR drafted on `ci/codeql-dependency-review-sbom`, awaiting merge.
  Actions billing-blocked (so the new workflows are dormant until enabled); SBOM verified locally. README
  untouched (no i18n churn).

## Next

- Roadmap PR 14: **#63 + #64** — CI: enforce the single-package bundling-deps invariant (#63) and add
  API-compat / package validation (#64). **#64 interacts with the pending 2.0 break — sequence it after the
  2.0 baseline or configure the baseline**; assess when reading, and AskUserQuestion if the API-baseline
  handling is ambiguous.
