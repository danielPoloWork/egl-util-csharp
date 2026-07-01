# Contributing to egl-util-csharp

Thanks for your interest in contributing. This project is held to an enterprise quality bar, and the
full contract for how work is done — persona, language, git workflow, quality gates, and
documentation rules — lives in **[`AGENTS.md`](AGENTS.md)**. Please read it first; the notes below are
a quick orientation.

## Ground rules

- **Language.** Every artifact on disk is written in **English**, except translated user-facing docs
  under [`docs/i18n/`](docs/i18n/) (English stays the source of truth). See `AGENTS.md` §2 and
  [ADR-0011](docs/adr/0011-adopt-documentation-i18n-zh-ja.md).
- **One change per PR, one PR at a time.** Each [`docs/ROADMAP.md`](docs/ROADMAP.md) item ships in its
  own PR, branched from `main`; wait for it to merge before the next. No stacked PRs.
- **Conventional Commits.** `type(scope): subject`, imperative, ≤ 72 chars. Branch names
  `<type>/<short-kebab>`.
- **Design decisions are documented.** Non-trivial choices and every adopted design pattern get an ADR
  in [`docs/adr/`](docs/adr/) and, for patterns, a row in [`docs/patterns/`](docs/patterns/).

## Before you open a PR

1. `dotnet build -c Release` and `dotnet test -c Release` are green.
2. `dotnet format --verify-no-changes` is clean; the build is warning-free (`-warnaserror`).
3. `python tools/consistency_lint.py` passes (version lockstep, ADR index, patterns, spec map,
   milestones, bugs, i18n freshness).
4. Docs are in lockstep: `README.md`, `docs/ROADMAP.md`, `CHANGELOG.md`, and any ADR/pattern/spec/journal
   updates ship **in the same PR**. See [`docs/workflow/`](docs/workflow/).

## Boundaries

Agents prepare branches, commits, and draft PRs; **the maintainer opens, reviews, and merges PRs** and
publishes releases. See `AGENTS.md` §6 and [`docs/workflow/release.md`](docs/workflow/release.md).

By participating, you agree to abide by the [Code of Conduct](CODE_OF_CONDUCT.md).
