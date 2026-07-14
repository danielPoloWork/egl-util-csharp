# ADR-0004: Adopt the DDD tactical building blocks

- **Status:** Accepted
- **Superseded in part by:** [ADR-0028](0028-canonical-type-entity-equality-and-transient-identity.md) — `Entity`/`AggregateRoot` become self-referencing generics (`Entity<TId, TEntity>`), with canonical-type equality and transient (default-`Id`) reference identity (breaking, 2.0). The DDD building-block decisions otherwise stand.
- **Date:** 2026-06-30
- **Deciders:** Maintainer
- **Related:** ADR-0003, spec §2 (functional requirements 1–5)

## Context

The library's purpose is to supply the recurring tactical building blocks of Domain-Driven
Design so that consuming projects do not re-implement them. Spec §2 enumerates the first five:
a strongly-typed `Entity<TId>`, a `ValueObject` with structural equality, an `AggregateRoot<TId>`
that accumulates domain events, the `IDomainEvent` marker, and `DomainEventNotification<TEvent>`
that carries a domain event onto the application's MediatR notification pipeline. These are
classical patterns; the value is a correct, well-tested, dependency-free implementation.

## Decision

We adopt the **Domain-Driven Design building-block patterns** as the public domain surface:

- **`Entity<TId>`** — identity equality: equal iff same concrete type and equal `Id`
  (`where TId : notnull`). Implements `IEquatable<>` and `==`/`!=`.
- **`ValueObject`** — structural equality derived from an explicit `GetEqualityComponents()`
  sequence, with a matching `GetHashCode`. Treated as immutable.
- **`AggregateRoot<TId>`** — an `Entity<TId>` that buffers raised `IDomainEvent`s in a read-only
  collection, exposing `RaiseDomainEvent` (protected) and `ClearDomainEvents`.
- **`IDomainEvent`** — marker carrying an `OccurredOnUtc` timestamp stamped by the raiser (so
  event time is testable via an injected clock, arriving in Milestone 8).
- **`DomainEventNotification<TEvent>`** (Application layer) — the **Adapter** pattern: it adapts
  an `IDomainEvent` onto MediatR's `INotification`, decoupling the domain from the messaging
  infrastructure (per the layering of ADR-0003).

The equivalence-relation laws (reflexive, symmetric, transitive) and the equal-implies-equal-hash
contract are verified with FsCheck property tests.

## Alternatives Considered

- **C# `record` types alone for value objects.** Rejected as the *library contract* — records
  give value equality but not a shared, discoverable base abstraction that the rest of the
  library (and consumers) can program against; consumers may still implement a `ValueObject` with
  a record internally.
- **Depend on an existing DDD library** (e.g. a third-party seedwork package). Rejected — the
  entire point of this artifact is to *provide* these blocks with zero third-party dependencies
  in the domain (spec §3, ADR-0003).

## Consequences

- Consumers derive their domain types from these bases and get correct equality and event
  buffering for free; the equality laws are guarded by property tests.
- `DomainEventNotification<TEvent>` introduces a dependency on the MediatR notification contract
  — confined to the Application assembly, never the Domain (ADR-0003).
- Catalogued in `docs/patterns/README.md`: *Clean / Onion*, *Domain-Driven Design building
  blocks*, and *Adapter*.

## References

- spec §2 (functional requirements 1–5); ADR-0003.
- `docs/patterns/design-patterns.md` — Architectural: *Domain-Driven Design building blocks*;
  Structural: *Adapter*.
