# Changelog

All notable changes to `egl-util-csharp` are documented here, following
[Keep a Changelog 1.1.0](https://keepachangelog.com/en/1.1.0/) and
[Semantic Versioning 2.0.0](https://semver.org/).

Every PR that introduces a user-visible change adds a line to `[Unreleased]` in the same
PR. A release PR moves the `[Unreleased]` entries into a new per-version file under
`docs/changelog/v<MAJOR>/v<X.Y.Z>.md` and adds an index row below.

## [Unreleased]

### Added

- `EventBroker` failures are now observable without a logger via an `EventSource` (#43). A handler that
  threw was isolated and logged through `_logger?.LogError(...)`, so a broker constructed with **no** logger
  (manual construction, or a host with no logging provider) dropped the failure with zero trace — no
  rethrow, metric, or callback. Every isolated failure (a throwing handler, and the last-resort dispatch
  guard) is now reported **unconditionally** through an `EventSource` named `It.D4np.Util.EventBroker`
  (`HandlerFailed` / `DispatchFailed` events), so it surfaces via ETW / `dotnet-trace` / an `EventListener`
  regardless of logging configuration; when a logger *is* supplied, it is still logged as before. Additive;
  no public-API change (the source name is the observable contract to subscribe to).
- `IPasswordHasher` gains rehash detection and span-based plaintext overloads (#35, #41). `NeedsRehash(string)`
  reports when a stored hash is under-strength (older scheme or lower work factor than the current
  configuration) so callers can implement the documented verify-then-conditionally-rehash upgrade path;
  `Hash(ReadOnlySpan<char>)` and `Verify(ReadOnlySpan<char>, string)` let a caller holding the plaintext in
  a clearable buffer derive without materializing a `string`. All three are **default interface members**
  (existing implementers and callers are unaffected); `PasswordHasher` overrides them (the span overloads
  are the primitives, `NeedsRehash` compares against the configured work factor). Additive (ADR-0029).
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

- The release workflow now gates the draft on the full CI suite and publishes the curated notes (#37, #73).
  A tag push does not trigger `ci.yml`, and `release.yml` previously packed + version-matched but ran **no**
  tests/coverage/format/analyzers/lint on the released commit — so a tag on a commit with failing tests still
  drafted a green release (#37). It now `needs:` a `verify` job that reuses `ci.yml` via `workflow_call`
  (build + test matrix, format/analyzers/coverage, consistency lint on the tagged commit) before packing.
  And the draft used GitHub's auto-generated notes, ignoring the mandated hand-written
  `docs/releases/<tag>.md` (#73); it now fails if that file is missing and passes it as the release body
  (`body_path`), with the auto-generated commit list appended. Repo/CI change only; the shipped package is
  unchanged.
- **BREAKING:** `LibraryVersion.Version` is now a runtime-resolved property instead of a `public const`, and
  the version is single-sourced in MSBuild (#48, #86, ADR-0030). It previously duplicated the `1.0.0` literal
  in the packable project's `<Version>` and in `Version.cs` (coupled only by the lint), and as a `const` it
  was **inlined** into every consumer at their compile time — so an in-place library upgrade left the
  consumer reporting the old version. The single `<Version>` now lives in `Directory.Build.props` (stamped
  onto every assembly and inherited as the NuGet package version), and `LibraryVersion.Version` is a
  `public static string { get; }` resolved at runtime from the loaded assembly's informational version. A
  **MAJOR** change (2.0): a consumer compiled against the 1.x `const` must be recompiled to pick up the
  property, and `Version` can no longer be used in a `const` context (attribute argument, `case` label);
  ordinary reads are source-compatible. The Domain layer stays dependency-free.
- The `consistency_lint.py` congruence checks are tightened and the consistency CI job no longer deep-clones
  (#72, #74, #82, #96). `check_milestones` now asserts README ↔ ROADMAP completion in **both** directions
  (previously only README-complete-but-ROADMAP-open failed) and flags a README milestone with no ROADMAP
  section (#72). `check_i18n_freshness` now verifies the **translation file exists** and fails on an
  unrecognised status cell, instead of silently skipping any row without a literal `` `translated` `` (#74).
  `check_patterns` requires an existing code location only for `Implemented` rows — a `Planned` row (decided
  in an ADR, not yet landed) may omit it, matching the catalogue's own status vocabulary — while still
  rejecting a cited path that does not exist (#82). The dead `git()` helper is removed and the consistency
  job drops `fetch-depth: 0` (no check reads git history; the i18n check is content-digest based) (#96).
  Repo/CI change only; the shipped package is unchanged.
- The reflective public entry points now carry trim/AOT annotations (#49). `ServiceCollectionExtensions.AddD4npUtil`
  (assembly scanning via `Assembly.GetTypes`) and the `EventBroker` constructor (runtime-type dispatch via
  `MakeGenericType`/`MakeGenericMethod`) are annotated `[RequiresUnreferencedCode]` and `[RequiresDynamicCode]`,
  so a consumer building a trimmed or Native-AOT app now gets a warning at the call site instead of a silent
  runtime failure. No runtime-behavior change (metadata only).
- **BREAKING:** `Entity<TId>` and `AggregateRoot<TId>` become self-referencing generics —
  `Entity<TId, TEntity>` and `AggregateRoot<TId, TAggregate>` — so equality is based on the **canonical
  domain type** rather than the runtime type (#25, #26, ADR-0028). This fixes two identity bugs: an EF Core
  proxy (`CustomerProxy : Customer`) now compares **equal** to a plain instance of the same identity (was
  unequal — #26), and a **transient** entity with a default `Id` (`0`/`Guid.Empty`) now compares by
  **reference** so distinct unsaved entities no longer collapse in a `HashSet`/`Dictionary` (was equal —
  #25). Migration: close the generic over the type itself — `class Order : AggregateRoot<Guid, Order>`,
  `class Customer : Entity<Guid, Customer>`; `IRepository<TEntity, TId>`'s constraint is now
  `AggregateRoot<TId, TEntity>`. There is no longer a common non-generic `Entity<TId>` base, so compare
  entities of different types through `object`/`IEquatable`. A **MAJOR** change (2.0); the Domain layer
  stays dependency-free (no EF reference, no proxy-name heuristic).
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

- The symbol package (`.snupkg`) is now genuinely debuggable: SourceLink + all three assemblies' PDBs
  (#36, #66). The package set `IncludeSymbols`/`SymbolPackageFormat=snupkg` but shipped **no** SourceLink
  metadata (a consumer stepping in with symbol servers got symbols with no source URL, so no source was
  fetched — #36), and the bundling target packed only the referenced projects' `.dll`/`.xml`, so the snupkg
  contained **only** `It.D4np.Util.Infrastructure.pdb` — the Domain and Application layers (Entity, Result,
  ValueObject, …) were undebuggable via the symbol server (#66). Added `Microsoft.SourceLink.GitHub` plus
  `PublishRepositoryUrl`/`EmbedUntrackedSources`/`DebugType=portable` in `Directory.Build.props` (so all
  three assemblies' PDBs carry the source-link document), and taught the pack target to include the bundled
  projects' PDBs. Verified by a pack smoke check: the snupkg now contains all three PDBs, the nupkg still
  ships only the DLLs/XML docs (no PDBs), and each PDB's SourceLink resolves to the GitHub raw URL at the
  built commit. Packaging only; the library API is unchanged.
- `SpecificationEvaluator` now rejects a specification that defines **both** `OrderBy` and
  `OrderByDescending` (#88). The primary ordering is a single ascending *or* descending key; the
  `Specification` base class enforces this (each `ApplyOrderBy*` resets the other), but a direct
  `ISpecification<TEntity>` implementation (a supported, assembly-scanned extension point) could set both.
  The evaluator applied `OrderBy` and **silently dropped** `OrderByDescending`, producing wrongly-ordered —
  and, when paged, wrongly-sliced — results with no diagnostic, even though it already threw for the
  analogous paged-but-unordered state. It now throws `InvalidOperationException` for the ambiguous both-set
  state too, and the exclusivity is documented on `ISpecification.OrderBy`/`OrderByDescending`. **Behavior
  change:** a direct implementation that set both (already producing wrong results) now fails loudly; specs
  built via the `Specification` base class cannot reach this state and are unaffected.
- `Error`'s constructor now rejects a null `Code` or `Message` (#76). The `sealed record Error(string Code,
  string Message)` backed its non-nullable annotations with NRT alone, so a caller outside NRT enforcement
  (a `#nullable disable` project, an explicit `null!`, or System.Text.Json materializing an `Error` from a
  payload with a missing field via the parameterized constructor) could build an `Error` whose "non-nullable"
  `Code`/`Message` was null — the null then surfaced far downstream (e.g. `result.Error.Code.StartsWith(…)`
  NREs). The record now guards both components on construction (`ArgumentNullException`), keeping the record
  fully positional (constructor, `Deconstruct`, `with`, equality) and `Error.None`'s empty-string sentinel
  valid. Additive hardening; no API change.
- `AddD4npUtil`'s no-argument default scan is now inlining-safe (#90). The default scan target comes from
  `Assembly.GetCallingAssembly()`; the BCL warns that if the calling method is inlined by the JIT, it returns
  the caller's caller — silently scanning the wrong assembly and registering **no** handlers/validators, with
  no error. `AddD4npUtil` now carries `[MethodImpl(MethodImplOptions.NoInlining)]` (the canonical guard) so
  the frame is never folded away. The XML docs also warn that a wrapper/extension method forwarding to
  `AddD4npUtil()` makes the **wrapper's** assembly the scan target — such wrappers should pass the intended
  assemblies explicitly. No API change (metadata + docs).
- Overriding `IEventBroker` now actually disables the default broker (#92). `AddD4npUtil` advertised the
  core singletons as "overridable — each uses `TryAdd`", but only the `IEventBroker` **mapping** was gated:
  the concrete `EventBroker` and its `IHostedService` were registered unconditionally. So a consumer who
  pre-registered their own `IEventBroker` still got the library's broker **constructed and started** as a
  hosted service — a phantom consumer loop and channel nothing publishes to — while their replacement went
  un-hosted. The whole broker cluster (concrete type, mapping, and hosted service) is now gated on
  `IEventBroker` being absent, so a pre-registered override fully replaces the default and no phantom broker
  runs. No API change; non-overriding consumers are unaffected. If you want your replacement broker hosted,
  register it as an `IHostedService` yourself (documented in the `AddD4npUtil` remarks).
- `EventBroker`'s per-event dispatch-plan cache is now per-broker instead of process-static (#44). The
  `ConcurrentDictionary<Type, EventDispatch>` was `static`, so it outlived every broker/provider and pinned
  a closed generic type + compiled delegate for each distinct **published runtime type** for the whole
  process lifetime. Under a plugin / dynamic-assembly host or with unbounded generic event types
  (`Envelope<TPayload>`), that was a slow, uncollectable leak of type/delegate metadata. The cache is now an
  instance field, collected with the broker and its provider; the per-broker recompute is one-time per event
  type and negligible. No API or behavior change (finite-event-type apps are unaffected).
- `CacheService.RemoveAsync` now removes L2 before L1 (#91). It previously cleared L1 first, then awaited
  the L2 delete, so during that round-trip a concurrent same-node `GetOrCreateAsync` could miss the
  just-cleared L1, hit the still-present L2, and **re-promote the removed value into L1** — the removing
  node then served the invalidated value from L1 until its TTL (indefinitely with `ttl = null`). Removing
  L2 first and L1 last (standard cache-aside order) shrinks that window from a full L2 round-trip to the
  gap between the two calls. It also makes an L2-delete fault leave **both** levels populated (rather than
  clearing L1 and leaving L2 stale). No API change. (Distinct from the unavoidable cross-node staleness of
  #42.)
- `PasswordHasher.Verify` now enforces the V1 scheme's fixed **16-byte salt and 32-byte key** (#40). It
  previously checked only that the decoded salt and key were non-empty, so a well-formed but off-spec hash
  (e.g. a 1-byte salt or an 8-byte key) was fed to PBKDF2 rather than rejected — the documented "V1"
  parameters were unenforced on the verify path. Such hashes now return `false`. No API change (no auth
  bypass; the attacker still needs the password).
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
