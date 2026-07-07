# ADR-0017: Pack and publish the NuGet package from the release workflow

- **Status:** Accepted
- **Date:** 2026-07-07
- **Deciders:** Maintainer
- **Related:** ADR-0010 (packaging), issue #22, `.github/workflows/release.yml`, docs/workflow/{release,packaging}.md

## Context

`release.yml` fired on a `v*.*.*` tag but only ran `dotnet build -c Release` and drafted a GitHub
Release. There was **no `dotnet pack`, no version-match assertion, no artifact attachment, and no
`dotnet nuget push`** — despite `docs/workflow/release.md` and `packaging.md` promising CI builds and
attaches artifacts and pushes to the registry, and despite the README's NuGet badges. As a result
`dotnet add package It.D4np.Util` resolved to nothing on any feed (issue #22, High). The documented
release process (release.md) already prescribes the human boundary: the agent tags and CI drafts the
release; the **maintainer clicks Publish** (the deliberate checkpoint); CI attaches artifacts and
publishes.

## Decision

Rework `release.yml` into two trigger-gated stages that mirror that documented boundary:

- **`pack-and-draft` (on the `v*.*.*` tag push).** Packs the single packable project
  (`It.D4np.Util.Infrastructure`, which bundles Domain + Application) into the `It.D4np.Util` `.nupkg`
  plus its `.snupkg` symbols, **asserts the packed version equals the tag** (fails the release
  otherwise), and drafts the GitHub Release with both files attached. Nothing is published.
- **`publish-nuget` (on `release: published`).** When the maintainer clicks **Publish** on that draft —
  the human gate — this job downloads the exact reviewed `.nupkg`/`.snupkg` from the release and runs
  `dotnet nuget push … --skip-duplicate` against nuget.org using the `NUGET_API_KEY` secret (the
  adjacent `.snupkg` is pushed to the symbol server alongside the `.nupkg`).

Publishing is therefore gated by the human `Publish` action, never by the tag push, so a tag can never
auto-publish. The push downloads the reviewed release assets rather than rebuilding, so exactly what a
human saw is what ships. `NUGET_API_KEY` must be configured (documented in `github-setup.md`); the job
fails loudly if it is absent. Verified locally: `dotnet pack` produces `It.D4np.Util.<ver>.nupkg` +
`.snupkg`, and the version-match logic accepts a matching tag and rejects a mismatched one.

## Alternatives Considered

- **A protected `environment:` with required reviewers on a tag-triggered push job.** Also a valid
  human gate, but it duplicates the approval the maintainer already gives by clicking Publish and
  requires extra repo configuration. Keyed the push to `release: published` instead; a protected
  environment is documented as optional additional hardening.
- **Re-pack from the tag in the publish job.** Rejected in favour of downloading the release assets, so
  the published bytes are the exact ones attached to (and reviewed on) the release — better provenance
  than a second build, even with a deterministic build.
- **Auto-publish on the tag push.** Rejected — publishing to a public, immutable registry is
  outward-facing and must remain the deliberate human checkpoint (AGENTS.md §11).
- **A separate `actions/upload-artifact` step.** Omitted — the release-asset attachment is the durable,
  human-reviewable, and publish-consumed artifact; a second ephemeral workflow artifact would be
  redundant for this flow.

## Consequences

- A tagged, published release now yields the `It.D4np.Util` package on nuget.org, so the README badges
  and `dotnet add package It.D4np.Util` resolve. Repo/CI change only; no library code, API, or runtime
  change.
- The maintainer must add the `NUGET_API_KEY` repository secret once (`github-setup.md` §6). Until then
  the publish job fails with a clear message; drafting/attaching still works.
- The version-match assertion turns a mistagged release into a fast, clear failure instead of a wrong
  published version. `--skip-duplicate` makes a re-published tag idempotent.
- Out of scope (separate issues): running tests/coverage in the release job (#37), SourceLink/PDBs in
  the symbol package (#36/#66), pinning `actions/setup-dotnet` to a SHA (#84).

## References

- issue #22; ADR-0010; docs/workflow/release.md, packaging.md, github-setup.md.
- `dotnet nuget push` (`--skip-duplicate`), `softprops/action-gh-release` (`files:`).
