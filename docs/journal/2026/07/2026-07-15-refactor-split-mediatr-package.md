# 2026-07-15 — Refactor: split the MediatR/FluentValidation blocks into the opt-in It.D4np.Util.MediatR package (#38) — breaking (2.0)

Makes the MediatR + FluentValidation coupling opt-in. Carries ADR-0037, a **BREAKING** CHANGELOG entry,
a frozen-spec amendment (§2–§5), a new packable project, and README + i18n updates.

## Done

- **Problem:** the single `It.D4np.Util` package put `DomainEventNotification<TEvent>` and
  `ValidationPipelineBehavior<TRequest, TResponse>` in `It.D4np.Util.Application`, so Application referenced
  MediatR (12.5.0) + FluentValidation (11.11.0) and Infrastructure mirrored both (for DI + the bundling
  invariant). Every consumer — even a DDD/Result-only one — pulled both transitively (#38).
- **Decision (maintainer via AskUserQuestion):** extract both types into a new package/assembly
  `It.D4np.Util.MediatR`; `DomainEventNotification` under namespace `It.D4np.Util.MediatR`,
  `ValidationPipelineBehavior` under `It.D4np.Util.MediatR.FluentValidation`; granular DI
  (`AddD4npUtilMediatRValidation` + convenience umbrella `AddD4npUtilMediatR`); **not** `Cqrs`/`Messaging`;
  add tests that the core stays free of both transitive deps. Recorded in ADR-0037.
- **Change (source):** new packable project `It.D4np.Util.MediatR` — depends on the core packable project
  (Infrastructure → the `It.D4np.Util` package dep) and Domain `PrivateAssets=all` (compile-only, for
  `IDomainEvent`); its own `MediatR` + `FluentValidation` + DI.Abstractions package refs. Moved both types +
  the `DomainEventNotification.Create` factory into it; the new namespaces shadow the packages, so sources
  and consuming tests use `global::MediatR` / `global::FluentValidation`. Stripped MediatR/FluentValidation
  from Application (now Domain-only) and Infrastructure; the core `AddD4npUtil` no longer registers the
  validation behavior or scans validators (remarks now point to the opt-in package). The two new reflective
  entry points carry the #49 trim/AOT annotations + the #90 `NoInlining` default-scan guard.
- **Change (DI):** `AddD4npUtilMediatRValidation(...)` registers the open-generic `IPipelineBehavior` →
  `ValidationPipelineBehavior` (idempotent via `TryAddEnumerable`) and scans `IValidator<>` (skipping open
  generics); `AddD4npUtilMediatR(...)` resolves the caller's assembly and delegates to it (the adapter is a
  type/factory, nothing to register).
- **Tests:** reworked `ServiceCollectionExtensionsTests` (dropped the validation assertions/samples; added a
  theory asserting the three core assemblies emit **no** `MediatR`/`FluentValidation` reference — non-vacuous,
  pre-split they did); new `MediatRServiceCollectionExtensionsTests` (behavior registration, #32 idempotency,
  validator scan, open-generic skip, default-scan, umbrella delegation, null guards, and the #49/#90 metadata
  guards). `DomainEventNotificationTests` / `ValidationPipelineBehaviorTests` moved to the new namespaces +
  `global::`.
- **Change (infra/docs):** CI `package` job packs **both** and asserts the split at the package level (core
  nuspec declares neither MediatR nor FluentValidation and bundles all three layers; the opt-in nuspec ships
  only its own assembly and depends on `It.D4np.Util` + MediatR + FluentValidation). `release.yml` packs both,
  version-matches every `.nupkg` to the tag, and emits an SBOM per package. `consistency_lint` docstring/CONFIG
  note the second packable (it bundles nothing → no `bundled-deps` entry). Frozen spec §2–§5 amended (layer
  list + dependency graph + moved types + the new package surface). Patterns rows 3 (Adapter) and 11 (Chain of
  Responsibility) re-pointed to the new code locations + ADR-0037. README + zh/jp Install/Quick-start updated;
  i18n digest re-synced.
- **Verified:** `dotnet build -warnaserror` clean (0 warnings); `dotnet format --verify-no-changes` clean;
  full suite **276 pass / 3 skipped** (Redis, no Docker). `dotnet pack` of both inspected locally — core deps =
  EF Core + Microsoft.Extensions.* only (no MediatR/FV); MediatR package deps = It.D4np.Util + MediatR +
  FluentValidation + DI.Abstractions, lib = its own DLL only.

## State

- v1.0.0 released. **BREAKING (2.0)** — accrues toward the maintainer-assembled 2.0 alongside
  ADR-0020/0027/0028/0030; no version bump. Migration: add the `It.D4np.Util.MediatR` package, change the two
  `using`s, and call `AddD4npUtilMediatRValidation(...)` / `AddD4npUtilMediatR(...)` after `AddD4npUtil(...)`.
  ADR-0037; CHANGELOG `Changed` (BREAKING); spec §2–§5; patterns 3 + 11; README + zh/jp + digest. PR drafted on
  `refactor/split-mediatr-package`, awaiting merge. CI billing-blocked → verified locally. No local Python →
  `consistency_lint` hand-traced: ADR index gains 0037, patterns rows cite existing new paths, spec-map
  unaffected (references roadmap items), i18n digest updated, no version/lockfile congruence break.

## Next

- Remaining backlog: the Docs/Low batches — #78 + #79, #87 + #89 + #93, #50 — then the tracker is down to the
  accrued 2.0 breaks the maintainer assembles into the 2.0 release.
