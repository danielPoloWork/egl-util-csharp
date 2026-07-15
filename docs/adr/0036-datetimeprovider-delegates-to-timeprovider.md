# ADR-0036: Bridge IDateTimeProvider to the BCL TimeProvider

- **Status:** Accepted
- **Date:** 2026-07-15
- **Deciders:** Maintainer
- **Related:** ADR-0004 (DDD building blocks / IDateTimeProvider), spec §5, #97

## Context

The library targets net8.0, where the BCL ships `System.TimeProvider` (with `TimeProvider.System` and, for
tests, `Microsoft.Extensions.TimeProvider.Testing`'s `FakeTimeProvider`) as the standard injectable clock.
`IDateTimeProvider` (UtcNow only) predates that and re-invents a narrower surface, and it is now frozen into
the 1.0 API and DI wiring. Consumers on the modern stack end up with two parallel clock abstractions, and
code written against `IDateTimeProvider` cannot benefit from the ecosystem tooling built around
`TimeProvider` (#97).

## Decision

Bridge the library's clock to `TimeProvider` without breaking the frozen `IDateTimeProvider` surface (a
MINOR):

- `DateTimeProvider` takes an optional `TimeProvider` (`DateTimeProvider(TimeProvider? timeProvider = null)`,
  falling back to `TimeProvider.System`) and computes `UtcNow` as `_timeProvider.GetUtcNow().UtcDateTime`
  (Kind = UTC, upholding the `IDateTimeProvider` contract). The optional parameter keeps
  `new DateTimeProvider()` working (non-breaking for manual construction).
- `AddD4npUtil` registers `TimeProvider.System` (`TryAddSingleton(TimeProvider.System)`), so a consumer can
  inject `TimeProvider` directly, and the default `DateTimeProvider` delegates to the same instance — both
  clocks agree.
- `IDateTimeProvider` is documented as a thin facade over `TimeProvider`, recommending `TimeProvider` for new
  code that needs `DateTimeOffset`, high-resolution timestamps, or timers.

## Alternatives Considered

- **Document the overlap only** (no code). Rejected — leaves the two clocks disconnected; the maintainer
  chose real interop so both resolve to one instance.
- **Make `DateTimeProvider`'s `TimeProvider` a required constructor parameter.** Rejected — source-breaking
  for anyone constructing `new DateTimeProvider()`; an optional parameter with a `TimeProvider.System`
  fallback is non-breaking and DI still injects the registered instance.
- **Obsolete/replace `IDateTimeProvider` with `TimeProvider` now.** Rejected for this MINOR — `[Obsolete]` on
  a frozen 1.0 type is a churn a patch/minor should not impose; deferred to 2.0.

## Consequences

- The library and its consumers share one clock (`TimeProvider.System` by default); tests can substitute any
  `TimeProvider` (including `FakeTimeProvider`) and both `IDateTimeProvider` and injected `TimeProvider`
  observe it. Non-breaking (the `IDateTimeProvider` surface and `new DateTimeProvider()` are unchanged);
  accrues toward the next MINOR (1.1.0). Spec §5 amended. Covered by tests: `DateTimeProvider` delegates to
  the injected `TimeProvider`, the default falls back to `TimeProvider.System`, and DI registers a
  `TimeProvider` the `DateTimeProvider` shares.
- **2.0 follow-up:** consider marking `IDateTimeProvider` `[Obsolete]` in favour of `TimeProvider`.

## References

- Issue #97; ADR-0004; spec §5; `IDateTimeProvider`, `DateTimeProvider`, `ServiceCollectionExtensions`.
