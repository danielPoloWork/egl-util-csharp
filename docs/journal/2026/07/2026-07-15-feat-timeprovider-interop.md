# 2026-07-15 — Feat: bridge IDateTimeProvider to the BCL TimeProvider (#97)

Roadmap PR 20 (refactor/API cluster). Maintainer chose (AskUserQuestion) **interop now (MINOR)**. CHANGELOG
`Added`; ADR-0036; spec §5 amended. Non-breaking.

## Done

- **Gap (#97):** `IDateTimeProvider` (UtcNow only) duplicates .NET 8's `System.TimeProvider`; consumers ended
  up with two parallel clocks and code against `IDateTimeProvider` couldn't use the `TimeProvider` ecosystem.
- **Bridge:**
  - `DateTimeProvider(TimeProvider? timeProvider = null)` — falls back to `TimeProvider.System`; `UtcNow` =
    `_timeProvider.GetUtcNow().UtcDateTime` (Kind = UTC, per the `IDateTimeProvider` contract). The optional
    parameter keeps `new DateTimeProvider()` working (non-breaking manual construction).
  - `AddD4npUtil` registers `TimeProvider.System` (`TryAddSingleton(TimeProvider.System)`), so a consumer can
    inject `TimeProvider` directly and the default `DateTimeProvider` delegates to the same instance — both
    clocks agree.
  - Documented `IDateTimeProvider` as a thin facade over `TimeProvider`, recommending `TimeProvider` for new
    code that needs `DateTimeOffset`/timestamps/timers.
- **Tests (+2):** `DateTimeProvider` delegates to an injected `TimeProvider` (a `FixedTimeProvider` stub — no
  `FakeTimeProvider` dependency); DI registers a `TimeProvider` (`Same(TimeProvider.System, …)`) the
  `DateTimeProvider` shares. The existing monotonic-safe default-clock test (#104) still passes (default →
  `TimeProvider.System`).
- New Infrastructure/Application code passes the analyzer gate (#166). `dotnet build -warnaserror` +
  `dotnet format` clean; full suite **266 pass / 3 Redis skip** (+2).

## State

- v1.0.0 released. The library's clock delegates to `TimeProvider`; `TimeProvider.System` is DI-registered;
  `IDateTimeProvider` is a documented facade. Non-breaking. CHANGELOG `Added`; ADR-0036 (+ index); spec §5
  amended. README untouched (clock isn't in the README surface → no i18n churn). PR drafted on
  `feat/timeprovider-interop`, awaiting merge. CI billing-blocked (verified locally).
- **2.0 follow-up:** consider `[Obsolete]` on `IDateTimeProvider` toward `TimeProvider`.

## Next

- PR 21: **#38** — split the MediatR/FluentValidation-coupled types (`DomainEventNotification`,
  `ValidationPipelineBehavior` + those package deps) into an opt-in package (e.g. `It.D4np.Util.MediatR`) so
  the DDD/Result core is dependency-free. **MAJOR/2.0**, large — needs its own sub-plan (package/project
  layout, bundling, dependency graph, DI split) and likely more AskUserQuestion rounds. Then Docs/Low batches
  (#78+#79, #87+#89+#93, #50).
