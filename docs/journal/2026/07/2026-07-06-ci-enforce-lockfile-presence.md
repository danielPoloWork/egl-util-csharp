# 2026-07-06 — CI: enforce lockfile presence (follow-up to #23)

Follow-up to #23 / ADR-0014, closing the "consistency vs. presence" gap the adversarial review of #23
surfaced.

## Done

- **Gap:** `RestoreLockedMode` (from #23) enforces lockfile *drift*, not *absence*. Because
  `RestorePackagesWithLockFile=true` is repo-wide, a new project with no committed `packages.lock.json`
  restores green under locked mode (NuGet silently generates one in `obj/`), leaving its dependencies
  ungated — e.g. a future `src/bench/…` project whose lockfile isn't committed.
- **Fix:** added a bash step to the `consistency` CI job that fails if any tracked `.csproj` lives in a
  directory without a committed `packages.lock.json` (`git ls-files '*.csproj'` → assert a tracked
  sibling lockfile), with a clear `::error file=…::` annotation naming the offender. Chose a bash step
  over a `tools/consistency_lint.py` check because the maintainer has no local Python and CI is
  currently billing-blocked, so a Python check would ship unverified; the bash logic is verified
  locally instead.
- **Verified locally:** passes on the current repo (all 4 projects commit a lockfile); fails with exit
  1 and names the offender in a synthetic repo where a project lacks one. (CI cannot run until the
  GitHub Actions billing/spending-limit block is cleared.)
- Docs: new **ADR-0015** records the presence-enforcement decision (and the bash-vs-Python rationale)
  and closes ADR-0014's known limitation via a cross-reference (ADR-0014's historical text is left
  intact); `local-build.md` (commit a new project's lockfile); CHANGELOG `[Unreleased]` Security.

## State

- Depends on #23 (merged, PR #107). Repo/CI-only change — no package, API, or runtime-behaviour change.
  PR drafted on `ci/enforce-lockfile-presence`, awaiting maintainer merge.
- **Blocker for the whole pipeline:** GitHub Actions is billing/spending-limit blocked, so no CI runs
  (all jobs fail to start) until resolved in Settings → Billing & plans.

## Next

- After merge, resume the audit backlog: **#24** (enforce the ≥80% coverage gate), then #22 (release
  pack/push). #19 (broker deadlock) last (architectural).
