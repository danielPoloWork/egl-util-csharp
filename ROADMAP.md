# Roadmap — egl-util-csharp

The project's plan as a numbered, checkbox-driven list. When an item completes in a PR,
flip its checkbox (`- [ ]` → `- [x]`) **in the same PR**. New work goes at the bottom of
its section with a fresh `<milestone>.<task>` number; never renumber.

- **Versioning start:** pre-1.0 milestone-driven.
- **Session journal:** see [`docs/journal/`](docs/journal/). Latest checkpoint: _none yet_.

---

## Milestone 1 — Project bootstrap & CI

The thinnest slice that compiles, tests, and ships under the full quality bar.

- [x] 1.1 Lay down the build system (dotnet SDK (MSBuild), Directory.Build.props) and a buildable skeleton under
      `src/main/csharp/it/d4np/util/`.
- [x] 1.2 Wire the test framework (xUnit (+ FsCheck for property tests)) with one passing smoke test under
      `src/test/csharp/it/d4np/util/`.
- [x] 1.3 Add formatter + linter configs (dotnet format / CSharpier, Roslyn analyzers + .editorconfig (warnings as errors)) at the repo root.
- [x] 1.4 Stand up the CI matrix (Linux x64, Windows x64, macOS arm64 (.NET 8)) with build + test + format + lint.
- [x] 1.5 Seed the version constant (public const string Version = "X.Y.Z") in `Version.cs`.
- [x] 1.6 Add Directory.Build.props (net8.0, nullable enable, warnings-as-errors, deterministic build), global.json, and the .editorconfig analyzer ruleset
- [x] 1.7 Create the solution, the library project (It.D4np.Util) and the xUnit test project with a passing smoke test, and commit packages.lock.json

---

## Milestone 2 — DDD core building blocks

Ship the strongly-typed domain primitives with proven equality semantics.

- [ ] 2.1 Implement Entity<TId> with identity equality and a typed Id
- [ ] 2.2 Implement ValueObject with structural equality, GetEqualityComponents, and hashing
- [ ] 2.3 Implement AggregateRoot<TId> with the domain-event collection (RaiseDomainEvent/ClearDomainEvents)
- [ ] 2.4 Define IDomainEvent (occurred-on via IDateTimeProvider) and DomainEventNotification<TEvent> (MediatR adapter)
- [ ] 2.5 Property-test the equality laws and record the DDD building-block entries in the patterns catalogue

---

## Milestone 3 — Functional Result error model

Provide allocation-light, exception-free error handling.

- [ ] 3.1 Implement Result (IsSuccess/IsFailure, Success/Failure factories)
- [ ] 3.2 Implement Result<TValue> with guarded Value and implicit conversions
- [ ] 3.3 Implement the immutable Error record with Error.None
- [ ] 3.4 Implement Result.Combine over many results and property-test identity/associativity

---

## Milestone 4 — Persistence patterns

Repository, Specification, and Unit of Work isolating EF Core behind abstractions.

- [ ] 4.1 Define IRepository<TEntity, TId> CRUD abstraction
- [ ] 4.2 Define ISpecification<TEntity> (criteria, includes, ordering, paging)
- [ ] 4.3 Implement SpecificationEvaluator over IQueryable and test on EF Core InMemory + SQLite
- [ ] 4.4 Define IUnitOfWork (atomic SaveChangesAsync)
- [ ] 4.5 Implement DbContextExtensions to extract and publish domain events around SaveChanges

---

## Milestone 5 — In-memory eventing & messaging

Async pub/sub on System.Threading.Channels, proven thread-safe.

- [ ] 5.1 Define IEventBroker (async PublishAsync) and IEventHandler<TEvent>
- [ ] 5.2 Implement EventBroker on Channels with a hosted background consumer and bounded back-pressure
- [ ] 5.3 Add the multi-threaded delivery test (exactly-once) and document the threading contract

---

## Milestone 6 — Security & validation

Secure password hashing and automatic request validation.

- [ ] 6.1 Define IPasswordHasher and implement PasswordHasher (PBKDF2 >= 600k, random salt, constant-time verify)
- [ ] 6.2 Implement ValidationPipelineBehavior<TRequest, TResponse> running FluentValidation validators
- [ ] 6.3 Add known-answer/round-trip hashing tests and validation-failure tests

---

## Milestone 7 — Caching & optimization

Two-level caching with a compact distributed serialization format.

- [ ] 7.1 Define ICacheService two-level (in-memory + distributed) with GetOrCreateAsync/TTL/invalidation
- [ ] 7.2 Implement DistributedCacheExtensions (compact System.Text.Json over IDistributedCache)
- [ ] 7.3 Integration-test the Redis layer via Testcontainers

---

## Milestone 8 — Utilities, DI, and 1.0 hardening

Finish the utilities, freeze the API, package to NuGet, and cut v1.0.0.

- [ ] 8.1 Implement IDateTimeProvider/DateTimeProvider and PaginatedList<T>
- [ ] 8.2 Implement ServiceCollectionExtensions assembly-scanning auto-registration
- [ ] 8.3 Freeze the public API, record the API-stability ADR, and reach >= 80% coverage
- [ ] 8.4 Configure NuGet packaging (symbols, README, SemVer) and cut v1.0.0 via the release workflow

---

## Spec Coverage Map

Tracks which spec section is fulfilled by which roadmap item(s). Every spec section has a
row with at least one fulfilling item and a status glyph. Legend: ⏳ not started · 🚧 in
progress · ✅ done · ❎ N/A.

| Spec § | Requirement | Roadmap items | Status |
|--------|-------------|---------------|--------|
| §1 | Objective & business context | 1.1 | ⏳ |
| §2 | Functional requirements | 1.1, 1.2 | ⏳ |
| §3 | Non-functional requirements | 1.3, 1.4 | ⏳ |
| §4 | Logical architecture | 1.1 | ⏳ |
| §5 | Public interface | 1.2 | ⏳ |
| §6 | Verification & test strategy | 1.2, 1.4 | ⏳ |
