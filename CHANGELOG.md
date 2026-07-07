# Changelog

All notable changes to `egl-util-csharp` are documented here, following
[Keep a Changelog 1.1.0](https://keepachangelog.com/en/1.1.0/) and
[Semantic Versioning 2.0.0](https://semver.org/).

Every PR that introduces a user-visible change adds a line to `[Unreleased]` in the same
PR. A release PR moves the `[Unreleased]` entries into a new per-version file under
`docs/changelog/v<MAJOR>/v<X.Y.Z>.md` and adds an index row below.

## [Unreleased]

### Added

- Documentation i18n & README polish (Milestone 9): the README translated into Simplified Chinese
  (`docs/i18n/zh/README.md`) and Japanese (`docs/i18n/jp/README.md`), with a `docs/i18n/` index and a content-digest
  freshness manifest (English remains the source of truth; ADR-0011); plus an enterprise README refresh
  — a NuGet version/downloads/CI/.NET/License badge row, a quick-start, and a translations switcher. The
  `i18n-freshness` lint is now content-digest based (squash-merge-safe). Docs only — the shipped package
  is unchanged.
- Capture-before-save domain-event dispatch: `DbContextExtensions.CaptureDomainEvents(this DbContext)`
  returns a `DomainEventSnapshot` taken before `SaveChangesAsync`, and
  `DispatchDomainEventsAsync(this DomainEventSnapshot, …)` publishes it after the commit — preserving
  the at-least-once, clear-on-success contract while delivering events for aggregates the save removes
  (ADR-0012). Additive to the frozen 1.0 surface.

### Changed

- The release workflow now packs and publishes the NuGet package (#22). On a `v*.*.*` tag it packs the
  single `It.D4np.Util` package (+ `.snupkg`), asserts the packed version equals the tag, and attaches
  both to the drafted GitHub Release; when a maintainer publishes that release, it pushes to nuget.org
  (`--skip-duplicate`) using the `NUGET_API_KEY` secret (ADR-0017). Previously the workflow only built
  and drafted, so `dotnet add package It.D4np.Util` resolved to nothing. Repo/CI change only; the
  shipped package contents are unchanged. Requires the `NUGET_API_KEY` secret (see
  `docs/workflow/github-setup.md`).
- CI now enforces the ≥80% line-coverage bar (#24). The `quality` job previously collected coverage
  (`--collect:"XPlat Code Coverage"`) but nothing read it; it now runs coverlet.msbuild with
  `/p:Threshold=80 /p:ThresholdType=line /p:ThresholdStat=total`, so the build fails if total line
  coverage drops below 80% (currently ~97%). Adds a test-only `coverlet.msbuild` dev dependency
  (ADR-0016). Repo/CI change only; the shipped package is unchanged.

### Deprecated

### Removed

### Fixed

- A JSON-`null` L2 entry no longer poisons the L1 cache with a null "hit" (#59). `CacheService` now
  treats a deserialized `null` from the distributed level as a miss (it runs the factory and does not
  promote `null` into L1), and `DistributedCacheExtensions.SetAsync` refuses a `null` value — together
  restoring the "a null result is never cached" contract across the interop path. A stored default value
  type (e.g. `0`) is still a real hit.
- `AddD4npUtil` now registers the `ValidationPipelineBehavior` idempotently (#32). It used a plain
  `AddTransient` (the lone non-idempotent registration), so calling `AddD4npUtil` twice — e.g. a library
  and its host both call it — appended a second descriptor and made MediatR run every FluentValidation
  validator twice per request. It now uses `TryAddEnumerable`, matching the other registrations.
- `CacheService.GetOrCreateAsync` now validates the `ttl` up-front (#34). A non-positive `ttl` (e.g. a
  deadline already in the past) previously surfaced as an uncontextualised `ArgumentOutOfRangeException`
  from the entry-options setter — *after* the factory had run and its result was discarded. It now
  throws `ArgumentOutOfRangeException` with a clear message before touching the caches or the factory,
  and the strictly-positive constraint is documented on `ICacheService.GetOrCreateAsync`.
- `EventBroker` no longer self-deadlocks when a handler re-publishes on a full channel (#19). Because
  the single consumer both drains the channel and runs handlers inline, a handler awaiting
  `PublishAsync` under back-pressure blocked forever (only the parked consumer could free a slot). An
  in-handler publish is now non-blocking: it enqueues if there is room and otherwise throws
  `InvalidOperationException` instead of waiting, so the broker (and shutdown drain) never hang.
  External publishers keep their blocking back-pressure (ADR-0018).
- Domain events raised on an aggregate that is then removed in the same unit of work are no longer
  silently dropped (#21). EF Core detaches `Deleted` entities from the change tracker once
  `SaveChanges` completes, so the post-save `DispatchDomainEventsAsync(this DbContext, …)` overload
  could not see them; the new capture-before-save flow (`CaptureDomainEvents` + `DomainEventSnapshot`)
  delivers them. The change-tracker overload's documentation now states the limitation and points to
  the safe flow.
- `CacheService` no longer throws against a size-limited `IMemoryCache` (#20). A cache registered
  with `AddMemoryCache(o => o.SizeLimit = N)` requires every entry to specify a size, so the first
  read-through/promotion write previously threw `InvalidOperationException`, breaking `GetOrCreateAsync`
  for every uncached key. Each L1 entry is now written with an explicit size of one unit; for a
  size-limited cache the `SizeLimit` therefore bounds the number of L1 entries (ADR-0013). No public
  API change.

### Security

- CI now restores in locked mode (#23). `RestoreLockedMode` is enabled in `Directory.Build.props`
  when `GITHUB_ACTIONS=true`, so a drifted or tampered `packages.lock.json` fails the build across the
  build, quality, and release jobs instead of being silently rewritten — making the committed
  lockfiles a real supply-chain gate (ADR-0014). Repo/CI change only; the shipped package is unchanged.
- CI now enforces lockfile *presence* (follow-up to #23). The `consistency` job fails if any tracked
  `.csproj` sits in a directory without a committed `packages.lock.json` — closing the gap where locked
  mode catches drift but silently generates a missing lockfile, leaving a new project's dependencies
  ungated (ADR-0015, closing ADR-0014's known limitation). Repo/CI change only.

---

## Released versions

| Version | Date | Notes |
|---------|------|-------|
| [v1.0.0](docs/changelog/v1/v1.0.0.md) | 2026-07-01 | First stable release; the full Milestone 1–8 surface, frozen under SemVer. |
