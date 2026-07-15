# 2026-07-15 — Docs: document the DomainEvents live-view and the cache promotion-TTL (~2×ttl) caveats (#87, #93)

Two public-XML-doc caveats for documented-but-underdocumented behaviors (round-2 audit). Docs-only; no
CHANGELOG/ADR (documenting existing behavior, not a change — matching the #45/#46/#42/#98 docs-accuracy
precedent, none of which took CHANGELOG entries). #89 (the third of this cluster) was split off: the maintainer
chose to FIX construction isolation, so it lands as a separate `fix` PR with a regression test.

## Done

- **#87 — `AggregateRoot.DomainEvents` returns a live read-only view, not a snapshot.** `_domainEvents.AsReadOnly()`
  is a live wrapper: a reference captured before dispatch reads back empty once `ClearDomainEvents` runs, and
  enumerating it directly in a loop where a handler raises a follow-up event on the same aggregate throws
  `InvalidOperationException`. Documented on both `AggregateRoot.DomainEvents` (full remarks: aliasing +
  re-entrancy + "materialize `ToArray()`/`ToList()` before a dispatch loop or retention"; noted the per-access
  wrapper allocation) and `IHasDomainEvents.DomainEvents` (concise, cross-referencing the aggregate). Distinct
  from #46 (cross-thread confinement) and #30 (events cleared during dispatch) — both concern single-thread use.
  Left the optional micro-opt (cache the wrapper) / v2 snapshot out — the primary fix is documentation.
- **#93 — cache L1-promotion restarts the TTL (staleness up to ~2×ttl).** On an L2 hit, `SetMemory` re-populates
  L1 with the promoting caller's `ttl` restarted from now, so an entry read just before its L2 expiry can live
  another full ttl in that node's L1. ADR-0009 acknowledged this but the public XML docs did not. Added the
  caveat to `ICacheService.GetOrCreateAsync`'s `ttl` param doc and a dedicated `<para>` in the `CacheService`
  remarks (per-node bound ~2×ttl; promoted L1 uses the promoting call's ttl; distinct from #42 cross-node).
- **Verified:** `dotnet build -warnaserror` clean — 0 warnings (the one new cref, `InvalidOperationException`,
  resolves); `dotnet format --verify-no-changes` clean; full suite **276 pass / 3 skip**. No test change (pure
  doc additions on existing, already-tested behavior).

## State

- v1.0.0 released; #38 (PR #172) and #78/#79 (PR #173) merged. This PR is **docs-only** — no CHANGELOG, no ADR,
  no version bump. `consistency_lint` hand-traced (no local Python): no ADR/patterns/spec-map/version input
  changed; README untouched so the i18n digest still matches; all 8 checks hold. PR drafted on
  `docs/domainevents-view-and-cache-ttl-caveats` (`docs` label, Maintenance milestone), awaiting merge. CI
  billing-blocked → verified locally.

## Next

- **#89 (fix construction isolation)** — the maintainer chose to fix, not document: resolve/invoke EventBroker
  handlers with per-handler construction try/catch so a broken handler ctor no longer drops the event for
  healthy siblings; a `fix` PR with a regression test (MS.DI eagerly materializes the handler array, so this
  needs per-descriptor resolution). Then **#50** (opt-in cache-stampede coalescing — an enhancement). After
  that, only the accrued 2.0 breaks (ADR-0020/0027/0028/0030/0037) remain for the maintainer-assembled 2.0.
