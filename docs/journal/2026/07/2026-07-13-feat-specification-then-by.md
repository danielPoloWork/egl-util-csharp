# 2026-07-13 — Feat: ThenBy/ThenByDescending secondary ordering for the Specification (#31)

An additive `[fix]`-tier item that lands as a **feature** (the first additive 1.x API surface change since
the 1.0 freeze). Carries ADR-0025, a frozen-spec amendment (§2/§5), and a CHANGELOG `Added` entry.

## Done

- **Gap:** `ISpecification` exposed a single sort key (`OrderBy` xor `OrderByDescending`), so paging over a
  non-unique key returned rows in an undefined order across pages — the hazard the evaluator's
  "paging requires an ordering" guard is meant to prevent. EF Core cannot express a multi-column sort via a
  single composite/anonymous key, so a real `ThenBy` chain is the only fix (#31).
- **Decision (maintainer via AskUserQuestion): add `ThenBy`/`ThenByDescending` (MINOR)** over document-only.
- **Change:** `ISpecification.ThenBys` — an ordered `IReadOnlyList<(KeySelector, Descending)>` — added via a
  **default interface method** returning empty, so existing implementors do **not** break. `Specification`
  gains `ApplyThenBy`/`ApplyThenByDescending` (require a primary ordering first; re-applying a primary
  clears accumulated secondary keys). `SpecificationEvaluator` applies the primary ordering, then each
  secondary key as `ThenBy`/`ThenByDescending`. A named tuple keeps the surface minimal (no new public
  type). ADR-0025 records it; frozen spec §2/§5 amended.
- **Tests:** base-class — secondary keys captured in order with direction; `ApplyThenBy` before a primary
  throws; re-applying a primary clears secondary keys; a default spec has empty `ThenBys`. Evaluator (both
  EF InMemory + SQLite) — `ThenBy` and `ThenByDescending` break a price-20 tie deterministically.
- **Verified:** build `-warnaserror` clean (the DIM + tuple compile); `dotnet format` clean; **spec suite
  20 pass**; full suite green. Mutation-checked — dropping the evaluator's `ThenBys` loop makes the
  tie-breaker tests fail.

## State

- v1.0.0 released. **Additive** public surface (`ISpecification.ThenBys` DIM + `Specification` helpers);
  backward-compatible (empty default), accrues toward **1.1.0**. ADR-0025; spec §2/§5 amended; CHANGELOG
  `Added`. README only references specifications in the high-level blurb (no ordering detail) → no README/
  i18n change. PR drafted on `feat/specification-then-by`, awaiting merge. CI still billing-blocked
  (verified locally). No local Python → `consistency_lint` hand-verified: ADR index gains ADR-0025,
  spec-map unaffected (still §2/§5), no version/lockfile change.

## Next

- Remaining design-decision `[fix]` Mediums, each with an ADR + AskUserQuestion: #25/#26 (Entity
  transient-identity / EF-proxy equality — property-tested), #71 (contravariant handler variance), #61
  (`DomainEventNotification` runtime bridge).
