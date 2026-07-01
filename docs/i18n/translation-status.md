# Translation Status

English is the **source of truth** for all documentation. Each translated page below records the
**SHA-256 content digest** of its English source at the point it was last synchronised.
`tools/consistency_lint.py` (the `i18n-freshness` check) recomputes the source's digest and fails when
it differs — flagging the translation as stale. To refresh a translation, update it and record the new
digest (`git show :README.md | sha256sum`) in the same PR. The digest is content-based, so it is
stable across the repository's squash-merge.

| English source | Language | Translation | Source digest | Status |
|---|---|---|---|---|
| [README.md](../../README.md) | 简体中文 (Simplified Chinese) | [zh/README.md](zh/README.md) | `2ec07cd33bc9af01` | `translated` |
| [README.md](../../README.md) | 日本語 (Japanese) | [jp/README.md](jp/README.md) | `2ec07cd33bc9af01` | `translated` |
