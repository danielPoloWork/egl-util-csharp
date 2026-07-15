# Session Journal

Dated end-of-session checkpoints — what got done, where the project stands, and how the
next session resumes. One file per session that changed the project's state, at
`docs/journal/<YYYY>/<MM>/<YYYY-MM-DD>-<short-slug>.md`. The journal is the dated trail;
`docs/ROADMAP.md` is the forward plan — checkpoints never live inline in the roadmap.

At the close of a state-changing session, the agent:

1. Creates the dated file under `docs/journal/<YYYY>/<MM>/`.
2. Adds a link row to this index (newest first, grouped by year/month).
3. Updates the *Latest checkpoint* pointer in `docs/ROADMAP.md`.

## Index

### 2026

_(newest first)_

- [2026-07-15 — Refactor: seal Result<TValue> (#95)](2026/07/2026-07-15-refactor-seal-result-of-t.md)
- [2026-07-15 — Feat: make PaginatedList reachable (criteria-only + ToPaginatedListAsync) (#70)](2026/07/2026-07-15-feat-reachable-pagination.md)
- [2026-07-15 — Feat: Result functional combinators + non-throwing access (#69)](2026/07/2026-07-15-feat-result-functional-combinators.md)
- [2026-07-14 — Feat: Specification And/Or/Not combinators + IsSatisfiedBy (#68)](2026/07/2026-07-14-feat-specification-predicate-combinators.md)
- [2026-07-14 — Build: make the Roslyn analyzer gate real (#77)](2026/07/2026-07-14-build-enforce-analyzer-gate.md)
- [2026-07-14 — CI: enforce the bundling invariant + pack on every PR (#63, #64)](2026/07/2026-07-14-ci-bundling-invariant-and-pack-gate.md)
- [2026-07-14 — CI: CodeQL + dependency review + CycloneDX SBOM (#47)](2026/07/2026-07-14-ci-codeql-dependency-review-sbom.md)
- [2026-07-14 — CI: pin setup-dotnet to a SHA + quality-job timeout (#84, #75)](2026/07/2026-07-14-ci-pin-setup-dotnet-and-quality-timeout.md)
- [2026-07-14 — CI: release gating (full CI) + curated release notes (#37, #73)](2026/07/2026-07-14-ci-release-gating-and-curated-notes.md)
- [2026-07-14 — Fix: snupkg SourceLink + bundled Domain/Application PDBs (#36, #66)](2026/07/2026-07-14-fix-snupkg-sourcelink-and-bundled-pdbs.md)
- [2026-07-14 — Fix: runtime-resolved, single-source library version (#48, #86) — breaking (2.0)](2026/07/2026-07-14-fix-runtime-resolved-single-source-version.md)
- [2026-07-14 — CI: consistency_lint hardening + drop the deep clone (#72, #74, #82, #96)](2026/07/2026-07-14-ci-consistency-lint-hardening.md)
- [2026-07-14 — Fix: SpecificationEvaluator rejects both-orderings specs (#88)](2026/07/2026-07-14-fix-spec-both-orderings-rejected.md)
- [2026-07-14 — Fix: Error constructor null guards + inlining-safe default scan (#76, #90)](2026/07/2026-07-14-fix-error-null-guard-and-noinlining-scan.md)
- [2026-07-14 — Fix: overriding IEventBroker disables the default broker + hosted service (#92)](2026/07/2026-07-14-fix-eventbroker-override-disables-default.md)
- [2026-07-14 — Fix: EventBroker failure visibility (EventSource) + per-broker dispatch cache (#43, #44)](2026/07/2026-07-14-fix-eventbroker-failure-visibility-and-cache-scope.md)
- [2026-07-14 — Fix: trim/AOT annotations on the reflective entry points (#49)](2026/07/2026-07-14-fix-trim-aot-annotations.md)
- [2026-07-14 — Fix: RemoveAsync removes L2 before L1 (#91)](2026/07/2026-07-14-fix-removeasync-l2-before-l1.md)
- [2026-07-14 — Feat: IPasswordHasher rehash-detection + span plaintext (#35, #41)](2026/07/2026-07-14-feat-passwordhasher-rehash-and-span.md)
- [2026-07-14 — Fix: enforce the V1 salt/key length in PasswordHasher.Verify (#40)](2026/07/2026-07-14-fix-passwordhasher-v1-length-enforcement.md)
- [2026-07-14 — Docs: document the paged-but-unordered exception (#81)](2026/07/2026-07-14-docs-paged-unordered-exception.md)
- [2026-07-14 — Fix: canonical-type entity equality + transient identity (#25, #26) — breaking (2.0)](2026/07/2026-07-14-fix-entity-canonical-equality-transient-identity.md)
- [2026-07-14 — Fix: variance-aware EventBroker dispatch (#71)](2026/07/2026-07-14-fix-variance-aware-event-dispatch.md)
- [2026-07-13 — Feat: DomainEventNotification factory for runtime-typed events (#61)](2026/07/2026-07-13-feat-domain-event-notification-factory.md)
- [2026-07-13 — Feat: ThenBy/ThenByDescending secondary ordering for the Specification (#31)](2026/07/2026-07-13-feat-specification-then-by.md)
- [2026-07-13 — Fix: define the CacheService L2-outage contract (#62)](2026/07/2026-07-13-fix-cache-l2-outage-contract.md)
- [2026-07-13 — Fix: fail fast on non-round-trippable cache types (#33)](2026/07/2026-07-13-fix-cache-non-round-trippable-type-guard.md)
- [2026-07-08 — Fix: reject null elements in Result.Failure / Result.Combine (#53)](2026/07/2026-07-08-fix-result-null-element-guards.md)
- [2026-07-08 — Fix: ValidationPipelineBehavior shared-ValidationContext race (#52)](2026/07/2026-07-08-fix-validation-shared-context-race.md)
- [2026-07-08 — Test: CacheService concurrent-miss last-write-wins coverage (#51)](2026/07/2026-07-08-test-cacheservice-concurrent-miss.md)
- [2026-07-08 — Docs: SECURITY.md supported-versions + maintenance.md support window (#94)](2026/07/2026-07-08-docs-security-policy-supported-window.md)
- [2026-07-08 — Docs: IDomainEvent.OccurredOnUtc DateTimeKind contract (#98)](2026/07/2026-07-08-docs-idomainevent-datetimekind.md)
- [2026-07-08 — Docs: cross-node L1 staleness after RemoveAsync (#42)](2026/07/2026-07-08-docs-cache-cross-node-l1-staleness.md)
- [2026-07-08 — Docs: AggregateRoot thread-confinement contract (#46)](2026/07/2026-07-08-docs-aggregateroot-thread-confinement.md)
- [2026-07-08 — Docs: ValueObject collection-component equality caveat (#45)](2026/07/2026-07-08-docs-valueobject-collection-equality.md)
- [2026-07-08 — CI: fail (not skip) the Redis integration tests when Docker is expected (#100)](2026/07/2026-07-08-ci-redis-coverage-fail-loud.md)
- [2026-07-08 — Test: cover cache TTL/expiration (#101)](2026/07/2026-07-08-test-cache-ttl-expiration.md)
- [2026-07-08 — Test: broker per-event scope, runtime-type dispatch, end-to-end (#99)](2026/07/2026-07-08-test-broker-scope-dispatch-e2e.md)
- [2026-07-08 — Test: cover AddD4npUtil's zero-argument default scan (#102)](2026/07/2026-07-08-test-addd4nputil-default-scan.md)
- [2026-07-08 — Test: cover EventBroker post-shutdown rejection and drain (#55)](2026/07/2026-07-08-test-eventbroker-shutdown.md)
- [2026-07-08 — Test: verify exactly-once by payload + FIFO order (#103)](2026/07/2026-07-08-test-exactly-once-payload.md)
- [2026-07-08 — Test: make the DateTimeProvider test monotonic-safe (#104)](2026/07/2026-07-08-test-datetimeprovider-monotonic-safe.md)
- [2026-07-08 — Test: strengthen ValueObject equality property tests (#54)](2026/07/2026-07-08-test-valueobject-inequality.md)
- [2026-07-08 — Test: pin PaginatedList navigation boundaries (#56)](2026/07/2026-07-08-test-paginatedlist-boundaries.md)
- [2026-07-07 — Test: cover multi-aggregate partial-clear on dispatch (#57)](2026/07/2026-07-07-test-multi-aggregate-partial-clear.md)
- [2026-07-07 — Fix: drain domain events to a fixed point during dispatch (#30)](2026/07/2026-07-07-drain-domain-events-fixed-point.md)
- [2026-07-07 — Fix: cache serializer preserves null properties (#58)](2026/07/2026-07-07-cache-null-property-roundtrip.md)
- [2026-07-07 — Fix: Result.Errors is a truly read-only view (#85)](2026/07/2026-07-07-immutable-result-errors.md)
- [2026-07-07 — Fix: remove the implicit Error → Result conversion (#28, breaking)](2026/07/2026-07-07-drop-implicit-error-operator.md)
- [2026-07-07 — Fix: Result.Success rejects a null value (#27)](2026/07/2026-07-07-success-rejects-null.md)
- [2026-07-07 — Fix: Result.Failure rejects Error.None (#29)](2026/07/2026-07-07-failure-rejects-error-none.md)
- [2026-07-07 — Fix: assembly scan no longer auto-registers specifications (#67)](2026/07/2026-07-07-scan-drops-specifications.md)
- [2026-07-07 — Fix: assembly scan skips open-generic implementors (#65)](2026/07/2026-07-07-scan-skips-open-generics.md)
- [2026-07-07 — Fix: EventBroker disposes the dispatch scope synchronously (#60)](2026/07/2026-07-07-eventbroker-async-scope.md)
- [2026-07-07 — Fix: JSON-null L2 entry poisons L1 with a null hit (#59)](2026/07/2026-07-07-cache-null-l2-miss.md)
- [2026-07-07 — Fix: idempotent ValidationPipelineBehavior registration (#32)](2026/07/2026-07-07-idempotent-validation-behavior.md)
- [2026-07-07 — Fix: validate non-positive TTL in GetOrCreateAsync (#34)](2026/07/2026-07-07-cacheservice-validate-ttl.md)
- [2026-07-07 — Fix: EventBroker self-deadlock on in-handler re-publish (#19)](2026/07/2026-07-07-eventbroker-republish-deadlock.md)
- [2026-07-07 — Release workflow packs & publishes the NuGet package (#22)](2026/07/2026-07-07-release-pack-and-publish.md)
- [2026-07-07 — CI: enforce the ≥80% coverage threshold (#24)](2026/07/2026-07-07-ci-enforce-coverage-threshold.md)
- [2026-07-06 — CI: enforce lockfile presence (follow-up to #23)](2026/07/2026-07-06-ci-enforce-lockfile-presence.md)
- [2026-07-06 — CI: enforce restore locked mode (#23)](2026/07/2026-07-06-ci-enforce-locked-mode-restore.md)
- [2026-07-06 — Fix: CacheService breaks against a size-limited IMemoryCache (#20)](2026/07/2026-07-06-fix-cacheservice-size-limited-memorycache.md)
- [2026-07-06 — Fix: domain events dropped on removed aggregates (#21)](2026/07/2026-07-06-fix-domain-events-dropped-on-delete.md)
- [2026-07-01 — documentation i18n (zh, ja)](2026/07/2026-07-01-i18n-zh-ja.md)
- [2026-07-01 — v1.0.0 release](2026/07/2026-07-01-release-v1.0.0.md)
- [2026-07-01 — Milestone 8: utilities, DI, and 1.0 hardening](2026/07/2026-07-01-utilities-di.md)
- [2026-07-01 — Milestone 7: caching & optimization](2026/07/2026-07-01-caching.md)
- [2026-06-30 — Milestone 6: security & validation](2026/06/2026-06-30-security-validation.md)
- [2026-06-30 — Milestone 5: in-memory eventing & messaging](2026/06/2026-06-30-eventing.md)
- [2026-06-30 — Milestone 4: persistence patterns](2026/06/2026-06-30-persistence-patterns.md)
- [2026-06-30 — Milestone 3: functional Result/Error model](2026/06/2026-06-30-result-model.md)
- [2026-06-30 — Milestone 2: DDD core building blocks](2026/06/2026-06-30-ddd-core.md)
