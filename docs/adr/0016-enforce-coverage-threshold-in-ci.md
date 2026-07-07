# ADR-0016: Enforce the ≥80% coverage threshold in CI

- **Status:** Accepted
- **Date:** 2026-07-07
- **Deciders:** Maintainer
- **Related:** ADR-0010 (makes good on its ≥80% coverage bar), spec §3, §6, issue #24, `.github/workflows/ci.yml`

## Context

ADR-0010, AGENTS.md §10, the ROADMAP, and the PR template all promise a **≥80% line-coverage** bar.
But the `quality` CI job ran `dotnet test -c Release --collect:"XPlat Code Coverage"`, which writes a
Cobertura file that **nothing reads** — no coverlet threshold, no ReportGenerator fail-under. The bar
was therefore unenforced: coverage could regress to any level and CI would stay green (issue #24).
Actual measured coverage is ~97% line / ~95% branch, so the bar is comfortably met today — it was just
never gated.

## Decision

Enforce the threshold with **coverlet.msbuild** in the `quality` job. Add `coverlet.msbuild` to the
test project (test-only, `PrivateAssets=all`; its lockfile regenerated accordingly) and replace the
unread `--collect` run with:

```
dotnet test -c Release /p:CollectCoverage=true /p:CoverletOutputFormat=cobertura \
  /p:Threshold=80 /p:ThresholdType=line /p:ThresholdStat=total
```

The test run now **fails** if total line coverage drops below 80%. Note the enforced gate is
whole-solution **aggregate** coverage: ADR-0010, AGENTS.md §10, and the PR template phrase the bar as
*new/changed code* ≥80%, while spec §3/§6 and ROADMAP 8.3 state a plain ≥80% — but coverlet.msbuild's
`ThresholdStat` has no new-/changed-code (diff) option (only `total`, `minimum`, `average`), so
aggregate `total` is adopted as the closest enforceable proxy for the ≥80% bar. Verified locally: the
gate passes at `Threshold=80` (97.02% total) and fails at `Threshold=99` with a clear coverlet error,
and the regenerated lockfile still passes locked-mode restore (ADR-0014).

## Alternatives Considered

- **Parse the Cobertura `line-rate` in a CI shell step.** Rejected — hand-rolled XML parsing is fragile;
  coverlet's built-in threshold is robust. (A Python parse would also be unverifiable locally — no local
  Python.)
- **ReportGenerator + a fail-under script.** Rejected — an extra tool and step for the same outcome;
  ReportGenerator does not fail-under natively.
- **Keep the `--collect` collector and add a threshold to it.** Rejected — the XPlat Code Coverage
  collector has no fail-under option; only coverlet.msbuild / coverlet.console do. `coverlet.collector`
  is retained for ad-hoc local `--collect` report generation.
- **`ThresholdStat=minimum` (each module ≥80%).** Considered — stricter (no weak module hides behind a
  strong one), but coverlet offers no diff/new-code stat, so the real choice is aggregate `total` vs
  per-module `minimum`. `total` is the least-surprising whole-solution proxy for the ≥80% bar;
  per-module or per-file enforcement can be tightened later without an API change.

## Consequences

- Coverage can no longer silently regress below 80%; a drop fails the `quality` job with a clear
  message. Makes good on ADR-0010's coverage promise.
- Adds one test-only dev dependency (`coverlet.msbuild`) and a lockfile entry; no package, API, or
  runtime-behaviour change. coverlet.msbuild writes `coverage.*` artifacts next to the test project —
  now git-ignored.
- The gate cannot actually run until the GitHub Actions billing/spending-limit block is cleared; its
  behaviour is verified locally in the meantime.

## References

- issue #24; ADR-0010; spec §3 (quality gates), §6 (verification & test strategy).
- coverlet.msbuild: `Threshold`, `ThresholdType`, `ThresholdStat`.
