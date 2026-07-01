# ADR-0011: Adopt documentation i18n (Simplified Chinese and Japanese)

- **Status:** Accepted
- **Date:** 2026-07-01
- **Deciders:** Maintainer
- **Related:** AGENTS.md §2 (language), §7 (documentation), spec §1

## Context

`AGENTS.md` §2 mandates that **every artifact in the repository is written in English**. That rule
keeps code, ADRs, commits, and specs consistent and reviewable. However, the README is the project's
front door, and non-English adopters benefit from a translated entry point. The consistency lint
already ships **disabled** i18n scaffolding (`check_i18n_freshness`, gated by `i18n_enabled`) that
tracks each translated page against the commit of its English source, failing when the source moves
ahead — so translations can be added without silently going stale.

## Decision

- **Adopt documentation i18n**, beginning with the **README** translated into **Simplified Chinese
  (`zh-CN`)** and **Japanese (`ja`)**.
- **Place the translated READMEs at the repository root** as `README.zh-CN.md` and `README.ja.md`,
  beside `README.md`, so their relative documentation links (`docs/specs/…`, `AGENTS.md`, `LICENSE`,
  …) resolve **identically to the English README**. The i18n **index** and **freshness manifest** live
  under `docs/i18n/` (`docs/i18n/README.md`, `docs/i18n/translation-status.md`). A language-switcher
  line at the top of each translated page links English and the sibling translation.
- **English remains the single source of truth.** Translations are derived; when the English source
  and a translation disagree, English wins. This **qualifies AGENTS.md §2**: source code, identifiers,
  comments, ADRs, specs, commit messages, branch names, and PR titles/bodies stay English-only; the
  *only* permitted non-English artifacts are the translated user-facing docs, each tracked for freshness.
- **Freshness is enforced.** Enable `i18n_enabled` in `tools/consistency_lint.py`; each translated page
  records its English source's commit in `docs/i18n/translation-status.md`, and the `i18n-freshness`
  check fails if the source has advanced. Crucially, **the i18n PR does not modify the tracked source
  (`README.md`)** — no switcher is added to it — so the recorded commit stays valid across the
  repository's squash-merge (a squash commit that does not touch `README.md` leaves its last-touched
  commit unchanged).
- **Quality gate.** Translations are produced and then verified by an independent fidelity/completeness
  review (all sections present; code, commands, URLs, and API identifiers byte-identical to English)
  before landing.

## Alternatives Considered

- **Keep English-only (do nothing).** Rejected — the maintainer asked for Chinese/Japanese docs, and the
  tooling to do it safely already exists.
- **Translate the whole docs tree (ADRs, specs, workflow, journals).** Rejected for now — ADRs and
  journals are internal, churn frequently, and every translated page adds freshness-maintenance cost.
  Start with the highest-value, most-visible page (README); extend deliberately later.
- **House the translated READMEs under `docs/i18n/`.** Rejected — it would force rewriting every relative
  doc link with a `../../` prefix (error-prone). Placing them at the root next to `README.md` keeps all
  links working verbatim; only the index and manifest live under `docs/i18n/`.
- **Add a language switcher to `README.md` itself.** Rejected — editing the tracked source would, through
  the squash-merge, make the recorded source commit trail `HEAD` and trip the freshness check. The
  switcher lives on the translated pages and in the `docs/i18n/` index instead.
- **Machine-only translation with no review.** Rejected — translations are adversarially verified for
  fidelity and completeness before landing.

## Consequences

- Adopters get a translated README in Chinese and Japanese with a language switcher; the English README
  is intentionally left untouched, so translations are discovered via the switcher on translated pages
  and the [`docs/i18n/`](../i18n/) index.
- Every future edit to `README.md` will flag its translations stale until refreshed (the intended
  behavior) — a small, explicit maintenance obligation, scoped to the README.
- `AGENTS.md` §2 gains a pointer to this ADR. No package/API impact; the shipped NuGet artifact is
  unchanged (docs only), so the version stays `1.0.0`.

## References

- AGENTS.md §2, §7; `tools/consistency_lint.py` (`check_i18n_freshness`); `docs/i18n/`.
