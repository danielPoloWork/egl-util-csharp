# 2026-07-08 — Docs: IDomainEvent.OccurredOnUtc DateTimeKind contract (#98)

Fourteenth from the zero-production-risk tier; a docs-accuracy clarification of a contract-only
invariant. No code/behavior change, so no CHANGELOG and no ADR.

## Done

- **Gap:** `IDomainEvent.OccurredOnUtc` is a plain `DateTime` documented as "the UTC instant", but the
  type cannot enforce UTC-ness. An implementation using `DateTime.Now` (Kind=Local), or an event
  rehydrated from JSON / EF Core (Kind=Unspecified when the text lacks a `Z`/offset), silently skews by
  the machine's UTC offset the moment anything converts or re-serializes it — in the one field the
  library standardizes across all domain events. The `DomainEventNotification`/outbox flows this library
  targets serialize events by design, so the round-trip Kind loss is a realistic path (#98).
- **Scope check:** the library **never reads** `OccurredOnUtc` internally (grep: only the interface
  declares it; the broker/dispatch machinery ignores the timestamp), so the recommended optional
  "`Debug.Assert`/guard before publishing" has **no consumption point** to attach to — documentation is
  the complete practical remedy. Switching to `DateTimeOffset` is a breaking change deferred to v2.
- **Fix (docs-only):** expanded the `OccurredOnUtc` XML doc with a `<remarks>` stating the Kind=Utc
  contract, the sanctioned source (`IDateTimeProvider.UtcNow` / `DateTime.UtcNow`, never `DateTime.Now`),
  the exact skew mechanism (`ToUniversalTime`/`ToLocalTime`/re-serialize), the serialization round-trip
  hazard (`System.Text.Json` and EF Core both yield `Unspecified`), the normalization requirement
  (`DateTime.SpecifyKind(value, DateTimeKind.Utc)`), and the deferred `DateTimeOffset` option. Added a
  supporting note to `IDateTimeProvider.UtcNow` that its contract is `Kind=Utc` (so the sanctioned source
  the `IDomainEvent` doc points to is itself self-consistent — a fake must honour it too).
- **Review catch:** a 3-lens adversarial pass (dotnet-accuracy / consistency / skeptic) — all three
  FIX_NEEDED — caught that a first draft, in a comment whose *sole purpose* is `DateTimeKind` precision,
  itself overstated the failure mode: "*calls `ToUniversalTime` **or** `ToLocalTime` … the instant shifts
  by the machine's UTC offset*". `ToLocalTime` on a `Kind=Local` value is a **no-op** (no shift), so the
  blanket claim is false. Anchored the corruption on `ToUniversalTime` (re-bases `Local`/`Unspecified`;
  no-op only on true `Utc`) and dropped `ToLocalTime` from the shift list. Also: "*deferred to a future
  major version*" → "*possible only in a future major version*" (it read as a committed v2 promise, but
  the API is frozen and future work is maintenance-only), and ordered `IDateTimeProvider.UtcNow`
  "(preferred, for testability)" ahead of `DateTime.UtcNow` to keep the type's DI guidance. Rejected the
  one cref-hygiene nit: `IDomainEvent`→`IDateTimeProvider` is the illegal Domain→Application direction, so
  the plain `<c>` reference is deliberate and correct.
- `dotnet build -warnaserror` clean (all BCL crefs resolve); `dotnet format` clean; 172 pass / 3 Redis
  skip (unchanged).

## State

- v1.0.0 released. Docs-only (two XML doc blocks); no behavior/API change. PR drafted on
  `docs/idomainevent-datetimekind`, awaiting merge. Wording adversarially reviewed before drafting. CI
  still billing-blocked (verified locally). No local Python → `consistency_lint` hand-verified; touches
  `IDomainEvent.cs` + `IDateTimeProvider.cs` (doc comments), this journal (+ index row), and the ROADMAP
  pointer — no lint invariant affected. README untouched → no i18n digest recompute.

## Next

- Continue docs-accuracy safest-first: #94 (SECURITY.md supported-versions table stale post-1.0 — a
  small policy call). Then #51/#52 (cache/validation concurrency). The remaining `[fix]` Mediums
  (#31/#33/#25/#26/#62/#71/#61) carry design decisions → ADR + a maintainer AskUserQuestion.
