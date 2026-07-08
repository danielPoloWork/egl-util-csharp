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
