# 2026-07-07 — Release workflow packs & publishes the NuGet package (#22)

Fifth fix from the post-1.0 audit backlog. High-severity packaging gap: the release workflow never
produced or pushed the package, so `dotnet add package It.D4np.Util` resolved to nothing.

## Done

- **Gap:** `release.yml` fired on a `v*.*.*` tag but only ran `dotnet build` + drafted a GitHub Release —
  no `dotnet pack`, no version-match, no artifact attach, no `dotnet nuget push` — despite
  release.md/packaging.md and the README badges promising a published package (#22).
- **Fix:** reworked `release.yml` into two trigger-gated stages mirroring the documented human boundary:
  - `pack-and-draft` (on tag): packs the single `It.D4np.Util` package (Infrastructure bundles
    Domain+Application) + `.snupkg`, asserts the packed version equals the tag, drafts the release with
    both attached. Nothing published.
  - `publish-nuget` (on `release: published`): the maintainer clicking **Publish** is the gate; the job
    downloads the reviewed assets and `dotnet nuget push --skip-duplicate` to nuget.org with
    `NUGET_API_KEY`.
  ADR-0017 records the design (release:published gate over a protected environment; download-and-push
  over rebuild for provenance; no auto-publish on tag).
- **Verified locally (what can be):** `dotnet pack` on Infrastructure produces `It.D4np.Util.1.0.0.nupkg`
  + `.snupkg` (Domain/Application are `IsPackable=false`); version-match logic accepts `v1.0.0` and
  rejects `v9.9.9`/`v1.0.0-rc.1`; no tabs / valid YAML; only repo-already-pinned actions used. The full
  tag→publish→push flow can't be exercised locally (and CI is billing-blocked).
- Docs: ADR-0017, CHANGELOG (Changed), `github-setup.md` §6 (the `NUGET_API_KEY` secret).

## State

- v1.0.0 released. Repo/CI change only; no library/API/runtime change. PR drafted on
  `ci/release-pack-and-push`, awaiting maintainer merge.
- **Two user actions still required for publishing to actually work:** (1) add the `NUGET_API_KEY`
  repository secret; (2) clear the GitHub Actions billing/spending-limit block so any workflow runs.

## Next

- Last audit High: **#19** (EventBroker self-deadlock when a handler re-publishes on a full channel) —
  architectural; needs its own ADR. That completes the 6 High-severity findings.
