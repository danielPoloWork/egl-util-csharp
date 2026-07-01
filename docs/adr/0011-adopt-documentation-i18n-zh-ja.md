# ADR-0011: Adopt documentation i18n (Simplified Chinese and Japanese)

- **Status:** Accepted
- **Date:** 2026-07-01
- **Deciders:** Maintainer
- **Related:** AGENTS.md §2 (language), §7 (documentation), spec §1

## Context

`AGENTS.md` §2 mandates that **every artifact in the repository is written in English**. That rule
keeps code, ADRs, commits, and specs consistent and reviewable. However, the README is the project's
front door, and non-English adopters benefit from a translated entry point. The consistency lint
shipped **disabled** i18n scaffolding (`check_i18n_freshness`, gated by `i18n_enabled`) that tracks
each translated page against its English source, so translations can be added without silently going
stale.

## Decision

- **Adopt documentation i18n**, beginning with the **README** translated into **Simplified Chinese
  (`zh-CN`)** and **Japanese (`ja`)**.
- **Place the translated READMEs under `docs/i18n/<lang>/`** — one folder per language
  (`docs/i18n/zh/README.md`, `docs/i18n/jp/README.md`) — alongside the i18n **index** and **freshness
  manifest**, keeping all translation assets in one place. Because the files sit three levels below the
  repository root, each repo-relative documentation link (`docs/specs/…`, `AGENTS.md`, `LICENSE`, …) is
  rewritten with a `../../../` prefix. Each README (English and translated) carries a language-switcher
  line linking the three variants. (`ROADMAP.md` likewise moved under `docs/`.)
- **English remains the single source of truth.** Translations are derived; when the English source
  and a translation disagree, English wins. This **qualifies AGENTS.md §2**: source code, identifiers,
  comments, ADRs, specs, commit messages, branch names, and PR titles/bodies stay English-only; the
  *only* permitted non-English artifacts are the translated user-facing docs, each tracked for freshness.
- **Freshness by content digest.** Enable `i18n_enabled` in `tools/consistency_lint.py`; each translated
  page records a **SHA-256 content digest of its English source** in `docs/i18n/translation-status.md`,
  and the `i18n-freshness` check fails if the source's current digest differs. A digest is **content-based,
  not commit-based**, so it is stable across the repository's squash-merge and lets the English source be
  edited freely: change the source, re-sync the translations, and update the recorded digest — all in one
  PR (the digest is computable at authoring time; `.gitattributes` pins `eol=lf`, so it is deterministic
  across platforms and CI).
- **Quality gate.** Translations are produced and then verified by an independent fidelity/completeness
  review (all sections present; code, commands, URLs, and API identifiers byte-identical to English)
  before landing.

## Alternatives Considered

- **Keep English-only (do nothing).** Rejected — the maintainer asked for Chinese/Japanese docs, and the
  tooling to do it safely already exists.
- **Commit-based freshness (`git log <sha>..HEAD -- source`).** Rejected — the recorded commit cannot
  reference the not-yet-created squash-merge commit, so *any* PR that edits a tracked source would leave
  the check red on `main`. A content digest sidesteps git history entirely and is squash-merge-safe.
- **Translate the whole docs tree (ADRs, specs, workflow, journals).** Rejected for now — ADRs and
  journals are internal, churn frequently, and every translated page adds freshness-maintenance cost.
  Start with the highest-value, most-visible page (README); extend deliberately later.
- **Place the translated READMEs at the repository root** (`README.zh-CN.md`, `README.ja.md`, beside
  `README.md`). Considered — it keeps relative links verbatim — but the maintainer preferred per-language
  folders under `docs/i18n/` so every translation asset lives together; the `../../../` link prefix is
  applied mechanically instead.
- **Machine-only translation with no review.** Rejected — translations are adversarially verified for
  fidelity and completeness before landing.

## Consequences

- Adopters get a translated README in Chinese and Japanese, discoverable via a language switcher on every
  README variant and the [`docs/i18n/`](../i18n/) index.
- Every future edit to `README.md` requires re-syncing the translations and updating the recorded digest
  in the same PR — a small, explicit obligation, scoped to the README and enforced by the lint.
- `AGENTS.md` §2 gains a pointer to this ADR. No package/API impact; the shipped NuGet artifact is
  unchanged (docs only), so the version stays `1.0.0`.

## References

- AGENTS.md §2, §7; `tools/consistency_lint.py` (`check_i18n_freshness`); `docs/i18n/`; `.gitattributes` (`eol=lf`).
