# 2026-06-30 — Milestone 6: security & validation

## Done

- Application abstraction `IPasswordHasher` (`Hash` / `Verify`) and `ValidationPipelineBehavior<TRequest, TResponse>`
  — a MediatR `IPipelineBehavior` that runs every registered FluentValidation `IValidator<TRequest>`, aggregates
  failures, and short-circuits with a `ValidationException` (else passes through).
- Infrastructure `PasswordHasher : IPasswordHasher` — PBKDF2-HMAC-SHA256, 128-bit random salt, 256-bit derived key,
  iteration floor + default 600,000 (OWASP 2024), constant-time `FixedTimeEquals` verification, and a self-describing
  versioned hash string `PBKDF2-SHA256-V1.{iterations}.{saltB64}.{keyB64}` so the work factor can be raised without
  invalidating stored hashes (ADR-0008).
- Dependencies: Application moved from `MediatR.Contracts` to the full `MediatR` 12.5.0 (for `IPipelineBehavior`) and
  added `FluentValidation` 11.11.0 — the last permissively-licensed lines (MediatR 13 / FluentValidation 12 went
  commercial). `PasswordHasher` adds no package dependency (BCL crypto only).
- 81 tests pass (+23 new): hash/verify round-trip, wrong-password rejection, salt uniqueness, iteration count ≥ 600k,
  a known-answer cross-check against the BCL primitive, forward-compatible iteration count, malformed/tampered-hash
  handling, null guards; validation pass-through, short-circuit, multi-validator aggregation, and null guards.
  build `-warnaserror`, `dotnet test`, and `dotnet format --verify-no-changes` all green. Adopted patterns: Strategy,
  Chain of Responsibility.

## State

- M1–M5 merged. M6 on `feat/security-validation`; PR drafted, awaiting maintainer merge. Version remains `0.0.0`.

## Next

- Milestone 7 — caching & optimization (`ICacheService` two-level, `DistributedCacheExtensions`, Redis via
  Testcontainers) — after M6 merges.
