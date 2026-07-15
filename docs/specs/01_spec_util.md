# Software Specification: EGL Util — Enterprise .NET Architecture Library (C# 12 / .NET 8)

> Rendered from the intake interview (Phase 5). Frozen contract: diverging implementation
> updates this spec in the same PR or adds an ADR superseding the relevant section.

## 1. Objective & Business Context

egl-util-csharp is a NuGet utility library of strongly-typed, immutable, extensible architectural building blocks for .NET applications that adopt Domain-Driven Design, Clean Architecture, and CQRS. It removes the boilerplate of re-implementing entities, value objects, aggregates, the Result error-handling pattern, repositories / specifications / unit-of-work, an in-memory event broker, password hashing, validation, and two-level caching, so teams focus on domain logic with compile-time safety instead of re-deriving the same scaffolding per project.

## 2. Functional Requirements

- `Entity<TId, TEntity>` — abstract base with identity-based equality on the canonical type `TEntity` (equal iff same canonical type and non-default `Id`; transient default-`Id` entities compare by reference, and EF proxies compare correctly — self-referencing generic, ADR-0028) and a strongly-typed `Id`.
- `ValueObject` — abstract base with structural value equality and matching `GetHashCode`, derived from an explicit `GetEqualityComponents()` sequence.
- `AggregateRoot<TId, TAggregate>` — extends `Entity<TId, TAggregate>`; accumulates domain events in a read-only `DomainEvents` collection via `RaiseDomainEvent` / `ClearDomainEvents`.
- `IDomainEvent` — marker interface for domain events carrying an occurred-on timestamp sourced from `IDateTimeProvider`.
- `DomainEventNotification<TEvent>` — adapts an `IDomainEvent` into a MediatR `INotification` so domain events dispatch as application notifications; ships in the opt-in `It.D4np.Util.MediatR` package under `It.D4np.Util.MediatR` (ADR-0037).
- `Result` — success/failure of an operation without throwing; exposes `IsSuccess`/`IsFailure` and an ordered `Errors` collection (with an `Error` convenience for the first), via `Success()`/`Failure(params Error[])` factories, and folds to a value with `Match` (added additively in 1.x, ADR-0034).
- `Result<TValue>` — generic result carrying a guarded success `Value` and the failure `Errors`, with implicit conversion from `TValue`; failures are created with the explicit `Result.Failure` factory (there is no implicit `Error` conversion — it collided with the value conversion; ADR-0020). Composes functionally via `Match`/`Map`/`Bind` and offers non-throwing access via `GetValueOrDefault`/`TryGetValue` (added additively in 1.x, ADR-0034).
- `Error` — immutable `record` of `Code` + `Message`, value-equal, defining a canonical `Error.None`.
- `Result.Combine` — aggregates many results into one that succeeds iff all succeed, else returns the collected errors.
- `IRepository<TEntity, TId>` — generic CRUD abstraction (`GetByIdAsync`, `ListAsync`, `Add`, `Update`, `Remove`) over an aggregate root.
- `ISpecification<TEntity>` — Specification pattern: criteria expression, includes, ordering (a primary key plus secondary `ThenBy`/`ThenByDescending` keys; added additively in 1.x, ADR-0025), and paging, isolating LINQ/EF Core query shape; predicates compose via `And`/`Or`/`Not` combinators (rebound onto a single parameter so they stay EF-translatable) plus an in-memory `IsSatisfiedBy` (default interface members; added additively in 1.x, ADR-0033).
- `SpecificationEvaluator` — applies an `ISpecification<TEntity>` to an `IQueryable<TEntity>`, composing criteria, includes, ordering (primary then secondary keys), and paging deterministically; a criteria-only overload (count the full matching set) and `ToPaginatedListAsync` make `PaginatedList<T>` reachable — a page plus its total match count (added additively in 1.x, ADR-0035).
- `IUnitOfWork` — atomic transaction boundary; `SaveChangesAsync` commits all tracked changes as one unit.
- `DbContextExtensions` — extracts pending domain events from tracked aggregates and publishes them around `SaveChanges`, clearing them on success; `CaptureDomainEvents` snapshots the events before `SaveChanges` (into a `DomainEventSnapshot`) so events on aggregates the save removes are still delivered.
- `IEventBroker` — asynchronous in-memory publish abstraction (`PublishAsync<TEvent>`) that does not block the caller.
- `EventBroker` — `System.Threading.Channels`-backed implementation with a background consumer: thread-safe, bounded back-pressure, ordered per-channel delivery.
- `IEventHandler<TEvent>` — consumer contract (`HandleAsync(TEvent, CancellationToken)`) resolved per event type.
- `IPasswordHasher` — algorithm-agnostic abstraction for secure password hashing and verification (`Hash`, `Verify`; plus additive `NeedsRehash` and `ReadOnlySpan<char>` overloads as default interface members, ADR-0029).
- `PasswordHasher` — PBKDF2 implementation with per-password random salt, configurable high iteration count, and constant-time verification.
- `ValidationPipelineBehavior<TRequest, TResponse>` — MediatR pipeline behavior running all FluentValidation validators and short-circuiting on failure; ships in the opt-in `It.D4np.Util.MediatR` package under `It.D4np.Util.MediatR.FluentValidation` (ADR-0037).
- `ICacheService` — two-level (in-memory + distributed/Redis) cache abstraction with `GetOrCreateAsync`, TTL, and key-based invalidation.
- `DistributedCacheExtensions` — typed get/set over `IDistributedCache` using compact System.Text.Json serialization.
- `IDateTimeProvider` / `DateTimeProvider` — injectable abstraction over the system clock (UtcNow) to make time deterministic in tests; on .NET 8+ the default delegates to `System.TimeProvider` (`TimeProvider.System` is registered alongside), a thin facade over the BCL clock (ADR-0036).
- `ServiceCollectionExtensions.AddD4npUtil(...)` — assembly-scanning auto-registration of event handlers into `IServiceCollection` (specifications are query objects constructed with their runtime arguments, not auto-registered — ADR-0019). FluentValidation validator scanning and the validation pipeline behavior moved to the opt-in `It.D4np.Util.MediatR` package's `AddD4npUtilMediatRValidation(...)` / `AddD4npUtilMediatR(...)` (ADR-0037).
- `PaginatedList<T>` — encapsulates a page of results with `Items`, `PageIndex`, `TotalCount`, `TotalPages`, `HasNext`/`HasPrevious`.

