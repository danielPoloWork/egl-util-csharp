# ADR-0019: Remove ISpecification<> from DI assembly-scan auto-registration

- **Status:** Accepted
- **Date:** 2026-07-07
- **Deciders:** Maintainer
- **Related:** ADR-0006 (Specification pattern); ADR-0010 (DI auto-registration — this ADR supersedes its `ISpecification<>` registration decision); spec §2

## Context

`AddD4npUtil` (ADR-0010) reflection-scans the given assemblies and registers every concrete
`IEventHandler<>`, `IValidator<>`, and `ISpecification<>` implementation as transient. The first two are
correct: handlers and validators are stateless services the container constructs and then resolves.

Specifications are different. A specification is a *query object* built with its runtime arguments —
`Specification<TEntity>` sets its `Criteria` through the constructor (ADR-0006), and a real consumer spec
reads like `OrdersByCustomerSpec(Guid customerId)`. Registering such a type as
`ServiceDescriptor.Transient(ISpecification<TEntity>, OrdersByCustomerSpec)` produces a descriptor whose
constructor parameters the container cannot supply. Because `Host.CreateDefaultBuilder` and
`WebApplicationBuilder` set `ServiceProviderOptions.ValidateOnBuild = true` in the Development
environment, building the container then throws and **the host fails to start** — triggered merely by
defining an ordinary parameterized specification in a scanned assembly, with no opt-out short of moving
specs to an unscanned assembly (#67).

Even for a parameterless spec the registration is meaningless: with two specs over the same `TEntity`,
`GetService<ISpecification<TEntity>>()` returns whichever was registered last (scan order), and no code
in the library resolves `ISpecification<>` from the container at all.

## Decision

`AddD4npUtil` no longer scans for or registers `ISpecification<>` implementations. Handlers and
validators continue to be auto-registered. Specifications are constructed with their runtime arguments at
the call site, as the pattern intends (`new OrdersByCustomerSpec(customerId)`), and evaluated through
`SpecificationEvaluator` / a repository.

This **supersedes the `ISpecification<>` auto-registration decision in ADR-0010**; the rest of ADR-0010
(services, validators, handlers, API freeze, packaging) stands. It is treated as a **correctness fix**,
not a feature removal: the registration was never usable for the pattern's normal (parameterized) shape
and had no sound use for the parameterless shape.

## Alternatives Considered

- **Register only parameterless-constructible specs.** Rejected. It preserves a semantically meaningless
  registration (last-write-wins over `TEntity`; nothing in the library resolves it) and keeps a
  surprising, inconsistent subset auto-wired. Cleaner to drop specifications from the scan entirely and
  document the intended usage.
- **Keep the registration and only document the `ValidateOnBuild` pitfall.** Rejected — it leaves every
  consumer one parameterized spec away from a Development-host startup failure.
- **Treat as a breaking change requiring a MAJOR bump.** Considered. The removed behavior could not be
  used for the pattern's normal shape and is resolved by no library code, so it is recorded as a bug fix;
  the version bump is decided at release time per the project's versioning policy. Documented in the
  CHANGELOG under *Removed* with the migration note.

## Consequences

- A parameterized specification in a scanned assembly no longer breaks `ValidateOnBuild`; the Development
  host starts.
- Consumers who (unusually) resolved a parameterless specification from the container must now `new` it
  up where they use it. Documented in the CHANGELOG.
- The frozen spec §2 `ServiceCollectionExtensions` line and the README quick-start are updated to name
  only handlers and validators; ADR-0010's DI-auto-registration decision is annotated as
  superseded-in-part.
- `ISpecification<TEntity>`, `Specification<TEntity>`, and `SpecificationEvaluator` are unchanged — only
  the DI auto-registration of specs is removed.

## References

- Issue #67; ADR-0006 (Specification pattern); ADR-0010 (DI auto-registration, superseded in part here);
  spec §2.
