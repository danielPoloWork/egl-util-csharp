# Roadmap — egl-util-csharp

The project's plan as a numbered, checkbox-driven list. When an item completes in a PR,
flip its checkbox (`- [ ]` → `- [x]`) **in the same PR**. New work goes at the bottom of
its section with a fresh `<milestone>.<task>` number; never renumber.

- **Versioning:** **v1.0.0 released** — the public API is frozen under SemVer (ADR-0010).
- **Session journal:** see [`docs/journal/`](journal/). Latest checkpoint: [2026-07-14 — feat: IPasswordHasher rehash-detection + span plaintext (#35, #41)](journal/2026/07/2026-07-14-feat-passwordhasher-rehash-and-span.md).

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

- [x] 2.1 Implement Entity<TId> with identity equality and a typed Id
- [x] 2.2 Implement ValueObject with structural equality, GetEqualityComponents, and hashing
- [x] 2.3 Implement AggregateRoot<TId> with the domain-event collection (RaiseDomainEvent/ClearDomainEvents)
- [x] 2.4 Define IDomainEvent (occurred-on via IDateTimeProvider) and DomainEventNotification<TEvent> (MediatR adapter)
- [x] 2.5 Property-test the equality laws and record the DDD building-block entries in the patterns catalogue

---

## Milestone 3 — Functional Result error model

Provide allocation-light, exception-free error handling.

- [x] 3.1 Implement Result (IsSuccess/IsFailure, Success/Failure factories)
- [x] 3.2 Implement Result<TValue> with guarded Value and implicit conversions
- [x] 3.3 Implement the immutable Error record with Error.None
- [x] 3.4 Implement Result.Combine over many results and property-test identity/associativity

---

## Milestone 4 — Persistence patterns

Repository, Specification, and Unit of Work isolating EF Core behind abstractions.

- [x] 4.1 Define IRepository<TEntity, TId> CRUD abstraction
- [x] 4.2 Define ISpecification<TEntity> (criteria, includes, ordering, paging)
- [x] 4.3 Implement SpecificationEvaluator over IQueryable and test on EF Core InMemory + SQLite
- [x] 4.4 Define IUnitOfWork (atomic SaveChangesAsync)
- [x] 4.5 Implement DbContextExtensions to extract and publish domain events around SaveChanges

---

## Milestone 5 — In-memory eventing & messaging

Async pub/sub on System.Threading.Channels, proven thread-safe.

- [x] 5.1 Define IEventBroker (async PublishAsync) and IEventHandler<TEvent>
- [x] 5.2 Implement EventBroker on Channels with a hosted background consumer and bounded back-pressure
- [x] 5.3 Add the multi-threaded delivery test (exactly-once) and document the threading contract

---

## Milestone 6 — Security & validation

Secure password hashing and automatic request validation.

- [x] 6.1 Define IPasswordHasher and implement PasswordHasher (PBKDF2 >= 600k, random salt, constant-time verify)
- [x] 6.2 Implement ValidationPipelineBehavior<TRequest, TResponse> running FluentValidation validators
- [x] 6.3 Add known-answer/round-trip hashing tests and validation-failure tests

---

## Milestone 7 — Caching & optimization

Two-level caching with a compact distributed serialization format.

- [x] 7.1 Define ICacheService two-level (in-memory + distributed) with GetOrCreateAsync/TTL/invalidation
- [x] 7.2 Implement DistributedCacheExtensions (compact System.Text.Json over IDistributedCache)
- [x] 7.3 Integration-test the Redis layer via Testcontainers

---

## Milestone 8 — Utilities, DI, and 1.0 hardening

Finish the utilities, freeze the API, package to NuGet, and cut v1.0.0.

- [x] 8.1 Implement IDateTimeProvider/DateTimeProvider and PaginatedList<T>
- [x] 8.2 Implement ServiceCollectionExtensions assembly-scanning auto-registration
- [x] 8.3 Freeze the public API, record the API-stability ADR, and reach >= 80% coverage
- [x] 8.4 Configure NuGet packaging (symbols, README, SemVer) and cut v1.0.0 via the release workflow

---

## Milestone 9 — Documentation i18n & README polish

Post-1.0 documentation work: translate the front-door docs (English stays the source of truth) and
bring the README to an enterprise polish. The README milestone table above tracks the product
milestones (1–8); this docs-infra milestone is tracked here only.

- [x] 9.1 Enable the content-digest `i18n-freshness` lint and add the `docs/i18n/` index + translation-status manifest (ADR-0011)
- [x] 9.2 Translate the README into Simplified Chinese (`docs/i18n/zh/README.md`) and Japanese (`docs/i18n/jp/README.md`)
- [x] 9.3 Enterprise README polish: NuGet version/downloads/CI/.NET/License badge row, quick-start, and a translations switcher

---

## Spec Coverage Map

Tracks which spec section is fulfilled by which roadmap item(s). Every spec section has a
row with at least one fulfilling item and a status glyph. Legend: ⏳ not started · 🚧 in
progress · ✅ done · ❎ N/A.

| Spec § | Requirement | Roadmap items | Status |
|--------|-------------|---------------|--------|
| §1 | Objective & business context | 1.1 | ✅ |
| §2 | Functional requirements | 1.1, 1.2, 2.1-2.5, 3.1-3.4, 4.1-4.5, 5.1-5.3, 6.1-6.3, 7.1-7.2, 8.1-8.2 | ✅ |
| §3 | Non-functional requirements | 1.3, 1.4, 5.2, 5.3, 6.1, 8.3 | ✅ |
| §4 | Logical architecture | 1.1, 2.1-2.4, 3.1-3.4, 4.1-4.5, 5.1-5.3, 6.1-6.2, 7.1-7.2, 8.1-8.2 | ✅ |
| §5 | Public interface | 1.2, 2.1-2.4, 3.1-3.4, 4.1-4.5, 5.1-5.3, 6.1-6.2, 7.1-7.2, 8.1-8.2 | ✅ |
| §6 | Verification & test strategy | 1.2, 1.4, 2.5, 3.4, 4.3, 5.3, 6.3, 7.3, 8.3 | ✅ |
