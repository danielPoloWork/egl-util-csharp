# Release Process

The mechanical step-by-step for cutting a release of `egl-util-csharp`. The governance
(which SemVer level, how a fix flows, deprecation/security) is in
[`maintenance.md`](maintenance.md); the agent-vs-human boundary is
[`AGENTS.md`](../../AGENTS.md) §11.

## Versioning

**Semantic Versioning 2.0.0**, annotated tags `vMAJOR.MINOR.PATCH`. Start point:
pre-1.0 milestone-driven.

- Pre-1.0: `MINOR` bumps on each completed roadmap milestone; `PATCH` for hotfixes.
- Post-1.0: `MAJOR` for incompatible changes, `MINOR` for additions, `PATCH` for fixes.

## Cutting a release (the steps)

1. **Bump the version constant** (public const string Version = "X.Y.Z") in `Version.cs`; update any
   version-check test.
2. **Roll the changelog** — move the `[Unreleased]` entries into a new per-version file
   `docs/changelog/v<MAJOR>/v<X.Y.Z>.md` and add an index row to `CHANGELOG.md`.
3. **Refresh the README** status badge (and milestone table on a MINOR that closes a
   milestone).
4. **Draft release notes** under `docs/releases/v<X.Y.Z>.md`.
5. **Run the consistency lint** (`python tools/consistency_lint.py`) — version lockstep must
   pass.
6. **Open the release PR** — *the maintainer does this*. The agent prepares it.
7. **Merge** — *the maintainer*.
8. **Tag + draft (carry-through)** — the agent runs `git tag -a v<X.Y.Z> -m "<headline>"` and
   `git push origin v<X.Y.Z>` immediately after merge; the tag push lets CI **pack** the
   `It.D4np.Util` package (+ symbols), assert the packed version equals the tag, and open the GitHub
   Release as a **draft** with the `.nupkg`/`.snupkg` attached. The agent always carries the release
   this far — only **Publish** is the human's.
9. **Publish** the GitHub Release — *the maintainer* (the deliberate human checkpoint). Publishing
   triggers CI to **push the reviewed package to nuget.org** (ADR-0017; requires the `NUGET_API_KEY`
   secret — see [`github-setup.md`](github-setup.md) §6).
10. The published version is **immutable** — fix a mistake forward with a new version, never by
    re-publishing (`--skip-duplicate` makes a re-run idempotent).

## Boundary

| Action | Who |
|---|---|
| Bump version, roll changelog, draft notes | Agent |
| Open / merge the release PR | **Human** |
| Create & push the annotated tag, then the **draft** release (CI drafts it on tag-push) | Agent |
| Publish the GitHub Release (click **Publish**) | **Human** |
| Pack, verify the version, & attach artifacts to the draft (on tag push) | CI |
| Push the package to nuget.org (on **Publish**) | CI |

Agents never publish releases, never amend or delete published tags, and only delete-and-
repush an *unpublished* tag whose release run visibly failed.
