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
| [0016](0016-enforce-coverage-threshold-in-ci.md) | Enforce the ≥80% coverage threshold in CI | Accepted |
| [0017](0017-release-pack-and-publish-nuget.md) | Pack and publish the NuGet package from the release workflow | Accepted |
| [0018](0018-non-blocking-in-handler-event-publish.md) | Make in-handler event publishing non-blocking to prevent broker self-deadlock | Accepted |
| [0019](0019-remove-specification-di-auto-registration.md) | Remove ISpecification<> from DI assembly-scan auto-registration | Accepted |
| [0020](0020-remove-implicit-error-to-result-conversion.md) | Remove the implicit Error → Result<TValue> conversion (breaking) | Accepted |
| [0021](0021-write-null-properties-in-cache-serialization.md) | Write null properties in the cache serialization format (drop WhenWritingNull) | Accepted |
| [0022](0022-drain-domain-events-to-a-fixed-point.md) | Drain domain events to a fixed point during dispatch | Accepted |
| [0023](0023-reject-non-round-trippable-cache-types.md) | Reject non-round-trippable cache types (fail fast on polymorphic serialization) | Accepted |
| [0024](0024-define-cache-l2-outage-contract.md) | Define the CacheService L2-outage contract (transparent writes; reads and removes propagate) | Accepted |
| [0025](0025-add-specification-then-by-ordering.md) | Add ThenBy/ThenByDescending secondary ordering to the Specification | Accepted |
| [0026](0026-add-domain-event-notification-factory.md) | Add a DomainEventNotification factory so the MediatR bridge works for runtime-typed events | Accepted |
| [0027](0027-variance-aware-event-dispatch.md) | Make EventBroker dispatch honor IEventHandler contravariance | Accepted |
| [0028](0028-canonical-type-entity-equality-and-transient-identity.md) | Canonical-type entity equality and transient identity (self-referencing Entity&lt;TId, TEntity&gt;) — **breaking (2.0)** | Accepted |
| [0029](0029-passwordhasher-rehash-detection-and-span-plaintext.md) | Add rehash-detection and span-based plaintext to IPasswordHasher | Accepted |
| [0030](0030-runtime-resolved-single-source-library-version.md) | Resolve the library version at runtime from a single MSBuild source — **breaking (2.0)** | Accepted |
| [0031](0031-supply-chain-sast-and-sbom.md) | Adopt CodeQL, dependency review, and a CycloneDX SBOM | Accepted |
| [0032](0032-enforce-the-analyzer-gate.md) | Make the Roslyn analyzer gate real (AnalysisMode + EnforceCodeStyleInBuild) | Accepted |
| [0033](0033-specification-predicate-combinators.md) | Add And/Or/Not predicate combinators (and IsSatisfiedBy) to ISpecification | Accepted |
| [0034](0034-result-functional-combinators.md) | Add functional combinators and non-throwing access to Result | Accepted |
| [0035](0035-reachable-pagination.md) | Make PaginatedList reachable via a criteria-only evaluation and ToPaginatedListAsync | Accepted |
| [0036](0036-datetimeprovider-delegates-to-timeprovider.md) | Bridge IDateTimeProvider to the BCL TimeProvider | Accepted |
| [0037](0037-split-mediatr-fluentvalidation-into-opt-in-package.md) | Split the MediatR/FluentValidation building blocks into the opt-in It.D4np.Util.MediatR package — **breaking (2.0)** | Accepted |
