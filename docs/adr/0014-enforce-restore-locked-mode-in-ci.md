# ADR-0014: Enforce restore locked mode in CI

- **Status:** Accepted
- **Date:** 2026-07-06
- **Deciders:** Maintainer
- **Related:** ADR-0010 (1.0 API freeze, coverage & packaging), spec §3, issue #23, `Directory.Build.props`

## Context

`Directory.Build.props` sets `RestorePackagesWithLockFile=true` and four `packages.lock.json` are
committed, so the intent is a reproducible, tamper-evident restore. But **no CI step enforced locked
mode.** `dotnet build` / `dotnet test` restore with the lockfiles present yet silently **update**
them when a transitive dependency drifts, so a shifted or compromised transitive package is pulled
and the build stays green — defeating the whole purpose of committing lockfiles (issue #23, High
severity, supply-chain). The session journals recorded `dotnet restore --locked-mode` as "green",
but that was run by hand locally; CI never gated on it.

## Decision

Set `RestoreLockedMode` to `true` in `Directory.Build.props`, conditioned on the CI environment:

```xml
<RestoreLockedMode Condition="'$(GITHUB_ACTIONS)' == 'true'">true</RestoreLockedMode>
```

Because it lives in the shared props file imported by every project, it applies to **every restore
in every CI job** — the `build`, `quality`, and `release` jobs, the implicit restores inside
`dotnet build` / `dotnet test` / `dotnet format`, and any job added later — with no per-job wiring to
forget. A drifted or tampered `packages.lock.json` then fails the restore instead of being rewritten.
Local restores stay **unlocked** (`GITHUB_ACTIONS` unset), so a developer can add or update a package
and regenerate the lockfile with `dotnet restore --force-evaluate`, committing it in the same PR.

Verified: with the current in-sync lockfiles a CI-simulated restore (`GITHUB_ACTIONS=true`) passes;
perturbing a committed `resolved` version makes the locked restore fail (NU1102) rather than silently
re-resolve — confirming the gate bites.

## Alternatives Considered

- **An explicit `dotnet restore --locked-mode` step in each job (plus `--no-restore` on the build /
  test / format steps).** Rejected — it must be repeated across `build`, `quality`, and `release` and
  re-added to every future job (easy to forget, which is how the gap arose), and threading
  `--no-restore` through every dotnet invocation is fragile. Enforcing it centrally in the shared
  props cannot be bypassed or forgotten.
- **`RestoreLockedMode=true` unconditionally (local builds too).** Rejected — it would block a
  developer legitimately adding or bumping a package until they regenerate the lockfile, adding
  friction to ordinary local work. The gate belongs at the CI boundary, not on every `dotnet build`.
- **Condition on `ContinuousIntegrationBuild` instead of `GITHUB_ACTIONS`.** Equivalent in effect, but
  conditioning directly on `GITHUB_ACTIONS` avoids any dependence on MSBuild property-evaluation order
  and matches how this file already detects CI.

## Consequences

- CI now fails fast on lockfile drift across the `build`, `quality`, and `release` jobs, so the
  committed lockfiles finally provide the reproducible / tamper-evident restore they were meant to.
  This complements the coverage and packaging gates finalized in ADR-0010 with a supply-chain lock.
- No package, API, or runtime-behaviour change; local development is unaffected.
- Contributors who change dependencies must commit the regenerated `packages.lock.json` in the same
  PR, or CI fails — documented in `docs/development/local-build.md`.
- **Known limitation — this enforces lockfile *consistency*, not *presence*.** `RestoreLockedMode`
  only rejects a lockfile that has drifted from the project's dependency graph; it does **not** require
  a lockfile to exist. A project with no committed `packages.lock.json` restores green under locked
  mode (NuGet silently generates one in `obj/`), so its dependencies would be ungated. Every current
  project commits a lockfile, but a future one (e.g. the planned `src/bench/…` tree) that forgets to
  is not caught here. Enforcing presence — a CI/lint check that every project directory ships a tracked
  `packages.lock.json` — is a tracked follow-up, kept separate so it can carry its own test.

## References

- issue #23; ADR-0010; spec §3 (deterministic build / dependency policy).
- NuGet lock files: `RestoreLockedMode` / `dotnet restore --locked-mode`.
