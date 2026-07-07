# 2026-07-07 — Fix: assembly scan no longer auto-registers specifications (#67)

Sixth fix from the Medium/Low audit tier, and the natural follow-up to #65 (same file). Unlike the
prior fixes this one **amends frozen 1.0 surface** — the maintainer chose full removal (over
parameterless-only) via an AskUserQuestion decision.

## Done

- **Bug:** `AddD4npUtil` scanned for and registered every concrete `ISpecification<>` implementation as
  `Transient(ISpecification<TEntity>, spec)`. But a specification is a query object built with runtime
  arguments (`Specification<TEntity>` sets `Criteria` via the constructor), so a normal consumer spec —
  `OrdersByCustomerSpec(Guid customerId)` — got a registration whose constructor parameter the container
  cannot supply. `Host.CreateDefaultBuilder` / `WebApplicationBuilder` set `ValidateOnBuild = true` in
  Development, so building the container threw and **the host failed to start**, triggered merely by
  defining a parameterized spec in a scanned assembly (#67). Even parameterless specs gained a
  meaningless registration (last-write-wins per `TEntity`); nothing in the library resolves
  `ISpecification<>` from DI.
- **Fix:** dropped `RegisterImplementations(services, typeof(ISpecification<>), toScan)` from
  `AddD4npUtil`. Handlers and validators are still auto-registered. Specifications are `new`-ed at the
  call site, as the pattern intends. Rewrote the `AddD4npUtil` XML `<remarks>` (specs are explicitly not
  registered, with the why).
- **Decision (maintainer, this session):** full removal — chosen over "parameterless-only" and "defer".
  Treated as a **correctness fix**, not a feature removal (the registration was unusable for the normal
  parameterized shape and had no sound use otherwise). Version bump deferred to release time per policy.
- **ADR-0019** records the decision and **supersedes the `ISpecification<>` auto-registration part of
  ADR-0010** (the rest of ADR-0010 stands; annotated it with a "Superseded in part by" forward-note).
  Indexed in `docs/adr/README.md`.
- **Frozen spec §2** (`docs/specs/01_spec_util.md`) amended: the `ServiceCollectionExtensions` line now
  names only handlers and validators, with a pointer to ADR-0019.
- **Test:** `Auto_registers_scanned_handlers_validators_and_specifications` →
  `Auto_registers_scanned_handlers_and_validators` (spec assertion dropped); new
  `Specifications_are_not_auto_registered` adds a `ParameterizedSpecification(Guid)` to the scanned test
  assembly and asserts no `ISpecification<>` descriptor is registered and none resolves. Verified red
  pre-fix / green post-fix. (Note: a `ValidateOnBuild=true` assertion over the whole test assembly is
  not viable — `EventBrokerTests` handler fixtures take a `TaskCompletionSource` ctor arg and fail
  validation independently; the descriptor-absence assertion targets #67's root cause cleanly.)
- **Docs:** CHANGELOG `[Unreleased]` → Removed; README quick-start drops "specifications"; the zh/jp
  translations re-synced (one list item removed) and both i18n digests refreshed to
  `25bf0a86c7e84e71` so `i18n-freshness` stays green. `dotnet build -warnaserror` clean; `dotnet format`
  clean; 148 pass / 2 Redis skip.

## State

- v1.0.0 released. Removes documented (but broken/unusable) 1.0 behavior → maintainer decides the bump
  at release; recorded as a bug fix. PR drafted on `fix/scan-drops-specifications`, awaiting merge.
- CI still billing-blocked (jobs don't start; verified locally). No local Python, so `consistency_lint`
  hand-verified — note the touched invariants this time: **adr-index** (0019 added + indexed, sequential),
  **i18n-freshness** (README changed → both digests updated + translations re-synced). Others unaffected.

## Next

- Continue Medium/Low safest-first: the core `Result` invariants #29 (Failure rejects `Error.None`) /
  #27 (`Success(null)`) / #28 (implicit-operator collision) — property-tested, so higher blast radius;
  go carefully. No-code-change tiers ([test]/[docs]/[ci]) also remain.
