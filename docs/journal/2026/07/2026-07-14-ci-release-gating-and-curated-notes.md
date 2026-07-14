# 2026-07-14 — CI: release gating (full CI) + curated release notes (#37, #73)

Roadmap PR 12 (build/CI + release-docs). Two coupled `release.yml` fixes, same file, tight batch. CHANGELOG
`Changed` (repo/CI only; shipped package unchanged); no ADR. GitHub Actions is billing-blocked, so verified
by careful reading of the workflow (no live run).

## Done

- **#37 — release drafted after build-only.** A tag push does not trigger `ci.yml`, and `release.yml` packed
  + version-matched but ran no tests/coverage/format/analyzers/lint on the released commit, so a tag on a
  broken commit still drafted a green release. Added a **`verify` job that reuses `ci.yml` via
  `workflow_call`** (`uses: ./.github/workflows/ci.yml`) — the full build + test OS matrix,
  format/analyzers/coverage, and the consistency lint, run on the tagged commit — and made `pack-and-draft`
  `needs: verify`, so the release is drafted only after every gate passes on exactly the released commit.
  (`ci.yml` already declared `workflow_call`, so it is designed for this reuse.)
- **#73 — draft ignored the curated notes.** `docs/workflow/release.md` step 4 mandates hand-written notes
  under `docs/releases/<tag>.md`, but the workflow passed `generate_release_notes: true` with no body, so the
  reviewed/published release carried only GitHub's auto-generated commit list. Added a step that **fails if
  `docs/releases/<tag>.md` is missing**, and set **`body_path: docs/releases/${{ github.ref_name }}.md`** on
  the draft step (keeping `generate_release_notes: true`, which appends the auto commit list after the
  curated body). The `uses: softprops/action-gh-release@<sha>` line is left **byte-identical** so the open
  Dependabot bump (#161, 3.0.1→3.0.2) merges without conflict regardless of order.
- **Drive-by consistency:** the version-mismatch error message still told maintainers to "bump Version.cs and
  the csproj `<Version>`" — stale after #159 moved the version to `Directory.Build.props`. Corrected it to
  point at `Directory.Build.props` (same version-gating step, so in scope).
- **Verification (no live Actions).** Re-read the whole workflow: the reusable-workflow call and `needs:`
  wiring are valid; both `verify` and `pack-and-draft` stay `if: github.event_name == 'push'` (neither runs
  on `release: published`, where only `publish-nuget` runs, unaffected). `docs/releases/v1.0.0.md` exists, so
  the curated-notes gate passes for the current release. No .NET/source changes — build/suite untouched.

## State

- v1.0.0 released. A `v*.*.*` tag now runs the full CI suite before drafting, and the drafted release body is
  the curated `docs/releases/<tag>.md` (auto commit list appended). CHANGELOG `Changed`; no ADR. PR drafted on
  `ci/release-gating-and-curated-notes`, awaiting merge. Dependabot #161 (action-gh-release bump) is open and
  independent — my edits avoid its line. CI billing-blocked; workflow verified by reading.

## Next

- Roadmap PR 13: **#47 + #84 + #75** — CI hygiene batch: add CodeQL / dependency-review / SBOM, pin
  `setup-dotnet` to a commit SHA, and add a `quality`-job timeout. Same `ci.yml`/security-workflow area,
  small items. Verify by reading (Actions billing-blocked).
