# 2026-07-07 — Fix: assembly scan skips open-generic implementors (#65)

Fifth fix from the Medium/Low audit tier. Correctness/robustness: one open-generic handler, validator,
or specification anywhere in a scanned assembly bricked startup for every `AddD4npUtil` consumer.

## Done

- **Bug:** `RegisterImplementations` filtered only on `type.IsAbstract || type.IsInterface` and never
  checked for unbound generic parameters. For an open-generic implementor — e.g.
  `class AuditHandler<TEvent> : IEventHandler<TEvent>`, or a non-abstract generic FluentValidation
  validator — `type.GetInterfaces()` yields the *open constructed* interface `IEventHandler`1[TEvent]`,
  which still matches `GetGenericTypeDefinition() == openGenericInterface`, so the scan registered
  `ServiceDescriptor.Transient(IEventHandler<TEvent>, AuditHandler<>)`. In MS.DI 8, `CallSiteFactory`
  runs at `BuildServiceProvider()` time and throws `ArgumentException` — "Cannot instantiate
  implementation type `AuditHandler`1[TEvent]` for service type `IEventHandler`1[TEvent]`" — because the
  implementation is a generic type definition while the service type is not. The whole container fails
  to build and the app cannot start, **regardless of `ValidateOnBuild`**. Generic handlers/validators
  are a common pattern; MediatR's and FluentValidation's own scanners deliberately skip generic
  definitions, this one did not (#65).
- **Fix:** skip any type with unbound generic parameters —
  `if (type.IsAbstract || type.IsInterface || type.ContainsGenericParameters) continue;`.
  `ContainsGenericParameters` is the correct, defensive check (a superset of `IsGenericTypeDefinition`);
  DI cannot construct such a type, so it must not be auto-registered. Only closed, `new`-able types are
  registered — matching the documented "concrete type" intent. The XML doc remark and the method comment
  now state the exclusion. No ADR (routine correctness fix aligning the scanner with framework norms; no
  API/design change). Registering the open-generic *pair* (as MediatR optionally does) is a deliberate
  non-goal here — it would add generic-handler support (a feature), not fix the crash.
- **Test:** `Open_generic_implementors_are_skipped_so_the_container_builds` — the test assembly now
  defines `OpenGenericEventHandler<TEvent>` and `OpenGenericValidator<T>`; the test asserts neither is
  registered (`ServiceCollection` inspection) and that `BuildServiceProvider()` succeeds while the
  ordinary closed `SampleEventHandler` still resolves. Verified it fails pre-fix (the scan registers both
  open definitions and `BuildServiceProvider` throws), passes post-fix. `dotnet build -warnaserror`
  clean; `dotnet format` clean; 147 pass / 2 Redis skip.
- Docs: CHANGELOG `[Unreleased]` Fixed.

## State

- v1.0.0 released. No public surface change → patch-level. PR drafted on `fix/scan-skips-open-generics`,
  awaiting maintainer merge. Actions still billing-blocked; verified locally (no local Python, so
  `consistency_lint.py` not run — the change touches only source, tests, CHANGELOG, and this journal,
  none of which affect the README↔ROADMAP / patterns / i18n invariants the lint checks).

## Next

- Continue Medium/Low safest-first: #67 (drop `ISpecification<>` from the scan — parameterized specs
  break `ValidateOnBuild`; same file, natural follow-up), then the core `Result` invariants
  (#29/#27/#28).
