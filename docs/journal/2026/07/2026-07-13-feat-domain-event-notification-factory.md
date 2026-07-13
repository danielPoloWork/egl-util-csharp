# 2026-07-13 — Feat: DomainEventNotification factory for runtime-typed events (#61)

An additive fix that makes the advertised MediatR Adapter actually usable. Carries ADR-0026 and a
CHANGELOG `Added` entry (a MINOR-tier addition).

## Done

- **Bug:** `DomainEventNotification<TEvent>` is sold (ADR-0004/0007, patterns catalogue) as the Adapter onto
  MediatR, but the library shipped no factory to close it over an event's **runtime** type. The only
  non-reflective code a `DispatchDomainEventsAsync` callback (`Func<IDomainEvent, …>`) can write —
  `new DomainEventNotification<IDomainEvent>(e)` — publishes `DomainEventNotification<IDomainEvent>`, and
  since class generics are invariant and MediatR resolves handlers by the exact constructed type, every
  `INotificationHandler<DomainEventNotification<OrderPlaced>>` **silently never fired** (#61).
- **Decision (maintainer via AskUserQuestion):** add an official factory with **both** overloads — a
  strongly-typed AOT-safe `Create<TEvent>` and a reflective `Create(IDomainEvent)` — not merely document.
- **Change:** a non-generic `DomainEventNotification` static class (coexists with the generic type):
  `Create<TEvent>(TEvent) : DomainEventNotification<TEvent>` (reflection-free) and `Create(IDomainEvent) :
  INotification` (closes `DomainEventNotification<>` over `GetType()` via a **compiled constructor delegate
  cached per event type**). Overload resolution routes a statically-`IDomainEvent` arg to the reflective
  overload and a concrete-typed arg to the generic. The reflective path is isolated and annotated
  `[RequiresDynamicCode]`/`[RequiresUnreferencedCode]` (the #49 pattern); ADR-0026 records the Native-AOT
  alternative (generic overload / explicit factory / source-gen). ADR-0007's bridge note + the patterns
  catalogue point to ADR-0026.
- **Tests:** integration via a real MediatR container — `Create(IDomainEvent)` and `Create<TEvent>` both
  reach `INotificationHandler<DomainEventNotification<OrderPlaced>>`; the open-typed
  `DomainEventNotification<IDomainEvent>` wrap reaches **none** (pins the trap); both `Create` overloads
  reject null.
- **Verified:** build `-warnaserror` clean (the trim/AOT attributes are metadata — the analyzers aren't
  enabled, so no build churn); `dotnet format` clean; notification suite 7 pass; full suite green.
  Mutation-checked — see below.

## State

- v1.0.0 released. **Additive** public surface (a new `DomainEventNotification` static class with `Create`
  overloads); backward-compatible, accrues toward **1.1.0**. ADR-0026; CHANGELOG `Added`; patterns
  catalogue + ADR-0007 pointer updated. README doesn't detail the type (blurb only) → no README/i18n
  change. PR drafted on `feat/domain-event-notification-factory`, awaiting merge. CI still billing-blocked
  (verified locally). No local Python → `consistency_lint` hand-verified: ADR index gains ADR-0026,
  patterns table row updated (patterns check), spec-map unaffected, no version/lockfile change.

## Next

- Remaining design-decision `[fix]` Mediums, each with an ADR + AskUserQuestion: #71 (contravariant
  handler variance — the `in` on `IEventHandler` advertises consumption the broker doesn't deliver), and
  #25/#26 (Entity transient-identity / EF-proxy equality — property-tested; the last and riskiest, touching
  core equality).
