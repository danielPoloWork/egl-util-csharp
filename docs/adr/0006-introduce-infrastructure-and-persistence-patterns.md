# ADR-0006: Introduce the Infrastructure assembly and adopt the persistence patterns

- **Status:** Accepted
- **Date:** 2026-06-30
- **Deciders:** Maintainer
- **Related:** ADR-0003, spec §2 (requirements 10–14), §4

## Context

Milestone 4 implements the persistence building blocks from spec §2 — `IRepository`,
`ISpecification`, `SpecificationEvaluator`, `IUnitOfWork`, and `DbContextExtensions`. ADR-0003
deferred the **Infrastructure** assembly until the first concrete technology was needed; EF Core
arrives now. The repository / specification / unit-of-work *abstractions* are dependency-free
(interfaces over `System.Linq.Expressions`) and belong in Application, while the EF Core
*implementations* belong in Infrastructure so that EF Core never leaks into the inner layers.

## Decision

- Introduce **`It.D4np.Util.Infrastructure`** (references Application + Domain +
  `Microsoft.EntityFrameworkCore` 8.0.x — the .NET 8 LTS-aligned line).
- **Application** gains the dependency-free abstractions: `IRepository<TEntity, TId>` (over
  `AggregateRoot<TId>`), `ISpecification<TEntity>` with a `Specification<TEntity>` base
  (criteria / includes / ordering / paging), and `IUnitOfWork`.
- **Infrastructure** gains `SpecificationEvaluator` (applies a specification to an `IQueryable`
  via EF Core `Where`/`Include`/`OrderBy`/`Skip`/`Take`) and `DbContextExtensions`
  (`ExtractDomainEvents` + `DispatchDomainEventsAsync`).
- Add a non-generic **`IHasDomainEvents`** marker (Domain), implemented by `AggregateRoot<TId>`,
  so infrastructure can enumerate every tracked aggregate's events regardless of its `TId`.
- Adopt the **Repository**, **Specification**, and **Unit of Work** patterns.
- **Domain-event dispatch is at-least-once:** `DispatchDomainEventsAsync` publishes each aggregate's
  events then clears them **on success**, so a publish failure leaves undelivered events buffered for
  a retry; it is called **after** a committed `SaveChangesAsync`. `PeekDomainEvents` (non-destructive)
  and `ClearDomainEvents` support outbox-style consumers.
- **Paging requires an ordering:** `SpecificationEvaluator` throws if a paged specification declares
  no `OrderBy`/`OrderByDescending`, since paging an unordered query is non-deterministic on
  relational providers. Multi-level eager loading uses `ISpecification.IncludeStrings` (dotted paths);
  the expression-based `Includes` is single-level.

## Alternatives Considered

- **Put the EF Core implementations in Application.** Rejected — it couples the application layer
  to EF Core, violating ADR-0003 (Application depends only on abstractions).
- **Reflect over `ChangeTracker` to find aggregates** instead of a marker interface. Rejected —
  fragile and slower than the explicit `IHasDomainEvents` marker.
- **Couple `DbContextExtensions` to MediatR / `IEventBroker` for publishing.** Rejected for M4 —
  the publisher is injected as a delegate, keeping Infrastructure bus-agnostic; the concrete
  MediatR / event-broker wiring arrives in Milestone 5.

## Consequences

- Infrastructure takes a dependency on EF Core 8.0.x; consumers choose a provider.
- **EF Core mapping guidance (tested):** because the library's `Id` is inherited and get-only
  (`Entity<TId>`), EF Core's key convention does not auto-detect it. Consumers must configure the
  key explicitly — `modelBuilder.Entity<T>().HasKey(e => e.Id)` — and provide a parameterless
  constructor for materialization. Verified against the EF Core InMemory and SQLite providers.
- `SpecificationEvaluator` and `DbContextExtensions` are integration-tested on both a
  non-relational (InMemory) and a relational (SQLite) provider.

## References

- spec §2 (requirements 10–14), §4; ADR-0003.
- `docs/patterns/design-patterns.md` — Data & Persistence: *Repository*, *Unit of Work*;
  Behavioral: *Specification*.
