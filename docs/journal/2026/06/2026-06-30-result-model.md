# 2026-06-30 — Milestone 3: functional Result/Error model

## Done

- Implemented the functional error model in the dependency-free `It.D4np.Util.Domain` assembly
  (ADR-0005): `Error` (immutable record + `Error.None` Null Object), `Result`
  (`IsSuccess`/`IsFailure`, ordered `Errors`), `Result<TValue>` (guarded `Value`, implicit
  conversions from a value and from an `Error`), and `Result.Combine` (error aggregation).
- 37 tests pass (15 new): unit tests plus FsCheck property tests proving `Combine`'s monoid laws
  (identity = `Success()`, associativity).
- Recorded the placement decision (Result in Domain, not Application) in ADR-0005; synced spec §4;
  catalogued the *Null Object* pattern.

## State

- M1, M2, and the CI-hygiene PR (#9) are merged to `main`. M3 is on `feat/result-model`; PR
  drafted, awaiting maintainer merge. Version remains `0.0.0`.

## Next

- Milestone 4 — persistence patterns (Repository, Specification, Unit of Work) — after M3 merges,
  one PR at a time.
