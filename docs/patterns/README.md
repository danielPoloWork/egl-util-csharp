# Design Patterns Catalogue

Living index of every design pattern **adopted**, **planned**, **considered and rejected**,
or **under evaluation** for `egl-util-csharp`. Mandatory reading whenever a PR introduces
or removes a pattern, and updated in the same PR.

- **Rules** — [`AGENTS.md`](../../AGENTS.md) §8.
- **Canonical taxonomy** — [`design-patterns.md`](design-patterns.md). All pattern names
  used here, in ADRs, and in commit messages must match its spelling and categorisation.

## How to use this catalogue

- **Adding a pattern** — when a PR adopts one, add a row to *Adopted* with the ADR link and
  the code location (a real path under `src/main/csharp/...`).
- **Refining** — update the row and link the new ADR.
- **Rejecting** — add it to *Rejected* with the reason; do not silently drop it.
- **Removing** — move the row to *Superseded*, link the superseding ADR, keep the history.

Status vocabulary: `Planned` (decided in an ADR, not yet landed) · `Implemented` (present
in `src/main/...`, ADR `Accepted`) · `Considered` · `Rejected` · `Superseded`.

## Adopted / Planned

| # | Pattern | Status | Problem it addresses | Code location | ADR / PR |
|---|---------|--------|----------------------|---------------|----------|
| 1 | Clean / Onion | Implemented | Keep the domain dependency-free; dependencies point inward toward it | [`Domain/`](../../src/main/csharp/it/d4np/util/Domain) | [ADR-0003](../adr/0003-adopt-layered-assembly-architecture.md) |
| 2 | Domain-Driven Design building blocks | Implemented | Strongly-typed entities, value objects, aggregates, and domain events | [`Domain/`](../../src/main/csharp/it/d4np/util/Domain) | [ADR-0004](../adr/0004-adopt-ddd-tactical-building-blocks.md) |
| 3 | Adapter | Implemented | Carry a domain event onto the MediatR notification contract, with a `Create`/`Create<TEvent>` factory that closes the notification over the event's runtime type (opt-in `It.D4np.Util.MediatR` package, #38) | [`DomainEventNotification`](../../src/main/csharp/it/d4np/util/MediatR/DomainEventNotification.cs) | [ADR-0004](../adr/0004-adopt-ddd-tactical-building-blocks.md), [ADR-0026](../adr/0026-add-domain-event-notification-factory.md), [ADR-0037](../adr/0037-split-mediatr-fluentvalidation-into-opt-in-package.md) |
| 4 | Null Object | Implemented | Represent "no error" without null checks (`Error.None`) | [`Error`](../../src/main/csharp/it/d4np/util/Domain/Error.cs) | [ADR-0005](../adr/0005-place-result-error-in-domain.md) |
| 5 | Repository | Implemented | Mediate between the domain and the data-mapping layer | [`IRepository`](../../src/main/csharp/it/d4np/util/Application/IRepository.cs) | [ADR-0006](../adr/0006-introduce-infrastructure-and-persistence-patterns.md) |
| 6 | Specification | Implemented | Declarative, composable queries isolated from the data layer, with And/Or/Not predicate combinators | [`Specification`](../../src/main/csharp/it/d4np/util/Application/Specification.cs) | [ADR-0006](../adr/0006-introduce-infrastructure-and-persistence-patterns.md), [ADR-0033](../adr/0033-specification-predicate-combinators.md) |
| 7 | Unit of Work | Implemented | Commit a set of changes as one atomic transaction | [`IUnitOfWork`](../../src/main/csharp/it/d4np/util/Application/IUnitOfWork.cs) | [ADR-0006](../adr/0006-introduce-infrastructure-and-persistence-patterns.md) |
| 8 | Producer-Consumer | Implemented | Decouple publishers from a single consumer via a bounded channel | [`EventBroker`](../../src/main/csharp/it/d4np/util/Infrastructure/EventBroker.cs) | [ADR-0007](../adr/0007-adopt-in-memory-event-broker-on-channels.md) |
| 9 | Publish-Subscribe | Implemented | Broadcast an event to all interested handlers | [`IEventBroker`](../../src/main/csharp/it/d4np/util/Application/IEventBroker.cs) | [ADR-0007](../adr/0007-adopt-in-memory-event-broker-on-channels.md) |
| 10 | Strategy | Implemented | Encapsulate an interchangeable password-hashing algorithm behind a stable interface | [`IPasswordHasher`](../../src/main/csharp/it/d4np/util/Application/IPasswordHasher.cs) | [ADR-0008](../adr/0008-adopt-secure-password-hashing-and-validation-behavior.md) |
| 11 | Chain of Responsibility | Implemented | Run cross-cutting validation as a link in the MediatR request pipeline (opt-in `It.D4np.Util.MediatR` package, #38) | [`ValidationPipelineBehavior`](../../src/main/csharp/it/d4np/util/MediatR/FluentValidation/ValidationPipelineBehavior.cs) | [ADR-0008](../adr/0008-adopt-secure-password-hashing-and-validation-behavior.md), [ADR-0037](../adr/0037-split-mediatr-fluentvalidation-into-opt-in-package.md) |
| 12 | Proxy | Implemented | Serve values from a fast cache in front of the origin (two-level cache-aside) | [`CacheService`](../../src/main/csharp/it/d4np/util/Infrastructure/CacheService.cs) | [ADR-0009](../adr/0009-adopt-two-level-cache-and-distributed-serialization.md) |
| 13 | Dependency Injection | Implemented | Register and supply the building blocks from a single composition entry point | [`ServiceCollectionExtensions`](../../src/main/csharp/it/d4np/util/Infrastructure/ServiceCollectionExtensions.cs) | [ADR-0010](../adr/0010-finalize-utilities-di-api-freeze-and-packaging.md) |

## Rejected

_No rejections recorded yet._

| # | Pattern | Considered for | Rejected because | ADR / PR |
|---|---------|----------------|------------------|----------|
| — | —       | —              | —                | —        |

## Superseded

_No superseded patterns yet._

| # | Pattern | Superseded by | When | ADR / PR |
|---|---------|---------------|------|----------|
| — | —       | —             | —    | —        |

## Candidate patterns to consider

The taxonomy in [`design-patterns.md`](design-patterns.md) lists every pattern in scope. As
the architecture takes shape, narrow that universe to the patterns plausibly applicable to
*this* artifact and list them here by category, each with a one-line "possible application".
A candidate remains a candidate until adopted (own ADR) or explicitly rejected.

## Out-of-scope categories

Record here any taxonomy category pre-classified as not applicable to this artifact (with a
one-line reason), so the policy of explicit rejection is honoured without filling the
*Rejected* table with N/A noise.
