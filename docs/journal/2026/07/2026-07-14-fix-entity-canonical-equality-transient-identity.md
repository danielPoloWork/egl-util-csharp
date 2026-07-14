# 2026-07-14 — Fix: canonical-type entity equality + transient identity (#25, #26) — breaking (2.0)

The final design-decision pair, and the largest change: a **breaking (MAJOR / 2.0)** reshape of the two
most foundational types. Carries ADR-0028 (supersedes the equality parts of ADR-0004, breaks the ADR-0010
freeze), a frozen-spec amendment (§2/§4/§5), and a CHANGELOG **BREAKING** entry.

## Done

- **Bugs:** `Entity<TId>` equality used the **runtime** type (`GetType()`), so (#26) an EF Core proxy
  (`CustomerProxy : Customer`) compared **unequal** to a plain instance of the same `Id`; and the ctor
  accepted a default `Id`, so (#25) two distinct unsaved entities with `Guid.Empty`/`0` compared **equal**
  and collapsed in a `HashSet`/`Dictionary`.
- **Decisions (maintainer via AskUserQuestion):** #25 → reference-equality for a default (transient) `Id`;
  #26 → an **explicit canonical equality type** (not runtime-type inspection, no proxy-name heuristic). I
  chose the maintainer's first-listed mechanism, the **self-referencing generic**.
- **Change (breaking):** `Entity<TId, TEntity> where TEntity : Entity<TId, TEntity>` — equality compares
  the canonical type `TEntity` (a proxy/subclass shares its parent's canonical type → compares equal; #26)
  and a **non-default** `Id`; a transient default-`Id` entity compares by reference and hashes by reference
  (#25). `AggregateRoot<TId, TAggregate>` and `IRepository`'s constraint (`AggregateRoot<TId, TEntity>`)
  follow. Domain stays dependency-free. Updated the stale `Entity{TId}`/`AggregateRoot{TId}` doc crefs.
- **Tests:** proxy/plain symmetry + transitivity + hash consistency; different canonical types sharing an
  `Id` (via `object`); transient reference identity (two `Guid.Empty` entities distinct, both retained in a
  `HashSet`); the `EqualityLawProperties` identity property updated so a default `Id` is transient. The EF
  persistence suite (InMemory + SQLite, `Product`/`Category` reshaped) still passes.
- **Verified:** build `-warnaserror` clean (CRTP + proxy tests compile, all crefs resolve); `dotnet format`
  clean; full suite **201 pass / 3 Redis skip** (+2). Mutation-checked — see below.

## State

- v1.0.0 released. **Breaking (MAJOR / 2.0)** reshape of `Entity`/`AggregateRoot` (+ `IRepository`
  constraint). ADR-0028 (supersedes ADR-0004 equality parts, breaks ADR-0010 freeze); frozen spec §2/§4/§5
  amended; CHANGELOG **BREAKING** + migration note; version accrues to **2.0.0** at release. **No README/
  i18n mention of these types → no translation change.** PR drafted on
  `fix/entity-canonical-equality-transient-identity`, awaiting merge. CI still billing-blocked (verified
  locally). No local Python → `consistency_lint` hand-verified: ADR index gains ADR-0028, spec-map
  unaffected (still §2/§4/§5), no version/lockfile change.

## Next

- The audit backlog's substantive design-decision items are now **drained** (all Highs long done; the
  Medium `[fix]` forks #31/#33/#25/#26/#62/#71/#61 resolved; docs/test tiers complete). Remaining work is
  ordinary SemVer maintenance and whatever the maintainer prioritizes next — notably cutting **2.0** once
  the breaking PRs (#28 earlier, #33/#62/#71 behavior changes, and this reshape) are merged and the release
  is assembled.
