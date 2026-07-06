# 2026-07-06 — CI: enforce restore locked mode (#23)

Third fix from the post-1.0 audit backlog (after #21, #20). High-severity supply-chain gap: committed
lockfiles were a no-op because CI never restored in locked mode.

## Done

- **Root cause:** `RestorePackagesWithLockFile=true` + four committed `packages.lock.json`, but no CI
  step passed `--locked-mode`/`RestoreLockedMode`. `dotnet build`/`test` restore with lockfiles present
  yet silently rewrite them on transitive drift, so a shifted/compromised transitive package would be
  pulled without failing the build. The journals' "`--locked-mode` green" notes were local hand-runs,
  not CI gates.
- **Fix:** added `<RestoreLockedMode Condition="'$(GITHUB_ACTIONS)' == 'true'">true</RestoreLockedMode>`
  to `Directory.Build.props`. In the shared props it applies to every restore in every CI job (build,
  quality, release, and any future job), including the implicit restores inside `dotnet build`/`test` —
  can't be forgotten. Local restores stay unlocked so devs add/bump packages freely
  (`dotnet restore --force-evaluate` to regenerate). ADR-0014 records the rationale (property vs
  per-job step; CI-only vs always).
- **Verified:** in-sync lockfiles → CI-simulated restore passes (exit 0); a perturbed `resolved`
  version → locked restore fails (NU1102, exit 1); lockfile reverted clean. `dotnet build -warnaserror`
  clean; `dotnet format --verify-no-changes` clean; 138 tests pass, 2 Redis integration skipped.
- Docs synced: ADR-0014, `docs/development/local-build.md` (locked-mode + regenerate guidance),
  CHANGELOG `[Unreleased]` Security.

## State

- v1.0.0 released. Repo/CI-only change — no package, API, or runtime-behaviour change. PR drafted on
  `ci/enforce-locked-mode-restore`, awaiting maintainer merge.

## Next

- After merge, continue the CI trio: **#24** (enforce the ≥80% coverage gate), then **#22** (release
  workflow packs/pushes the NuGet package). #19 (broker deadlock) last (architectural).
