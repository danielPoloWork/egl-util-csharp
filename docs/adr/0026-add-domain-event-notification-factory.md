# ADR-0026: Add a DomainEventNotification factory so the MediatR bridge works for runtime-typed events

- **Status:** Accepted
- **Date:** 2026-07-13
- **Deciders:** Maintainer
- **Related:** ADR-0004 (DDD building blocks — introduced `DomainEventNotification<TEvent>`), ADR-0007 (event broker / MediatR bridge), #49 (trim/AOT annotations), spec §2/§5

## Context

`DomainEventNotification<TEvent>` is advertised (ADR-0004, ADR-0007, the patterns catalogue) as the
Adapter that carries a domain event onto MediatR's notification pipeline, but the library shipped **no
way to construct it for an event whose type is only known at runtime**. The library's own dispatch entry
point, `DbContextExtensions.DispatchDomainEventsAsync`, hands events to a
`Func<IDomainEvent, CancellationToken, Task>` callback, so the only non-reflective code a consumer can
write there is `mediator.Publish(new DomainEventNotification<IDomainEvent>(e))`. Because class generics
are invariant and MediatR resolves `INotificationHandler<TNotification>` by the notification's exact
constructed runtime type, that publishes `DomainEventNotification<IDomainEvent>` — and every handler
registered as `INotificationHandler<DomainEventNotification<OrderPlaced>>` **silently never fires** (zero
handlers is not an error in MediatR). The advertised bridge was non-functional for its primary usage
pattern (#61).

## Decision

Add an official factory (a non-generic `DomainEventNotification` static class, coexisting with the generic
type) with two overloads:

- **`Create<TEvent>(TEvent) : DomainEventNotification<TEvent>`** — reflection-free and trim/AOT-safe. Prefer
  it wherever the event type is known at compile time.
- **`Create(IDomainEvent) : INotification`** — for call sites (like the dispatch callback) that only have
  the runtime type. It closes `DomainEventNotification<>` over `domainEvent.GetType()` via `MakeGenericType`
  and a **compiled constructor delegate cached per event type**, so publishing the result reaches the
  concrete-typed handlers. Overload resolution routes a statically-`IDomainEvent` argument here and a
  statically-concrete argument to the generic overload.

The reflective overload (and its private builder) is isolated and annotated `[RequiresDynamicCode]` +
`[RequiresUnreferencedCode]`, so trimming/AOT builds surface the incompatibility (the #49 pattern). Native
AOT consumers use `Create<TEvent>` or an explicit per-type registration instead. Additive surface (a MINOR
change); no existing behavior changes.

## Alternatives Considered

- **Document the limitation only.** Rejected — it leaves the advertised Adapter silently non-functional for
  its primary usage pattern; documenting a footgun is not fixing it.
- **Only the reflective `Create(IDomainEvent)` overload.** Rejected — it would force even statically-typed
  call sites through the trim/AOT-unsafe path; the generic overload keeps the common case AOT-safe.
- **Emit `DomainEventNotification` from a source generator.** Deferred — a good future AOT-clean option;
  recorded here as the sanctioned Native-AOT alternative, but out of scope for this additive fix.

## Consequences

- The bridge is usable: `mediator.Publish(DomainEventNotification.Create(e))` in a dispatch callback reaches
  `INotificationHandler<DomainEventNotification<ConcreteEvent>>`. Covered by integration tests (an
  `OrderPlaced` reaches its concrete handler through both overloads; the open-typed wrap reaches none).
- The reflective overload is trim/AOT-unsafe and annotated as such; the generic overload and the existing
  constructor are unaffected. Additive and backward-compatible; accrues toward the next MINOR (1.1.0).
- The patterns catalogue and ADR-0007's bridge note point to this ADR.

## References

- Issue #61 (and #49 for the annotation pattern); ADR-0004; ADR-0007; `DomainEventNotification`.
