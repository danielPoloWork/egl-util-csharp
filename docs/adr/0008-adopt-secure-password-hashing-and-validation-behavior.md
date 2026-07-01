# ADR-0008: Adopt secure password hashing and a validation pipeline behavior

- **Status:** Accepted
- **Date:** 2026-06-30
- **Deciders:** Maintainer
- **Related:** ADR-0003, ADR-0004, ADR-0006, ADR-0007, spec §2 (requirements 18–20), §3, §4

## Context

Spec §2 (18–20) calls for the security & validation slice: `IPasswordHasher` / `PasswordHasher`
(secure password hashing) and `ValidationPipelineBehavior<TRequest, TResponse>` (automatic request
validation on MediatR). The non-functional requirements (spec §3) pin the security parameters:
PBKDF2 with ≥ 600,000 iterations (OWASP 2024), a ≥ 128-bit random salt, and constant-time
verification. The validation behavior must run every registered FluentValidation validator and
short-circuit on failure. Both must fit the layered architecture (ADR-0003) and its dependency
policy: abstractions in Application, technology in Infrastructure.

## Decision

- **Password hashing.** Place the abstraction `IPasswordHasher` (`Hash` / `Verify`) in **Application**
  — a pure service contract, like `IEventBroker` — and the concrete `PasswordHasher` in
  **Infrastructure**. `PasswordHasher` uses **PBKDF2-HMAC-SHA256** (`Rfc2898DeriveBytes.Pbkdf2`), a
  per-password **128-bit** random salt from `RandomNumberGenerator`, a **256-bit** derived key, and a
  configurable iteration count whose **default and floor is 600,000** (the constructor rejects anything
  lower and — defense-in-depth — anything above a `MaximumIterations` ceiling of 100× the floor).
  Verification is **constant-time** via `CryptographicOperations.FixedTimeEquals`. `Hash` emits
  a **self-describing string** `PBKDF2-SHA256-V1.{iterations}.{saltB64}.{keyB64}`; `Verify` reads the
  work factor and salt from the stored value, never from the current configuration, so the iteration
  count can be raised later without invalidating already-stored hashes. `Verify` accepts a *lower*
  stored count (older hashes still verify, then rehash on next use) but rejects one above the ceiling
  before deriving, so a crafted hash cannot pin a CPU. A malformed or unrecognised
  hash returns `false` rather than throwing; `null` arguments throw. This is the **Strategy** pattern:
  an interchangeable algorithm behind a stable interface.
- **Validation.** `ValidationPipelineBehavior<TRequest, TResponse>` implements MediatR's
  `IPipelineBehavior<TRequest, TResponse>` (`where TRequest : notnull`). It runs every injected
  `IValidator<TRequest>`, aggregates the failures, and **short-circuits by throwing**
  `FluentValidation.ValidationException` when any fail; otherwise it invokes the next delegate. With no
  validators registered it passes through unvalidated. MediatR pipeline behaviors form a
  **Chain of Responsibility** — each link decides whether to forward the request or stop it.
- **Dependencies.** Application now references the **full `MediatR` package (12.5.0)** — required for
  `IPipelineBehavior` / `RequestHandlerDelegate`, which live in `MediatR`, not in the
  `MediatR.Contracts` package referenced until now — plus **`FluentValidation` (11.11.0)**. These are
  the last versions under permissive licences (MediatR 13 and FluentValidation 12 moved to commercial
  licences). The full `MediatR` package transitively provides the `MediatR.Contracts` types, so the
  prior explicit `MediatR.Contracts` reference (ADR-0004) is dropped as redundant and
  `DomainEventNotification` still compiles against `INotification`. `PasswordHasher` adds **no** new
  package dependency (BCL cryptography only).

## Alternatives Considered

- **BCrypt / Argon2 / `Microsoft.AspNetCore.Identity.PasswordHasher`.** Rejected — the spec fixes
  PBKDF2; Argon2 needs a native/third-party dependency, and depending on ASP.NET Core Identity would
  pull a web framework into a framework-agnostic library.
- **An opaque binary hash format (ASP.NET Identity style).** Rejected — a delimited, versioned,
  self-describing text format is auditable, trivially parseable, and survives a future scheme change
  via the `-V1` discriminator.
- **Throwing on a malformed stored hash in `Verify`.** Rejected — verifying an unparseable, unknown,
  or corrupt stored value can never succeed, so returning `false` is the safer, non-throwing contract.
- **Returning a `Result` failure from the behavior instead of throwing `ValidationException`.**
  Deferred — throwing the FluentValidation exception is the idiomatic MediatR pattern and keeps the
  behavior decoupled from the `Result` type and from any particular `TResponse` shape; a `Result`-aware
  variant can be added later without breaking this one.
- **Keeping `MediatR.Contracts` only.** Rejected — `IPipelineBehavior` is not in that package; the
  pipeline behavior requires the full `MediatR` package.

## Consequences

- Application gains a dependency on the full `MediatR` (12.5.0) and `FluentValidation` (11.11.0)
  packages, pinned to their last permissively-licensed major lines; revisit before upgrading across
  the commercial boundary.
- The password-hash string is a stable, versioned public contract: consumers persist it as-is, and
  raising the iteration count keeps older hashes verifiable.
- Validation failures surface as `FluentValidation.ValidationException`; the host (e.g. an API
  exception filter) maps it to a response. This is documented on the behavior.
- DI wiring (registering `IPasswordHasher`, the open-generic behavior, and assembly-scanned validators)
  is deferred to the Milestone 8 DI helper, consistent with ADR-0007.
- Adopts the **Strategy** (Behavioral) and **Chain of Responsibility** (Behavioral) patterns; both
  catalogued.

## References

- spec §2 (requirements 18–20), §3 (security NFRs), §4 (layering); ADR-0003; ADR-0004; ADR-0006; ADR-0007.
- `docs/patterns/design-patterns.md` — Behavioral: *Strategy*, *Chain of Responsibility*.
- OWASP Password Storage Cheat Sheet (2024): PBKDF2-HMAC-SHA256 ≥ 600,000 iterations.
