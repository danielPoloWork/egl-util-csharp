# Documentation translations (i18n)

Translations of the user-facing documentation. **English is the source of truth**; translations are
derived and may lag behind it. Each language has its own folder here (`zh/`, `jp/`).
[`translation-status.md`](translation-status.md) records the freshness of each page — a SHA-256 content
digest of the English source, enforced by the `i18n-freshness` check in `tools/consistency_lint.py` —
and [ADR-0011](../adr/0011-adopt-documentation-i18n-zh-ja.md) records the policy.

## Available translations

| Page | 简体中文 (`zh/`) | 日本語 (`jp/`) |
|---|---|---|
| README | [zh/README.md](zh/README.md) | [jp/README.md](jp/README.md) |

## Contributing a translation

1. Translate (or refresh) the page, keeping code, commands, URLs, and API identifiers unchanged.
2. Record the English source's current **content digest** (a SHA-256 prefix, e.g.
   `git show :README.md | sha256sum`) in [`translation-status.md`](translation-status.md) so the
   `i18n-freshness` check passes. The digest is content-based, so it is stable across the squash-merge.
3. Ship both in the same PR.
