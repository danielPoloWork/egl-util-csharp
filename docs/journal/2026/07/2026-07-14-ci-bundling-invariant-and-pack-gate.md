# 2026-07-14 — CI: enforce the bundling invariant + pack on every PR (#63, #64)

Roadmap PR 14 (packaging/CI). Two complementary packaging guards. Both approaches chosen by the maintainer
(AskUserQuestion): #63 → a `consistency_lint` check; #64 → the `dotnet pack` CI gate now, `EnablePackageValidation`
baseline deferred to the 2.0 cut. CHANGELOG `Changed` (repo/CI only); no ADR. No local Python (lint
hand-verified by tracing); the pack assertion was verified locally with `dotnet pack` + `unzip`.

## Done

- **#63 — bundling invariant unenforced.** The package bundles Domain/Application as `PrivateAssets=all`
  project references, so their `PackageReference`s never reach the nuspec and must be mirrored, at the same
  version, in the packable Infrastructure project (FluentValidation + MediatR are hand-duplicated today).
  Nothing checked this — a version bumped only in a bundled project, or a new dependency added only there,
  ships a nuspec that disagrees with the bundled binaries. **Fix:** added a `bundled-deps` check to
  `tools/consistency_lint.py` (CONFIG `packable_project` + `bundled_projects`) that parses each project's
  `PackageReference`s and fails when a bundled one is **missing from** (FileNotFoundException risk) or
  **version-mismatched against** (MissingMethodException risk) the packable project. Docstring updated;
  registered as check 8.
- **#64 — no pack gate / no API-compat baseline.** CI never ran `dotnet pack`, so the bundling target was
  exercised by no automated gate. **Fix (maintainer-chosen scope):** added a `package` CI job that runs
  `dotnet pack` on every PR and asserts the nupkg bundles all three layer DLLs (`Domain`, `Application`,
  `Infrastructure`), failing CI if the bundling target drops one. **Deferred:** `EnablePackageValidation` +
  `PackageValidationBaselineVersion` — `main` already carries the five accrued 2.0-breaking changes while
  `<Version>` is 1.0.0, so a 1.0.0 baseline would fail `dotnet pack` immediately; the baseline is scheduled
  for the 2.0 cut (enable against 2.0.0 once 2.0 ships).
- **Verification.** #63 hand-traced against the real csprojs: Application's FluentValidation 11.11.0 + MediatR
  12.5.0 both mirror Infrastructure identically; Domain has no PackageReferences — so `bundled-deps` PASSES;
  the check is non-vacuous by construction (a drift or a missing id fails). #64 pack assertion run locally:
  `dotnet pack` produced `It.D4np.Util.1.0.0.nupkg` bundling all three DLLs, and the `unzip -l | grep
  lib/net8.0/<dll>` assertion matched each. `dotnet build -warnaserror` + `dotnet format` clean; no
  C#/source changed, so the test suite is unaffected (223 pass / 3 Redis skip on this tree).

## State

- v1.0.0 released. The bundling invariant is lint-enforced and CI packs + asserts the bundled layers on every
  PR; API-compat baselining awaits 2.0. CHANGELOG `Changed`; no ADR. PR drafted on
  `ci/bundling-invariant-and-pack-gate`, awaiting merge. Actions billing-blocked (workflow read-verified; pack
  assertion verified locally). No local Python → `bundled-deps` hand-verified.
- **2.0 follow-up tracked:** enable `EnablePackageValidation` + `PackageValidationBaselineVersion=2.0.0` on
  the packable project when 2.0 is cut (the remaining half of #64).

## Next

- Roadmap PR 15: **#77** — enable the real analyzer gate (`EnforceCodeStyleInBuild`, a stricter
  `AnalysisMode`). Standalone; **may surface latent warnings that `-warnaserror` then turns into errors**, so
  assess the fallout when reading and fix or scope accordingly.
