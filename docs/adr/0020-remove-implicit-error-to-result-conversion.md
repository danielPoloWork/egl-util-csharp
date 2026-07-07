# ADR-0020: Remove the implicit Error → Result<TValue> conversion (breaking)

- **Status:** Accepted
- **Date:** 2026-07-07
- **Deciders:** Maintainer
- **Related:** ADR-0005 (Result/Error model); ADR-0010 (1.0 API freeze — this ADR makes a deliberate breaking change to the frozen surface); spec §2

## Context

`Result<TValue>` shipped two implicit conversions (spec §2): from `TValue` (wrap a success) and from
`Error` (wrap a failure). The two collide:

- **`Result<Error>` is unusable.** Both operators are applicable, so `Result<Error> r = someError;` is a
  compile error (CS0457, ambiguous user-defined conversion). The collision at least self-flags here.
- **`Result<object>` silently inverts.** For `Result<object> r = someError;`, overload resolution prefers
  the more specific `Error` source type, so the assignment binds the `Error` operator and produces a
  **failure** — silently discarding an intended success that carries the error as its value. No error, no
  warning.

The `Error` operator is an advertised convenience (`return someError;` from a `Result<TValue>`-returning
method), so the collision is a genuine 1.0 API-design defect (#28).

## Decision

Remove `implicit operator Result<TValue>(Error)`. Failures are created with the explicit factory
`Result.Failure<TValue>(error)` (or `Result.Failure(errors)` for the non-generic result). The implicit
`TValue` → `Result<TValue>` success conversion is kept — it is the common ergonomic win and does not
collide.

This is a **deliberate breaking change** to the frozen v1.0 surface (it amends ADR-0010's freeze and
spec §2), classified as a breaking change rather than a bug fix: existing code using the `return error;`
idiom must migrate to `return Result.Failure<TValue>(error);`. The migration is small and mechanical, and
the removed operator was actively harmful for `Result<object>` and unusable for `Result<Error>`. The
version bump (MAJOR) is applied at release time per the project's versioning policy; the CHANGELOG records
it under *Removed* with the migration note.

## Alternatives Considered

- **Keep both operators and only document the footgun.** Rejected by the maintainer — it leaves the
  silent `Result<object>` inversion in place indefinitely.
- **Narrow the value operator so it cannot bind `Error`.** Not expressible in C#: there is no
  `where TValue : not Error` constraint, and an operator's type parameter cannot be constrained
  independently of its declaring type.
- **Remove the value operator instead.** Rejected — the value → success conversion is the more useful and
  more-used convenience; the `Error` operator is the colliding one.

## Consequences

- `Result<object> r = someError;` now binds the value operator and yields a **success carrying the error
  object** (the intended, unsurprising behavior); `Result<Error>` is constructible again.
- Consumers using `return someError;` (implicit failure) must switch to
  `return Result.Failure<TValue>(error);`; a `Result<T> = error` assignment no longer compiles.
- Spec §2 and the README quick-start drop the `Error` implicit conversion; ADR-0010's API freeze is
  annotated as amended in part.
- `Error`, `Result`, `Result.Failure`, and the `TValue` → `Result<TValue>` conversion are otherwise
  unchanged.

## References

- Issue #28; ADR-0005 (Result/Error model); ADR-0010 (1.0 API freeze, amended here); spec §2.
