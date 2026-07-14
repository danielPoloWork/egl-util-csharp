# Changelog

All notable changes to `egl-util-csharp` are documented here, following
[Keep a Changelog 1.1.0](https://keepachangelog.com/en/1.1.0/) and
[Semantic Versioning 2.0.0](https://semver.org/).

Every PR that introduces a user-visible change adds a line to `[Unreleased]` in the same
PR. A release PR moves the `[Unreleased]` entries into a new per-version file under
`docs/changelog/v<MAJOR>/v<X.Y.Z>.md` and adds an index row below.

## [Unreleased]

### Added

- A `DomainEventNotification` factory so the MediatR bridge works for runtime-typed events (#61). The
  advertised Adapter shipped with no way to close `DomainEventNotification<TEvent>` over an event's runtime
  type, so the natural dispatch-callback code (`new DomainEventNotification<IDomainEvent>(e)`) published the
  wrong closed type and reached **zero** concrete-typed handlers. `DomainEventNotification.Create<TEvent>(e)`
  (reflection-free, trim/AOT-safe) and `DomainEventNotification.Create(IDomainEvent e)` (closes the
  notification over the runtime type via a cached constructor delegate) now build a notification that
  MediatR routes to `INotificationHandler<DomainEventNotification<ConcreteEvent>>`. The reflective overload
  is annotated `[RequiresDynamicCode]`/`[RequiresUnreferencedCode]`; Native AOT consumers use the generic
  overload or an explicit factory (ADR-0026). Additive; no existing behavior changes.
- Secondary ordering for the Specification pattern (#31). `Specification<TEntity>` gains protected
  `ApplyThenBy` / `ApplyThenByDescending` helpers and `ISpecification<TEntity>` a `ThenBys` member, so a
  specification can define a primary sort key plus `ThenBy`/`ThenByDescending` tie-breakers — giving paging
  over a non-unique key a deterministic order across pages (EF Core cannot express a multi-column sort via
  a single composite key). `ThenBys` has a default interface implementation (empty), so the addition is
  **backward-compatible**: existing `ISpecification` implementors are unaffected (ADR-0025). Applied by
  `SpecificationEvaluator` after the primary ordering.

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

- CI now fails, rather than silently skips, the Testcontainers Redis integration tests on the
  Docker-capable leg (#100). The tests caught every container-start failure and downgraded to a skip, so a
  regression (a bad image tag, a Testcontainers break) could silently stop the only real-Redis coverage
  while the pipeline stayed green. They now honor a `REQUIRE_DOCKER` flag — set on the ubuntu CI leg — that
  turns "the container did not start" into a test failure; environments without Docker (the windows/macOS
  legs, local dev) still skip. Repo/CI change only; the shipped package is unchanged.
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

- **BREAKING:** the implicit `Error` → `Result<TValue>` conversion is removed (#28). It collided with the
  implicit `TValue` → `Result<TValue>` conversion: `Result<Error>` could not be built from an `Error`
  (CS0457, ambiguous), and `Result<object> r = someError;` silently bound the `Error` overload and produced
  a *failure* — inverting an intended success that carries the error as its value. Create failures with the
  explicit `Result.Failure<TValue>(error)` factory instead (`return someError;` becomes
  `return Result.Failure<TValue>(error);`). The `TValue` → `Result<TValue>` success conversion is
  unchanged, so `Result<object> r = someError;` is now a success carrying the error (ADR-0020).
- `AddD4npUtil` no longer auto-registers `ISpecification<>` implementations from the scanned assemblies
  (#67). A specification is a query object built with its runtime arguments (e.g.
  `OrdersByCustomerSpec(Guid customerId)`), so the scan's `Transient(ISpecification<TEntity>, spec)`
  descriptor had constructor parameters the container could not supply; with `ValidateOnBuild` enabled
  (the default in a Development host) the container then failed to build and the application would not
  start — triggered merely by defining a parameterized specification in a scanned assembly. Event
  handlers and validators are still auto-registered. Construct specifications where you use them
  (`new OrdersByCustomerSpec(id)`) — nothing in the library resolved them from DI. Treated as a
  correctness fix (ADR-0019, superseding that part of ADR-0010).

### Fixed

- `EventBroker` now honors `IEventHandler`'s contravariance (#71). `IEventHandler<in TEvent>` is declared
  contravariant, but the broker resolved handlers by the event's **exact** runtime type, and MS.DI applies
  no generic variance to `IEnumerable<T>`, so a handler registered for a base type or a catch-all
  `IEventHandler<IDomainEvent>` silently never fired. Dispatch now also resolves and invokes handlers
  registered for the event's base types and implemented interfaces (most-specific first), so the `in`
  modifier delivers what it advertises (ADR-0027). **Behavior change:** base-typed/interface handlers that
  silently never fired now fire — it surfaces only latent, already-broken registrations; weigh as at least
  a MINOR. No public-API change; exact-type dispatch is unchanged.
- `CacheService` now has a defined **L2-outage contract** and no longer discards a computed value on a
  distributed-cache write blip (#62). Previously any `IDistributedCache` fault failed the whole call —
  worst of all, a fault on the *post-factory* write threw away the value the factory had just produced.
  Now a post-factory L2 **write** fault is swallowed and the value is returned (still promoted to L1), so
  a transient outage never fails the business operation; cancellation (`OperationCanceledException`) is
  still honoured, and serialization errors still surface (the value is serialised before the guarded
  write). L2 **read** faults and `RemoveAsync` faults still propagate, now documented (ADR-0024).
  **Behavior change:** a caller that previously saw an exception on an L2 write fault now gets the value.
  No public-API change.
- The cache now **fails fast** on a value that cannot round-trip instead of silently storing a lossy one
  (#33). `CacheService`/`DistributedCacheExtensions` serialise the **declared** type `T` (no `$type`
  discriminator), so `GetOrCreateAsync<IShape>()` storing a `Circle`, `GetOrCreateAsync<object>()`, or a
  runtime subtype (a `Dog` under `<Animal>`) wrote only `T`'s members and read back lossy with no error.
  A non-round-trippable `T` (`object`, an interface, or an abstract class) and a value whose runtime type
  differs from `T` now throw `NotSupportedException` (ADR-0023). No public-API change; a concrete,
  matching `T` serialises to the same bytes as before. **Behavior change:** code that previously
  "succeeded" with silent loss now throws — it surfaces only latent misuse, but weigh it as at least a
  MINOR at release.
- `Result.Failure(params Error[])` and `Result.Combine(params Result[])` now reject a **null element**
  with an `ArgumentException` (#53). Both guarded the array but not its elements: a null `Error` leaked
  through the non-nullable `Result.Error` (deferring a `NullReferenceException` to the consumer), and
  `Combine` threw an opaque `NullReferenceException` on a null `Result`. The element is now validated
  up front, matching the type's existing fail-fast posture (it already rejects `Error.None` and null
  success values). No public-API change.
- `ValidationPipelineBehavior` no longer shares one `ValidationContext` across concurrently-running
  validators (#52). It validated via `Task.WhenAll` over a single `ValidationContext<TRequest>`, but a
  `ValidationContext` is not safe for concurrent use, so validators doing genuinely-async work overlapped
  and corrupted each other's failures — the aggregated failures were nondeterministically lost or
  duplicated (synchronous validators, which never overlap, were unaffected). Each validator now runs
  against its own `ValidationContext<TRequest>`; execution stays parallel and every failure aggregates
  correctly. No public-API change.
- Domain-event dispatch now drains each aggregate to a fixed point (#30). Both `DispatchDomainEventsAsync`
  overloads published an aggregate's buffered events and then cleared its *entire* buffer, so an event a
  handler raised on that aggregate during dispatch (a synchronous publish) was wiped unpublished — silent
  domain-event loss the at-least-once contract did not cover. Each aggregate is now drained (re-publishing
  events that appear while handlers run) until none remain, then cleared; the per-aggregate
  clear-on-success retry semantics are unchanged (ADR-0022, amending ADR-0012). A handler that raises
  events on the same aggregate without bound will not terminate.
- The cache serializer no longer corrupts a cached value's null properties (#58). `CacheSerializer` set
  `DefaultIgnoreCondition = WhenWritingNull`, omitting null properties from the stored JSON; on read,
  `System.Text.Json` left an absent property at its C# default, so a type with a non-null property default
  (e.g. `string? Note { get; set; } = "n/a";`) cached with that property `null` came back with the default
  after L1 eviction — silent, exception-free corruption where identical `GetOrCreateAsync` calls could
  observe different data. Null properties are now written explicitly and round-trip as `null` (ADR-0021,
  amending ADR-0009); payloads stay compact via the Web defaults and no indentation.
- `Result.Errors` is now a truly read-only view (#85). It was backed by the `Error[]` returned from the
  factories, so a consumer could downcast `IReadOnlyList<Error>` to `Error[]` — or a subclass could pass a
  mutable list to the protected constructor and retain a reference — and mutate a supposedly immutable
  `Result` after construction. The constructor now stores a `ReadOnlyCollection<Error>` over a defensive
  copy (successes share a single empty instance). The declared `IReadOnlyList<Error>` surface is unchanged.
- `Result.Success<TValue>` now rejects a `null` value (#27). `Result.Success<TValue>(null)` and the
  implicit `TValue` → `Result<TValue>` conversion previously produced `IsSuccess == true` with a `null`
  `Value`, deferring a `NullReferenceException` far downstream even though `Value` is typed non-nullable
  and the type's premise is "no nulls". `Success<TValue>` (and thus the implicit operator) now throws
  `ArgumentNullException` for a `null` reference or an empty `Nullable<T>`, using an allocation-free null
  check that never boxes value-type successes. No public API change.
- `Result.Failure` now rejects `Error.None` (#29). `Result.Failure(Error.None)` previously produced a
  failure whose `Error` equalled `Error.None` — the sentinel documented as carried by *successful*
  results — so a caller using the documented `result.Error == Error.None` success check misclassified a
  genuine failure as success and silently swallowed it. Both `Failure(params Error[])` and its generic
  overload now throw `ArgumentException` when any supplied error is `Error.None`, so a failure always
  carries a distinguishable error. No public API change.
- `AddD4npUtil`'s assembly scan now skips open-generic type definitions (#65). An open-generic handler,
  validator, or specification in a scanned assembly (e.g. `AuditHandler<TEvent> : IEventHandler<TEvent>`)
  was registered against its open constructed interface, so `BuildServiceProvider` threw an
  `ArgumentException` ("Cannot instantiate implementation type …") — the entire container failed to build
  and the application could not start, regardless of `ValidateOnBuild`. The scan now excludes any type
  with unbound generic parameters (matching MediatR's and FluentValidation's own scanners); only closed,
  constructible types are auto-registered. No public API change.
- `EventBroker` now disposes each per-event DI scope asynchronously (#60). It used a synchronous
  `using`/`Dispose`, which throws for a resolved service that implements `IAsyncDisposable` but not
  `IDisposable` — leaking it and logging the already-succeeded dispatch as *failed* on every event. It
  now uses `await using`/`CreateAsyncScope`, which still disposes plain `IDisposable` services
  synchronously (strictly safer).
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
