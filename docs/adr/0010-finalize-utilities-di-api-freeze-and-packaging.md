# ADR-0010: Adopt DI auto-registration and finalize the 1.0 API, coverage, and packaging

- **Status:** Accepted
- **Superseded in part by:** [ADR-0019](0019-remove-specification-di-auto-registration.md) — the `ISpecification<>` auto-registration decision below is withdrawn; and [ADR-0020](0020-remove-implicit-error-to-result-conversion.md) — the frozen surface is amended to drop the implicit `Error` → `Result<TValue>` conversion (breaking). The rest of this ADR stands.
- **Date:** 2026-07-01
- **Deciders:** Maintainer
- **Related:** ADR-0003, ADR-0007, ADR-0008, ADR-0009, spec §2 (requirements 23–25), §3, §5

## Context

Milestone 8 finishes the utilities (`IDateTimeProvider` / `DateTimeProvider`, `PaginatedList<T>`),
adds one-call dependency-injection wiring (`ServiceCollectionExtensions.AddD4npUtil`), and hardens the
library for a 1.0 release: freeze the public surface, finalize the coverage bar, and configure NuGet
packaging (spec §2 requirements 23–25, §3, §5). The version bump to 1.0.0 and the release itself are a
**separate release PR** (per [`docs/workflow/release.md`](../workflow/release.md)); this milestone's
feature/hardening work lands first, with the version held at `0.0.0`.

## Decision

- **Utilities.** `IDateTimeProvider` (Application) abstracts the system clock; `DateTimeProvider`
  (Infrastructure) is the single sanctioned use of `DateTime.UtcNow`. `PaginatedList<T>` (Application) is
  an immutable page of results with derived `TotalPages` / `HasPrevious` / `HasNext` (1-based paging).
- **DI auto-registration.** `ServiceCollectionExtensions.AddD4npUtil(this IServiceCollection, params Assembly[])`
  registers the library's services (`IDateTimeProvider`, `IPasswordHasher`, `ICacheService`, and one
  `EventBroker` exposed as both `IEventBroker` and an `IHostedService`) with `TryAdd` so consumers can
  override, wires `ValidationPipelineBehavior` as an open-generic MediatR `IPipelineBehavior`, and
  reflection-scans the given assemblies to register every `IEventHandler<>`, `IValidator<>`, and
  `ISpecification<>` implementation. This adopts the **Dependency Injection** pattern (a single
  composition entry point). The consumer supplies `IMemoryCache`, `IDistributedCache`, MediatR, and
  logging.
- **API freeze.** The public surface enumerated in spec §5 is frozen at 1.0 and governed by SemVer:
  additions are MINOR, breaking changes MAJOR. Stability is enforced by review discipline plus the
  consistency lint and this ADR.
- **Coverage bar.** New and changed code must hold **≥ 80% line coverage** (coverlet). Measured at this
  milestone: **96.5% overall** (Domain 98.4%, Application 97.4%, Infrastructure 95.6%), with the Redis
  integration tests skipped locally (they raise it further on the Linux CI cell).
- **Single-package distribution.** Ship **one** NuGet package, `It.D4np.Util`, bundling the Domain,
  Application, and Infrastructure assemblies (and their XML docs) in `lib/net8.0`. The Infrastructure
  project is the only packable one; it references Domain and Application with `PrivateAssets="all"` and a
  `BuildOutputInPackage` target bundles their DLLs. Every third-party dependency of all three layers is
  declared directly on the Infrastructure project so the package's dependency list is complete. The
  package carries the README, MIT license expression, repository metadata, and a symbol package (snupkg).

## Alternatives Considered

- **Three packages (Domain / Application / Infrastructure).** Rejected by the maintainer in favour of a
  single package for simpler consumption. Trade-off: every consumer transitively acquires EF Core,
  MediatR, FluentValidation, and the caching/DI/hosting/logging abstractions even when only the
  dependency-free domain primitives are used.
- **`Microsoft.CodeAnalysis.PublicApiAnalyzers` to enforce the freeze.** Considered; deferred. SemVer,
  code review, and this ADR govern stability today; the analyzer (with `PublicAPI.Shipped.txt`) can be
  adopted later without a public-API change.
- **`System.TimeProvider` (.NET 8) instead of `IDateTimeProvider`.** Rejected — the spec fixes the
  `IDateTimeProvider` surface; a `TimeProvider`-backed implementation remains possible behind it.
- **Version derivation from the git tag (MinVer / Nerdbank.GitVersioning).** Rejected — the project keeps
  a manual `Version.cs` constant that the consistency lint holds in lockstep with the README badge and
  release files; the package `<Version>` is bumped alongside them in the release PR.
- **A third-party scanner (Scrutor) for registration.** Rejected — a small reflection loop keeps the
  dependency surface minimal (spec §2.24 calls for reflection-based scanning).

## Consequences

- Consumers add one dependency (`It.D4np.Util`) and get all three layers; the full dependency graph
  comes with it.
- The 1.0.0 version bump, changelog roll, and release notes are a **separate release PR**; this PR keeps
  `0.0.0`. The package `<Version>` must be bumped in lockstep with `Version.cs` and the README badge.
- Packaging is verified with `dotnet pack` (bundled DLLs + XML docs + README + a complete, correct
  dependency list, with no dangling `It.D4np.Util.Domain` / `.Application` package dependencies).
- `AddD4npUtil` is designed to be called once; the hosted-service registration is not idempotent across
  repeated calls (documented).
- Adopts the **Dependency Injection** (Creational) pattern; catalogued.

## References

- spec §2 (requirements 23–25), §3, §5; ADR-0003; ADR-0007; ADR-0008; ADR-0009;
  [`docs/workflow/release.md`](../workflow/release.md); [`docs/workflow/packaging.md`](../workflow/packaging.md).
- `docs/patterns/design-patterns.md` — Creational: *Dependency Injection*.
- OWASP / coverlet coverage measured at 96.5% line.
