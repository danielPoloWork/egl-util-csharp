# 2026-07-15 — Docs: correct the frozen spec's Result/Error layer + consumer import namespaces (#78, #79)

Two frozen-spec/contract accuracy fixes from the round-2 audit. Docs-only; no ADR, no CHANGELOG (these are
repo-internal governance docs — the frozen spec, AGENTS.md, packaging workflow — not the shipped API surface).

## Done

- **#78 — spec §4 mermaid placed `Result / Error` in the Application subgraph**, contradicting the same
  section's prose (line 55, "Domain … the functional Result/Result<T>/Error model"), ADR-0005 (whose whole
  decision is "place Result/Error in Domain"), and the shipped code (`namespace It.D4np.Util.Domain`). Moved
  the `RES[Result / Error]` node from the Application subgraph into the Domain subgraph. (I noticed this exact
  inaccuracy while reworking the diagram for #38 and deliberately left it as pre-existing/out-of-scope; it is
  its own ticket, now fixed.) No arrow referenced `RES`, so the move is clean.
- **#79 — spec §5 and AGENTS.md §5 claimed `using It.D4np.Util;`**, but no public type lives in that bare
  namespace — every public type is in `It.D4np.Util.Domain` / `.Application` / `.Infrastructure`, and the only
  member of `It.D4np.Util` itself is `LibraryVersion` (`Domain/Version.cs`). Corrected the lead-in of spec §5
  and AGENTS.md §5 line 93 to import via the layer namespaces, noting the bare namespace holds only
  `LibraryVersion`. (My #38 §5 edit had kept the `using It.D4np.Util;` lead-in — this corrects it.)
- **Same-drift sweep:** a repo-wide grep for `using It.D4np.Util;` found two more occurrences. Fixed the
  living doc `docs/workflow/packaging.md` (same correction). **Left `docs/adr/0002-...md:43` unchanged** — ADRs
  are point-in-time records and the repo's convention is supersession/amendment *notes*, not body rewrites
  (cf. ADR-0009/0010); ADR-0002's core decision (source layout + root namespace `It.D4np.Util`) stands, and
  the current API-guidance docs (spec, AGENTS.md, packaging, README) are now all correct.

## State

- v1.0.0 released; #38 (opt-in MediatR package) merged as PR #172. This PR is **docs-only** — no code, no
  CHANGELOG, no ADR, no version bump. `consistency_lint` unaffected (no ADR/patterns/spec-map/version input
  changed; README untouched so the i18n digest still matches). Build/test unaffected (276 pass baseline). PR
  drafted on `docs/spec-import-ns-and-result-layer` (`docs` label, Maintenance milestone), awaiting merge. CI
  billing-blocked → verified locally / by hand-trace.

## Next

- Remaining backlog: #87 (AggregateRoot.DomainEvents live-view aliasing — XML docs), #89 (EventBroker
  isolation covers execution not construction — XML docs), #93 (cache L1-promotion 2×ttl staleness — XML docs)
  — a public-XML-doc-caveat batch; then #50 (opt-in cache-stampede coalescing — an enhancement). After that,
  only the accrued 2.0 breaks (ADR-0020/0027/0028/0030/0037) remain for the maintainer-assembled 2.0.
