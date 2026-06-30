# 2026-06-30 — Milestone 4: persistence patterns

## Done

- Introduced the `It.D4np.Util.Infrastructure` assembly (EF Core 8.0.28), deferred in ADR-0003
  (now ADR-0006).
- Application abstractions: `IRepository<TEntity, TId>`, `ISpecification<TEntity>` + a
  `Specification<TEntity>` base (criteria / includes / ordering / paging), `IUnitOfWork`.
- Infrastructure: `SpecificationEvaluator` (applies a spec to `IQueryable` via EF Core; requires an
  ordering when paging; supports string-based multi-level includes) and `DbContextExtensions`
  (`PeekDomainEvents` / `ClearDomainEvents` / `DispatchDomainEventsAsync` — publish-then-clear-on-
  success, at-least-once delivery).
- Domain: added the non-generic `IHasDomainEvents` marker, implemented by `AggregateRoot<TId>`.
- 54 tests pass, including `SpecificationEvaluator` integration tests on **EF Core InMemory and
  SQLite**. build `-warnaserror`, test, format, and `--locked-mode` all green.
- Adopted patterns: Repository, Specification, Unit of Work (ADR-0006).
- An adversarial multi-dimension review workflow caught (and this PR fixes): domain events were
  cleared *before* publishing (now publish-then-clear-on-success), no multi-level eager loading
  (added `IncludeStrings`), and paging without a required ordering (now rejected).

## Notes

- EF Core's key convention does not auto-detect the library's inherited get-only `Id`; consumers
  configure it explicitly (`HasKey(e => e.Id)`) and supply a parameterless ctor for materialization
  (documented in ADR-0006).

## State

- M1–M3 and the CI-hygiene PR are merged. M4 on `feat/persistence-patterns`; PR drafted, awaiting
  maintainer merge. Version remains `0.0.0`.

## Next

- Milestone 5 — in-memory eventing & messaging (`IEventBroker`/`EventBroker` on Channels,
  `IEventHandler<TEvent>`) — after M4 merges.
