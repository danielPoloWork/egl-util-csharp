# ADR-0005: Place the functional Result/Error model in the dependency-free Domain assembly

- **Status:** Accepted
- **Date:** 2026-06-30
- **Deciders:** Maintainer
- **Related:** ADR-0003, ADR-0004, spec §2 (requirements 6–9), §4

## Context

Milestone 3 introduces the functional error model from spec §2 — `Result`, `Result<TValue>`,
the immutable `Error` record, and `Result.Combine`. The spec §4 architecture sketch listed these
under the Application layer. However, they are **pure, dependency-free primitives** used
pervasively: domain factory methods return them, application use cases return them, and
infrastructure adapters produce them. The Application assembly already carries
`MediatR.Contracts` (ADR-0003 / Milestone 2), so placing `Result` there would force that
transitive dependency onto every consumer of the library's headline "functional Result" feature.

## Decision

Place `Error`, `Result`, `Result<TValue>`, and `Result.Combine` in the dependency-free
**`It.D4np.Util.Domain`** assembly, which serves as the shared kernel for dependency-free
primitives. Specifics:

- `Error` is an immutable `record` of `Code` + `Message`; `Error.None` is the **Null Object**
  representing the absence of an error.
- `Result` carries an **ordered collection of `Error`s** (spec §2.7 specifies "an array of Error
  objects"), with a single-`Error` convenience accessor returning the first error (or `Error.None`).
- `Result.Combine` aggregates the errors of several results and forms a **monoid**: `Success()` is
  the identity and combination is associative — both laws are property-tested with FsCheck.

This **refines ADR-0003's** layer-contents sketch; spec §4 is updated in the same PR to list the
Result model under Domain.

## Alternatives Considered

- **Place it in Application (per the spec §4 sketch).** Rejected — it drags `MediatR.Contracts`
  onto every `Result` consumer, contradicting the zero-dependency-core principle of ADR-0003.
- **A separate `SharedKernel` / `Common` assembly.** Rejected for now — premature structure for a
  small library; the dependency-free Domain already fills the shared-kernel role. Revisit if
  non-domain shared types proliferate.
- **A single-`Error` `Result`** (the common minimal pattern). Rejected — spec §2.7 and §2.9 call
  for an error *array* and "collected errors" from `Combine`; multiple-error aggregation is required.

## Consequences

- `Result` is usable with **zero third-party dependencies**; domain code can return it directly.
- Spec §4 moves the Result model from Application to Domain (updated in this PR); the Null Object
  is catalogued in `docs/patterns/README.md`.
- Slightly larger API surface than a single-error `Result` (`Errors` collection + `Error`
  convenience), justified by the spec's aggregation requirement.
- `Combine`'s monoid laws are guarded mechanically by FsCheck property tests.

## References

- spec §2 (requirements 6–9), §4; ADR-0003; ADR-0004.
- `docs/patterns/design-patterns.md` — Behavioral: *Null Object*.