## 3. Non-Functional Requirements

- Compile-time type safety: generic, strongly-typed surface; nullable reference types enabled and treated as errors (`dotnet build -warnaserror`).
- Immutability: `Error`, value objects, and domain events are immutable (records / init-only); no post-construction mutation of value types.
- No exceptions for expected control flow: failures flow through `Result`; exceptions are reserved for programmer errors.
- Thread safety: `EventBroker` is safe for concurrent publishers with documented delivery and ordering guarantees.
- Security: `PasswordHasher` uses PBKDF2 with >= 600,000 iterations (OWASP 2024), >= 128-bit random salt, and constant-time verification.
- Test determinism: time is injected via `IDateTimeProvider`; no `DateTime.UtcNow` in shipped logic.
- Dependency policy: the domain layer has zero third-party dependencies; the core `It.D4np.Util` package depends only on EF Core and the Microsoft.Extensions abstractions, and the MediatR/FluentValidation coupling is isolated to the opt-in `It.D4np.Util.MediatR` package (ADR-0037).
- Portability: targets `net8.0`; builds and tests green on Linux, Windows, and macOS under .NET 8.
- Quality gates: >= 80% line coverage (coverlet), `dotnet format` clean, Roslyn analyzers as errors, deterministic build.
- API stability: the public surface is protected by SemVer. 1.0 froze the initial surface; 2.0 rolls up the breaking changes accrued after it (see the changelog and ADR-0020/0027/0028/0030/0037).

## 4. Logical Architecture & Core Algorithm

Clean / Onion architecture: dependencies point inward toward the domain. The core is organised into three layers, separated by namespace under `It.D4np.Util` and shipped as the single `It.D4np.Util` NuGet package; an optional fourth assembly ships separately as the opt-in `It.D4np.Util.MediatR` package (ADR-0037):

- Domain (`It.D4np.Util.Domain`) — Entity<TId, TEntity>, ValueObject, AggregateRoot<TId, TAggregate>, IDomainEvent, and the functional Result/Result<T>/Error model. Zero external dependencies.
- Application (`It.D4np.Util.Application`) — ISpecification<T>, IRepository<TEntity,TId>, IUnitOfWork, IEventBroker, ICacheService, IDateTimeProvider, IPasswordHasher, PaginatedList<T>. Depends on Domain only.
- Infrastructure (`It.D4np.Util.Infrastructure`) — SpecificationEvaluator (EF Core), EventBroker (Channels), PasswordHasher (PBKDF2), CacheService (two-level in-memory + distributed), DistributedCacheExtensions (Redis/JSON), DbContextExtensions, DateTimeProvider, ServiceCollectionExtensions. Depends on Application plus concrete tech. The single packable project of the core: the `It.D4np.Util` package bundles Domain + Application + Infrastructure.
- MediatR bridge (`It.D4np.Util.MediatR`, opt-in package) — DomainEventNotification<TEvent> (namespace `It.D4np.Util.MediatR`) and ValidationPipelineBehavior<TRequest, TResponse> (namespace `It.D4np.Util.MediatR.FluentValidation`), plus the DI extensions `AddD4npUtilMediatR` / `AddD4npUtilMediatRValidation`. Depends on the `It.D4np.Util` package plus MediatR and FluentValidation, so those two dependencies reach only consumers who opt in; kept out of the core so a DDD/Result-only consumer takes neither.

