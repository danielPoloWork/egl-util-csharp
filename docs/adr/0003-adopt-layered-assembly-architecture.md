# ADR-0003: Adopt a layered assembly architecture (Clean / Onion)

- **Status:** Accepted
- **Date:** 2026-06-30
- **Deciders:** Maintainer
- **Related:** ADR-0002, AGENTS.md §8, spec §3 (non-functional) and §4 (architecture)

## Context

The spec's non-functional requirements mandate that **the domain layer has zero third-party
dependencies**, while outer concerns depend on MediatR, FluentValidation, EF Core, and the
`Microsoft.Extensions` abstractions (spec §3). The logical architecture is Clean / Onion with
dependencies pointing inward toward the domain (spec §4). Milestone 2 introduces the first type
that needs a framework — `DomainEventNotification<TEvent>` adapts a domain event onto MediatR —
so the structural choice can no longer be deferred. Packaging all of this in a single assembly
would force MediatR (and later EF Core, Redis) onto every consumer of the pure domain types,
violating the dependency policy at the assembly boundary where it is actually enforceable.

## Decision

We organize the library as **separate assemblies, one per architectural layer**, with
dependencies pointing strictly inward:

- **`It.D4np.Util.Domain`** — entities, value objects, aggregate roots, domain events. **Zero
  third-party package references.**
- **`It.D4np.Util.Application`** — use-case-facing abstractions and adapters (e.g.
  `DomainEventNotification<TEvent>`). References `Domain` plus messaging/validation *contracts*
  only (MediatR.Contracts today).
- **`It.D4np.Util.Infrastructure`** — concrete technology adapters (EF Core, Redis, PBKDF2).
  Introduced when the first such type lands (Milestone 4); references `Application`.

Each layer is a project under the existing Maven-style tree
(`src/main/csharp/it/d4np/util/<Layer>/`), consistent with ADR-0002's "subdivide by component".

## Alternatives Considered

- **A single assembly with namespace-only layering.** Rejected — namespaces do not enforce the
  dependency direction at compile time, and a single assembly leaks MediatR/EF Core as transitive
  dependencies to consumers that only want the pure domain types, breaking the spec §3 policy.
- **Two assemblies (domain + everything-else).** Rejected — it collapses Application and
  Infrastructure, re-coupling framework-free use-case abstractions to concrete tech (EF Core,
  Redis) and defeating testability of the application layer in isolation.

## Consequences

- The domain compiles and tests with no third-party packages, and consumers reference only the
  layers they need; the dependency policy is enforced by the project graph, not by convention.
- More projects to manage and reference; the test project references every layer.
- `Infrastructure` is intentionally absent until Milestone 4 — adding it later is additive.
- The version constant (`Version.cs`) lives in `Domain`; `tools/consistency_lint.py` is pointed
  at its new path.

## References

- spec §3 (dependency policy), §4 (logical architecture); ADR-0002; AGENTS.md §5, §8.
- `docs/patterns/design-patterns.md` — Architectural Application Styles: *Clean / Onion*.
