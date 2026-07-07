# 2026-07-07 — CI: enforce the ≥80% coverage threshold (#24)

Fourth fix from the post-1.0 audit backlog (after #21, #20, #23 + the lockfile-presence follow-up).
High-severity ci/process gap: the advertised ≥80% coverage bar was collected but never asserted.

## Done

- **Gap:** the `quality` job ran `dotnet test --collect:"XPlat Code Coverage"`, which writes a Cobertura
  file nothing reads — no threshold. The ≥80% bar promised in ADR-0010 / AGENTS.md / ROADMAP / PR
  template was unenforced; coverage could regress with CI green (#24). Measured actual coverage: ~97%
  line / ~95% branch — met, but ungated.
- **Fix:** added `coverlet.msbuild` (test-only, `PrivateAssets=all`; regenerated the test project's
  `packages.lock.json`) and replaced the unread collect with
  `dotnet test -c Release /p:CollectCoverage=true /p:CoverletOutputFormat=cobertura /p:Threshold=80
  /p:ThresholdType=line /p:ThresholdStat=total`. The run fails if total line coverage < 80%. ADR-0016
  records the decision (coverlet.msbuild over a fragile Cobertura-parse or ReportGenerator; aggregate
  `total` as the closest enforceable proxy — coverlet has no diff/new-code stat; ADR-0010 & AGENTS.md
  phrase the bar as new/changed code, spec §3/§6 as a plain ≥80%).
- **Verified locally:** gate passes at Threshold=80 (97.02% total), fails at Threshold=99 (clear coverlet
  error); solution-level CI-form command passes; locked-mode restore still green with the new lockfile;
  no tabs / valid YAML. Git-ignored coverlet's `coverage.*` output.
- Docs: ADR-0016, CHANGELOG `[Unreleased]` Changed, `docs/development/local-build.md`, `.gitignore`.

## State

- v1.0.0 released. Repo/CI-only change — no package, API, or runtime-behaviour change. PR drafted on
  `ci/enforce-coverage-threshold`, awaiting maintainer merge.
- **Pipeline still billing-blocked:** GitHub Actions won't run any job until the spending-limit block is
  cleared (Settings → Billing & plans); the gate is verified locally in the meantime.

## Next

- After merge, the last two audit Highs: **#22** (release workflow packs/pushes the NuGet package), then
  **#19** (EventBroker self-deadlock — architectural, needs its own ADR).