Dependency direction and the domain-event flow:

```mermaid
flowchart TB
  subgraph MediatR["It.D4np.Util.MediatR (opt-in package)"]
    DEN[DomainEventNotification to MediatR]
    VAL[ValidationPipelineBehavior / FluentValidation]
  end
  subgraph Infrastructure
    EVB[EventBroker / Channels]
    PH[PasswordHasher / PBKDF2]
    SE[SpecificationEvaluator / EF Core]
    DCE[DistributedCacheExtensions / Redis]
    DBX[DbContextExtensions]
  end
  subgraph Application
    SPEC[ISpecification]
    REPO[IRepository / IUnitOfWork]
  end
  subgraph Domain
    AGG[AggregateRoot raises IDomainEvent]
    RES[Result / Error]
  end
  Infrastructure --> Application --> Domain
  MediatR -. opt-in; depends on the It.D4np.Util package .-> Infrastructure
  AGG -. raised .-> DBX
  DBX -. on SaveChanges .-> DEN
  DEN -. publish .-> EVB
  EVB -. dispatch .-> H[IEventHandler]
```

## 5. Public Interface

Consumers import the core via the layer namespaces — `using It.D4np.Util.Domain;` / `It.D4np.Util.Application;` / `It.D4np.Util.Infrastructure;` (all shipped in the `It.D4np.Util` package; the bare `It.D4np.Util` namespace holds only `LibraryVersion`). The core public surface:

- `Entity<TId, TEntity>`, `AggregateRoot<TId, TAggregate>`, `ValueObject` — domain primitives.
- `IDomainEvent` — domain-event marker (raised in the Domain layer).
- `Result`, `Result<TValue>`, `Error`, `Result.Combine(...)` — functional error model.
- `IRepository<TEntity, TId>`, `ISpecification<TEntity>`, `SpecificationEvaluator` — query/persistence.
- `IUnitOfWork`, `DbContextExtensions`, `DomainEventSnapshot` — transaction + event dispatch (post-save change-tracker dispatch, or capture-before-save via `DomainEventSnapshot`).
- `IEventBroker`, `EventBroker`, `IEventHandler<TEvent>` — in-memory pub/sub.
- `IPasswordHasher`, `PasswordHasher` — password hashing.
- `ICacheService`, `DistributedCacheExtensions` — two-level caching.
- `IDateTimeProvider`, `DateTimeProvider`, `PaginatedList<T>` — utilities.
- `ServiceCollectionExtensions.AddD4npUtil(...)` — DI auto-registration (core services + event-handler scan).

The opt-in `It.D4np.Util.MediatR` package adds the MediatR/FluentValidation bridge — reference it only if you use them (ADR-0037):

- `DomainEventNotification<TEvent>` and the `DomainEventNotification.Create`/`Create<TEvent>` factory — namespace `It.D4np.Util.MediatR`.
- `ValidationPipelineBehavior<TRequest, TResponse>` — namespace `It.D4np.Util.MediatR.FluentValidation`.
- `ServiceCollectionExtensions.AddD4npUtilMediatR(...)` / `AddD4npUtilMediatRValidation(...)` — DI wiring for the validation pipeline behavior and the FluentValidation validator scan.

## 6. Verification & Test Strategy

Every public type has xUnit unit tests; FsCheck property tests assert the `ValueObject` equality laws (reflexive/symmetric/transitive) and `Result.Combine` identity/associativity. `PasswordHasher` has known-answer and round-trip tests plus assertions on iteration count (>= 600k) and salt uniqueness. `SpecificationEvaluator` is tested against EF Core InMemory and SQLite providers; the Redis cache layer is integration-tested via Testcontainers. `EventBroker` has a multi-threaded test asserting exactly-once delivery from N concurrent publishers. CI enforces nullable + analyzers as errors (`dotnet build -warnaserror`), `dotnet format --verify-no-changes`, coverage >= 80% (coverlet), across a Linux/Windows/macOS .NET 8 matrix. Each functional requirement maps to a test; the security, coverage, and format gates run on every PR.

Toolchain: built with dotnet SDK (MSBuild), Directory.Build.props, tested with xUnit (+ FsCheck for property tests), checked with
Nullable reference types + Roslyn analyzers (soundness); dotnet-counters (leaks), coverage target ≥ 80% line. Every functional and
non-functional requirement above maps to a CI gate (see [`.github/workflows/ci.yml`](../../.github/workflows/ci.yml)).
