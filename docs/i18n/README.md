# Documentation translations (i18n)

Translations of the user-facing documentation. **English is the source of truth**; translations are
derived and may lag behind it. The translated READMEs live at the repository root (beside `README.md`)
so their relative links resolve identically; this directory holds the index and the freshness manifest.
[`translation-status.md`](translation-status.md) records the freshness of each page (enforced by the
`i18n-freshness` check in `tools/consistency_lint.py`), and
[ADR-0011](../adr/0011-adopt-documentation-i18n-zh-ja.md) records the policy.

## Available translations

| Page | 简体中文 | 日本語 |
|---|---|---|
| README | [README.zh-CN.md](../../README.zh-CN.md) | [README.ja.md](../../README.ja.md) |

## Contributing a translation

1. Translate (or refresh) the page, keeping code, commands, URLs, and API identifiers unchanged.
2. Record the English source's current commit in [`translation-status.md`](translation-status.md) so
   the freshness check passes.
3. Ship both in the same PR.
