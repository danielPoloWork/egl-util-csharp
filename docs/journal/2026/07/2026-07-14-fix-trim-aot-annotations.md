# 2026-07-14 — Fix: trim/AOT annotations on the reflective entry points (#49)

Roadmap PR 4 (correctness cluster). Metadata-only annotations; CHANGELOG `Changed`, no ADR.

## Done

- **Gap:** `EventBroker` dispatch (`MakeGenericType`/`MakeGenericMethod`/`GetMethod`) and `AddD4npUtil`
  assembly scanning (`Assembly.GetTypes`) are trim/AOT-unsafe but carried no annotations, so a consumer's
  trimming/Native-AOT build got no warning (#49).
- **Fix:** annotated the reflective **public entry points** — `ServiceCollectionExtensions.AddD4npUtil` and
  the `EventBroker` constructor — with `[RequiresDynamicCode]` and `[RequiresUnreferencedCode]` (clear
  messages pointing to the AOT-safe alternatives). A consumer's trim/AOT build now surfaces the
  incompatibility at the call site. (`DomainEventNotification.Create(IDomainEvent)` was already annotated
  in #61.) No runtime-behavior change.
- **Build note:** the project doesn't enable the trim/AOT analyzers, so the attributes are metadata-only —
  no IL2026/IL3050 churn on the internal reflective calls or the existing call sites. They take effect in a
  *consumer's* trim/AOT build.
- **Test:** a reflection test asserts both attributes are present on `AddD4npUtil` and the `EventBroker`
  ctor (guards against accidental removal; removing an attribute reddens it).
- `dotnet build -warnaserror` clean; `dotnet format` clean; DI suite 13 pass; full suite green.

## State

- v1.0.0 released. Metadata-only public-surface annotation (`AddD4npUtil` + `EventBroker` ctor); CHANGELOG
  `Changed`; no ADR. A consumer with `<IsAotCompatible>`/trimming will now see new warnings — noted in the
  CHANGELOG. PR drafted on `fix/trim-aot-annotations`, awaiting merge. CI billing-blocked (verified
  locally). No local Python → `consistency_lint` hand-verified; README untouched.

## Next

- Roadmap PR 5: **#43 + #44** — EventBroker handler-failure visibility when no `ILogger` is supplied, and a
  bound on the process-wide static dispatcher cache. Same subsystem, tight batch. #43 may carry a small
  design choice (how to surface failures without a logger) → assess when reading it.
