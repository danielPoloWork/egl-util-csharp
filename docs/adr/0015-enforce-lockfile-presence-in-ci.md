# ADR-0015: Enforce lockfile presence in CI

- **Status:** Accepted
- **Date:** 2026-07-06
- **Deciders:** Maintainer
- **Related:** ADR-0014 (closes its "consistency, not presence" limitation), issue #23, `.github/workflows/ci.yml`

## Context

ADR-0014 enabled `RestoreLockedMode` in CI so a drifted or tampered `packages.lock.json` fails the
build. Its adversarial review surfaced a residual gap, recorded as ADR-0014's known limitation:
locked mode enforces lockfile **consistency**, not **presence**. `RestorePackagesWithLockFile=true` is
set repo-wide, but a project with **no** committed `packages.lock.json` restores green under locked
mode — NuGet silently generates one in `obj/` — leaving that project's dependencies ungated. All four
current projects commit a lockfile, but a future project (e.g. the planned `src/bench/…` tree) whose
lockfile isn't committed would silently escape the supply-chain gate.

## Decision

Add a step to the `consistency` CI job that fails the build if any tracked `.csproj` lives in a
directory without a **committed** `packages.lock.json`. It uses only `git` (already available after
checkout): it iterates `git ls-files -z '*.csproj'` and, for each, asserts a tracked sibling
`packages.lock.json` via `git ls-files --error-unmatch`, emitting a `::error file=…::` annotation
naming any offender. Paths are read NUL-delimited so directories with non-ASCII or special characters
are not C-quoted by git (which would otherwise cause a spurious failure).

Implement it as an **inline bash step**, not a check in `tools/consistency_lint.py`. The lint would be
the more congruent home, but the maintainer has no local Python and CI is currently blocked by a
GitHub Actions billing limit, so a Python check would ship unverified; the bash logic is verified
locally instead (passes on the repo; fails and names the offender in a synthetic repo missing a
lockfile, including the non-ASCII path case).

## Alternatives Considered

- **A check in `tools/consistency_lint.py`.** Deferred — architecturally the better home (it is the
  repo-congruence tool), but unverifiable right now: no local Python and no CI runners. It can be
  migrated there later once the toolchain allows it to be tested.
- **`git -c core.quotePath=false ls-files` (without `-z`).** Rejected — it fixes non-ASCII quoting but
  still mishandles paths containing newlines/tabs. NUL-delimited output is the robust choice.
- **Do nothing / document only.** Rejected — a supply-chain gate that silently ignores an entire
  ungated project defeats its purpose; the check is cheap and closes the gap.

## Consequences

- ADR-0014's known limitation is closed: locked mode (consistency) plus this check (presence) together
  ensure every project's dependencies are gated by a committed lockfile.
- Adding a new project now requires committing its `packages.lock.json`, or CI fails with a clear
  message — documented in `docs/development/local-build.md`.
- Repo/CI-only change; no package, API, or runtime-behaviour change.
- The check currently cannot run until the GitHub Actions billing/spending-limit block is cleared; its
  logic is verified locally in the meantime.

## References

- issue #23; ADR-0014.
- `git ls-files` (`-z`, `--error-unmatch`), `core.quotePath`.
