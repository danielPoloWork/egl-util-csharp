# 2026-07-08 — Docs: cross-node L1 staleness after RemoveAsync (#42)

Thirteenth from the zero-production-risk tier; a docs-accuracy clarification of a coherence limitation.
No code/behavior change, so no CHANGELOG and no new ADR — ADR-0009 is enriched with a pre-existing
consequence (no decision reversed).

## Done

- **Gap:** `CacheService.RemoveAsync` (and writes) touch only the calling process's L1 (`IMemoryCache`,
  per-process) and the shared L2 (`IDistributedCache`). There is no invalidation channel to other
  instances, so a peer keeps serving its own L1 copy until that entry expires/is evicted — with
  `ttl = null`, until eviction. This is correct for a two-level cache without an invalidation bus, but it
  was undocumented; ADR-0009 covered only the stampede/last-write-wins limitation (#42).
- **Fix (docs-only):** added a `<b>Cross-node coherence.</b>` `<para>` to `CacheService`'s `<remarks>`
  and a `<remarks>` to `ICacheService.RemoveAsync` stating the per-process L1 / shared-L2 split, that
  `RemoveAsync` does not reach peers' L1, and the mitigation — pass a bounded `ttl` so L1 self-expires and
  the staleness window is capped. Enriched ADR-0009 with a Consequences bullet (the coherence gap) and an
  Alternatives-Considered entry for an opt-in Redis pub/sub invalidation bus, marked **Deferred** in the
  same style as the stampede-lock deferral (can be added later as opt-in without changing the
  `ICacheService` contract).
- **Scope:** the issue's "optionally offer a Redis pub/sub channel" is an explicitly-optional *feature*
  (new API + ADR + a design decision), not part of this Low/`docs` item — documented as a deferred
  alternative rather than implemented. If the maintainer wants the bus, it should be its own feature
  issue/PR with an AskUserQuestion.
- **Review catch:** a 3-lens adversarial pass (accuracy / consistency / skeptic) unanimously flagged that
  the `RemoveAsync` remark's first draft said a no-TTL peer copy lives "*(never, if cached with no TTL)*" —
  dropping eviction and contradicting the two sibling docs (CacheService + ADR-0009) written in the same
  change, which correctly say "expires **or is evicted**". Since every L1 entry is written with `Size = 1`,
  a no-TTL entry is still evictable under a `SizeLimit` cache (#20), so "never" was literally false. Fixed
  to "expires or is evicted (with no TTL, until it is evicted or explicitly removed)". Also took the one
  named consistency nit — `each node's L1 entry` → `each instance's L1 entry`, leaving `cross-node` as the
  only remaining "node" (a fixed compound adjective).
- `dotnet build -warnaserror` clean; `dotnet format` clean; 172 pass / 3 Redis skip (unchanged).

## State

- v1.0.0 released. Docs-only (two XML `<remarks>` + ADR-0009 Consequences/Alternatives); no behavior/API
  change. PR drafted on `docs/cache-cross-node-l1-staleness`, awaiting merge. Wording adversarially
  reviewed before drafting. CI still billing-blocked (verified locally). No local Python →
  `consistency_lint` hand-verified; touches `CacheService.cs` + `ICacheService.cs` (doc comments),
  `docs/adr/0009-*.md` (no new ADR → adr-index unchanged), this journal (+ index row), and the ROADMAP
  pointer — no lint invariant affected. README untouched → no i18n digest recompute.

## Next

- Continue docs-accuracy safest-first: #98 (IDomainEvent.OccurredOnUtc DateTimeKind is contract-only),
  #94 (SECURITY.md supported-versions table stale post-1.0 — a small policy call). Then #51/#52
  (cache/validation concurrency). The remaining `[fix]` Mediums (#31/#33/#25/#26/#62/#71/#61) carry design
  decisions → ADR + a maintainer AskUserQuestion.
