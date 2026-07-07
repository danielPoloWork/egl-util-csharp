# 2026-07-07 — Fix: idempotent ValidationPipelineBehavior registration (#32)

Second fix from the Medium/Low audit tier. Self-contained DI-wiring fix, aligning the lone
non-idempotent registration with the rest.

## Done

- **Bug:** `AddD4npUtil` registered the open-generic MediatR pipeline behavior with plain
  `AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationPipelineBehavior<,>))` — the only
  non-idempotent line (everything else uses `TryAdd*`). Calling `AddD4npUtil` twice (a documented
  pattern: a library and its host both call it) appended two descriptors, so MediatR ran every
  FluentValidation validator twice per request — doubling e.g. `MustAsync` DB-uniqueness queries (#32).
- **Fix:** `services.TryAddEnumerable(ServiceDescriptor.Transient(typeof(IPipelineBehavior<,>),
  typeof(ValidationPipelineBehavior<,>)))`. TryAddEnumerable dedupes by implementation type, so repeated
  calls register the behavior once. MediatR still resolves it via `IEnumerable<IPipelineBehavior<…>>`.
  No ADR (routine DI-idempotency fix, no API/design change).
- **Tests:** added `Registering_twice_leaves_a_single_validation_behavior` — after two `AddD4npUtil`
  calls, exactly one `ValidationPipelineBehavior` descriptor is present (the pre-existing "registering
  twice" test only checked the hosted broker). `dotnet build -warnaserror` clean; `dotnet format` clean;
  143 pass / 2 Redis skip.
- Docs: CHANGELOG `[Unreleased]` Fixed.

## State

- v1.0.0 released. No public surface change → patch-level. PR drafted on
  `fix/idempotent-validation-behavior`, awaiting maintainer merge. Pipeline still Actions
  billing-blocked; verified locally.

## Next

- Continue Medium/Low safest-first. Candidate `[fix][Medium]`: #59 (JSON-null L2 poisons L1), #60
  (EventBroker sync scope dispose / IAsyncDisposable leak), then the core Result invariants (#29/#27/#28).
