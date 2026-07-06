# Architecture Decision Records

One numbered Markdown file per decision, in the lightweight
[Michael Nygard](https://cognitect.com/blog/2011/11/15/documenting-architecture-decisions)
format. Numbering is sequential and never reused or renumbered. Template:
[`template.md`](template.md).

Open an ADR when a choice affects the public surface or compatibility, when two reasonable
options exist and the rationale is non-obvious, when a **design pattern** is adopted, or
when superseding a prior decision. Do **not** open one for routine implementation details
or trivially reversible choices.

Status transitions: `Proposed` → `Accepted` → (`Superseded by ADR-XXXX` | `Deprecated`).

## Index

| ADR | Title | Status |
|-----|-------|--------|
| [0001](0001-record-architecture-decisions.md) | Record architecture decisions | Accepted |
| [0002](0002-adopt-cross-language-source-layout.md) | Adopt the cross-language source layout | Accepted |
| [0003](0003-adopt-layered-assembly-architecture.md) | Adopt a layered assembly architecture (Clean / Onion) | Accepted |
| [0004](0004-adopt-ddd-tactical-building-blocks.md) | Adopt the DDD tactical building blocks | Accepted |
| [0005](0005-place-result-error-in-domain.md) | Place the functional Result/Error model in the Domain assembly | Accepted |
| [0006](0006-introduce-infrastructure-and-persistence-patterns.md) | Introduce the Infrastructure assembly and adopt the persistence patterns | Accepted |
| [0007](0007-adopt-in-memory-event-broker-on-channels.md) | Adopt an in-memory event broker on System.Threading.Channels | Accepted |
| [0008](0008-adopt-secure-password-hashing-and-validation-behavior.md) | Adopt secure password hashing and a validation pipeline behavior | Accepted |
| [0009](0009-adopt-two-level-cache-and-distributed-serialization.md) | Adopt a two-level cache and compact distributed serialization | Accepted |
| [0010](0010-finalize-utilities-di-api-freeze-and-packaging.md) | Adopt DI auto-registration and finalize the 1.0 API, coverage, and packaging | Accepted |
| [0011](0011-adopt-documentation-i18n-zh-ja.md) | Adopt documentation i18n (Simplified Chinese and Japanese) | Accepted |
| [0012](0012-capture-domain-events-before-save.md) | Capture domain events before SaveChanges so removed aggregates are not dropped | Accepted |
| [0013](0013-cacheservice-size-limited-memorycache.md) | Make CacheService compatible with a size-limited IMemoryCache | Accepted |
| [0014](0014-enforce-restore-locked-mode-in-ci.md) | Enforce restore locked mode in CI | Accepted |
| [0015](0015-enforce-lockfile-presence-in-ci.md) | Enforce lockfile presence in CI | Accepted |
