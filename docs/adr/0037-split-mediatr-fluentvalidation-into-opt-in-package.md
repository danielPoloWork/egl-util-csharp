# ADR-0037: Split the MediatR/FluentValidation building blocks into the opt-in It.D4np.Util.MediatR package — breaking (2.0)

- **Status:** Accepted
- **Date:** 2026-07-15
- **Deciders:** Maintainer
- **Related:** ADR-0004 (DDD blocks / Adapter), ADR-0008 (validation pipeline behavior), ADR-0010 (packaging), ADR-0026 (DomainEventNotification factory), ADR-0032 (analyzer gate), #38, #49, #90

## Context

The 1.0 surface delivered every layer as the **single** `It.D4np.Util` NuGet package, and two of its building
blocks are coupled to MediatR and FluentValidation:

- `DomainEventNotification<TEvent>` — the Adapter (ADR-0004/0026) that carries an `IDomainEvent` onto MediatR's
  `INotification` contract.
- `ValidationPipelineBehavior<TRequest, TResponse>` — the Chain-of-Responsibility (ADR-0008) that runs
  FluentValidation validators as a MediatR `IPipelineBehavior`.

Both lived in `It.D4np.Util.Application`, so that project referenced the full `MediatR` (12.5.0) and
`FluentValidation` (11.11.0) packages; `It.D4np.Util.Infrastructure` (the single packable project) mirrored
both — for its DI wiring and because the bundling invariant (ADR-0010, #63) requires the packable project to
carry every bundled assembly's third-party dependency. The upshot: a consumer using only the DDD/Result core
(entities, the `Result`/`Error` model, specifications, the repository/unit-of-work contracts, the event broker,
password hashing, the two-level cache) still transitively pulled MediatR **and** FluentValidation — two sizable
dependencies, a larger attack/upgrade surface, and a CQRS/validation opinion — that they never used. #38 asks to
make those two dependencies **opt-in** without losing the building blocks.

## Decision

Extract the two MediatR/FluentValidation-coupled types and their DI wiring into a **new packable project and
assembly**, `It.D4np.Util.MediatR` (NuGet package `It.D4np.Util.MediatR`), and strip both dependencies from the
core. The shape (naming and namespaces decided by the maintainer via AskUserQuestion):

- **Package `It.D4np.Util.MediatR`** — MediatR is the primary integration boundary and both exported types
  participate in its pipeline. Deliberately **not** `Cqrs` or `Messaging`: those advertise broader architectural
  capability than the package implements.
- **Capability-specific namespaces.** `DomainEventNotification<TEvent>` (and its `Create`/`Create<TEvent>`
  factory) sits under `It.D4np.Util.MediatR`; `ValidationPipelineBehavior<TRequest, TResponse>` sits under
  `It.D4np.Util.MediatR.FluentValidation` (it is FluentValidation-specific).
- **Granular DI extensions.** `AddD4npUtilMediatRValidation(...)` registers the pipeline behavior and
  assembly-scans FluentValidation validators; `AddD4npUtilMediatR(...)` is a convenience umbrella that enables
  the package's building blocks in one call. The `DomainEventNotification` adapter is a type/factory used
  directly (`Create`), so it needs no registration — the umbrella therefore just delegates to
  `AddD4npUtilMediatRValidation`.
- **Dependency shape.** The new project references the core packable project (Infrastructure) with default
  assets, so the published package takes a NuGet dependency on `It.D4np.Util`; it references Domain
  `PrivateAssets="all"` **only to compile** against `IDomainEvent` (Domain's assembly ships inside
  `It.D4np.Util`, so it is neither re-bundled nor re-declared). It adds `MediatR` and `FluentValidation` as its
  own dependencies. The **core `It.D4np.Util` package now declares neither** — verified two ways (see below).
- **The core `AddD4npUtil` no longer registers the pipeline behavior or scans validators** — that wiring moved
  to `AddD4npUtilMediatRValidation`. Its remarks point consumers to the opt-in package.

Two mechanical consequences of the naming, handled in-tree:

- The new namespaces `It.D4np.Util.MediatR(.FluentValidation)` **shadow** the `MediatR` and `FluentValidation`
  package namespaces from anywhere under the `It.D4np.Util.*` tree, so the package sources and the consuming
  tests qualify the packages with `global::` (`using global::MediatR;`).
- The two new reflective public entry points assembly-scan (`Assembly.GetTypes`) and register an open-generic
  behavior that DI closes via `MakeGenericType`, so they carry the same trim/AOT annotations the core enforces
  (`[RequiresUnreferencedCode]`/`[RequiresDynamicCode]`, #49) and `[MethodImpl(NoInlining)]` for the
  `GetCallingAssembly` default scan (#90) — the split does not create an un-annotated reflective entry point.

## Alternatives Considered

- **Keep both types in the core, document the dependency.** Rejected — it leaves MediatR + FluentValidation on
  every consumer, which is exactly the cost #38 removes.
- **Name the package `It.D4np.Util.Cqrs` / `.Messaging`.** Rejected (maintainer) — the package is a thin MediatR
  adapter + a validation behavior, not a CQRS or messaging framework; the name would over-promise.
- **One flat namespace for both types.** Rejected — `ValidationPipelineBehavior` is FluentValidation-specific;
  capability-specific namespaces (`.FluentValidation`) keep the surface honest and leave room for a
  MediatR-only consumer to ignore the validation half.
- **Two separate packages (a MediatR adapter and a FluentValidation behavior).** Rejected — both participate in
  the MediatR pipeline and share the `global::` and DI concerns; one package with capability-specific
  namespaces and granular DI extensions is simpler to consume and version.
- **Register the `DomainEventNotification` adapter in DI.** Rejected — it is a type constructed directly and a
  static `Create` factory; there is nothing to register. The umbrella `AddD4npUtilMediatR` delegates to the
  validation registration rather than inventing a no-op adapter registration.

## Consequences

- **Breaking (2.0).** A consumer of `DomainEventNotification<TEvent>` / `DomainEventNotification.Create` /
  `ValidationPipelineBehavior<,>` must:
  1. add the package — `dotnet add package It.D4np.Util.MediatR`;
  2. change the `using` — `DomainEventNotification` is now `It.D4np.Util.MediatR`, `ValidationPipelineBehavior`
     is now `It.D4np.Util.MediatR.FluentValidation` (both were `It.D4np.Util.Application`);
  3. restore the DI wiring — after `AddD4npUtil(...)`, also call `AddD4npUtilMediatRValidation(...)` (or the
     umbrella `AddD4npUtilMediatR(...)`) so the validation behavior and validator scan run again. **Silent
     behavior change if missed:** `AddD4npUtil` alone no longer registers the behavior or scans validators, so
     FluentValidation validators stop running in the MediatR pipeline. A **MAJOR** change; accrues toward the
     maintainer-assembled 2.0, alongside ADR-0020/0027/0028/0030.
- **The core imposes neither dependency.** A DDD/Result-only consumer of `It.D4np.Util` now pulls only EF Core
  and the Microsoft.Extensions abstractions. Guarded on two levels: a test asserts the three core assemblies
  (Domain, Application, Infrastructure) emit **no** `MediatR`/`FluentValidation` assembly reference
  (non-vacuous — pre-split they did), and the CI `package` job asserts the packed core nuspec declares neither
  dependency while the opt-in nuspec declares `It.D4np.Util` + `MediatR` + `FluentValidation` and ships only its
  own assembly (the core layers arrive via the `It.D4np.Util` dependency, never re-bundled).
- **A second packable project.** Both packages inherit the single `<Version>` from `Directory.Build.props`, so
  they stay in lockstep automatically (ADR-0030); `package_version_file` remains unset. CI/release pack both,
  version-match every produced `.nupkg` against the tag, and emit an SBOM per package. `consistency_lint`'s
  `bundled-deps` check needs no new entry: the opt-in package bundles nothing.
- **Application is dependency-free of third parties.** With both types gone, `It.D4np.Util.Application`
  references only Domain.

## References

- Issue #38; ADR-0004, ADR-0008, ADR-0010, ADR-0026, ADR-0032; #49, #90.
- `src/main/csharp/it/d4np/util/MediatR/` (the new project: `DomainEventNotification.cs`,
  `FluentValidation/ValidationPipelineBehavior.cs`, `ServiceCollectionExtensions.cs`,
  `FluentValidation/ServiceCollectionExtensions.cs`); the core
  `Infrastructure/ServiceCollectionExtensions.cs` (validation wiring removed); `tools/consistency_lint.py`
  (`bundled-deps` note); `.github/workflows/ci.yml` (`package` job) and `release.yml` (pack both + SBOM).
