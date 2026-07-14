# ADR-0028: Canonical-type entity equality and transient identity (self-referencing Entity&lt;TId, TEntity&gt;)

- **Status:** Accepted
- **Date:** 2026-07-14
- **Deciders:** Maintainer
- **Breaking:** Yes — reshapes `Entity<TId>` → `Entity<TId, TEntity>` and `AggregateRoot<TId>` →
  `AggregateRoot<TId, TAggregate>`. A **MAJOR** change (2.0); supersedes the equality parts of ADR-0004
  and breaks the API frozen by ADR-0010.
- **Related:** ADR-0004 (DDD building blocks), ADR-0010 (1.0 API freeze), #25, #26, spec §2/§4/§5

## Context

`Entity<TId>` based equality on the **runtime** type (`GetType() == other.GetType()`) and the raw `Id`,
with the constructor rejecting only a null `Id`. Two defects (a Medium pair):

- **#26 — EF Core proxies.** With lazy-loading/change-tracking proxies, EF materializes a runtime subclass
  (`CustomerProxy : Customer`). `GetType()` differs between a proxy-loaded instance and a hand-built one of
  the same `Id`, so they compare **unequal** and miss in set/dictionary lookups. The Domain assembly has
  **zero external dependencies** (ADR-0003), so it cannot reference EF Core to unwrap the proxy type, and a
  namespace/assembly-name proxy heuristic is fragile across proxy providers.
- **#25 — transient identity.** A default `Id` (`0`, `Guid.Empty`) was treated as a real identity, so two
  distinct unsaved entities compared **equal** and collapsed into a single slot in a `HashSet`/`Dictionary`
  — silently dropping an entity. The classic transient-vs-persisted identity trap.

## Decision

Define the equality type **explicitly** via a self-referencing generic base, rather than inspecting the
runtime type; and give transient entities reference identity:

- **`Entity<TId, TEntity> where TEntity : Entity<TId, TEntity>`.** `TEntity` is the entity's **canonical
  domain type**. Equality compares the canonical type (via the closed generic / `typeof(TEntity)`), not
  `GetType()`. An EF proxy — or any subclass — inherits the same closed base, so its canonical type equals
  its parent's and a proxy compares equal to a plain instance of the same identity, with **no EF reference
  and no proxy-name heuristic** (#26). A concrete entity closes the generic over itself:
  `class Customer : Entity<Guid, Customer>`. `AggregateRoot<TId, TAggregate>` and `IRepository`'s constraint
  follow.
- **Transient identity (#25).** An entity whose `Id` equals `default(TId)` (`0`/`Guid.Empty`) is transient
  and compares by **reference** (equal only to itself; hashes by reference), so distinct unsaved entities
  never merge. A non-default `Id` compares by identity as before. A reference-typed `TId` is never transient
  (the constructor still rejects a null `Id`). This fits the common EF "`Id = 0` until the store assigns the
  key" pattern.

## Alternatives Considered

- **A `GetType()`-based comparison with proxy-name detection** (Castle.Proxies / dynamic-assembly). Rejected —
  fragile across proxy providers and couples the pure Domain layer to proxy internals; explicitly ruled out.
- **A mandatory protected `EntityType` member** each entity implements. A valid canonical-type mechanism (and
  keeps the generic arity), but pushes per-entity boilerplate and a runtime member; the self-referencing
  generic is compile-time-enforced and needs no member body.
- **Rejecting a default `Id` in the constructor** (for #25). Rejected — it breaks the common EF pattern of
  constructing an entity with `Id = 0` before the database assigns the key.
- **Reference-equality only, no canonical type / documentation only.** Rejected — leaves the advertised,
  tested equality broken under proxies and the transient-collapse footgun in place.

## Consequences

- **Breaking (MAJOR / 2.0).** Every entity/aggregate must close the generic over itself:
  `class Order : AggregateRoot<Guid, Order>`. There is no longer a common non-generic `Entity<TId>` base, so
  heterogeneous entity comparison goes through `object`/`IEquatable`. `IRepository<TEntity, TId>`'s constraint
  becomes `AggregateRoot<TId, TEntity>`.
- **Behavior:** proxies and plain instances of the same identity now compare equal (fixes #26); a subclass
  shares its parent's canonical identity by design. Transient entities compare by reference (fixes #25).
  The previous "same default `Id` ⇒ equal" behavior — and its property test — is replaced.
- Supersedes the equality parts of ADR-0004 and breaks the ADR-0010 freeze; the frozen spec (§2/§4/§5) is
  amended. Version accrues to the next **MAJOR** (2.0.0) at release.
- No new external dependency; Domain stays dependency-free. Covered by tests: proxy/plain symmetry,
  transitivity, hash-code consistency, different canonical types sharing an `Id`, and transient reference
  identity.

## References

- Issues #25, #26; ADR-0003 (layering), ADR-0004, ADR-0010; spec §2/§4/§5; `Entity`, `AggregateRoot`, `IRepository`.
