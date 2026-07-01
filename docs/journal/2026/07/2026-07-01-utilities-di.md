# 2026-07-01 — Milestone 8: utilities, DI, and 1.0 hardening (feature PR)

Milestone 8 is delivered as **two PRs** (per `docs/workflow/release.md`): this feature/hardening PR
first (version stays `0.0.0`), then a separate release PR that cuts v1.0.0.

## Done (8.1–8.3 + packaging config)

- **Utilities:** `IDateTimeProvider` (Application) + `DateTimeProvider` (Infrastructure, the single
  sanctioned `DateTime.UtcNow`); `PaginatedList<T>` (Application) — immutable page with derived
  `TotalPages`/`HasPrevious`/`HasNext`.
- **DI (8.2):** `ServiceCollectionExtensions.AddD4npUtil(this IServiceCollection, params Assembly[])` —
  `TryAdd`-registers the core services (one `EventBroker` as both `IEventBroker` and `IHostedService`),
  wires `ValidationPipelineBehavior` as an open-generic MediatR `IPipelineBehavior`, and reflection-scans
  for `IEventHandler<>`/`IValidator<>`/`ISpecification<>`. Infrastructure gains `MediatR` + `FluentValidation`
  refs. Dependency Injection pattern (ADR-0010).
- **API freeze + coverage (8.3):** ADR-0010 freezes the spec §5 surface under SemVer and finalizes the
  **≥ 80% line** bar. Measured **96.5%** overall (Domain 98.4%, Application 97.4%, Infrastructure 95.6%),
  Redis tests skipped locally.
- **Packaging config:** single `It.D4np.Util` package — Infrastructure is the only packable project and
  bundles the Domain + Application DLLs and XML docs (`PrivateAssets=all` + a `BuildOutputInPackage`
  target); all third-party deps declared directly so the package's dependency list is complete; README,
  MIT, symbols (snupkg). Verified with `dotnet pack` (3 DLLs + 3 XML docs + README + correct deps, no
  dangling `It.D4np.Util.Domain`/`.Application` deps).
- 127 tests (125 pass + 2 Redis skip), 15 new. build `-warnaserror`, `dotnet format`, and
  `dotnet restore --locked-mode` all green.

## State

- M1–M7 merged. M8 feature work on `feat/utilities-di`; PR drafted, awaiting merge. Version remains `0.0.0`.

## Next

- **Release PR (8.4):** bump `Version.cs` + README badge + package `<Version>` to `1.0.0`, roll the
  changelog into `docs/changelog/v1/v1.0.0.md`, draft `docs/releases/v1.0.0.md`, mark Milestone 8 done.
  Awaiting the maintainer's go-ahead to cut v1.0.0 (the outward-facing release step).
