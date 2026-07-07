# 2026-07-07 — Fix: EventBroker disposes the dispatch scope synchronously (#60)

Fourth fix from the Medium/Low audit tier. Async-correctness: a synchronous scope dispose broke on
`IAsyncDisposable`-only services.

## Done

- **Bug:** `DispatchAsync` created the per-event scope with `using IServiceScope scope =
  _scopeFactory.CreateScope();` — a synchronous `Dispose` in an async pipeline. If any service in the
  handler graph implements `IAsyncDisposable` but not `IDisposable` (common for async-resource owners),
  MS.DI's `ServiceProviderEngineScope.Dispose` throws `InvalidOperationException`. That fires *after*
  the handlers succeeded, propagates to `ExecuteAsync`'s catch, and is logged as "Failed to dispatch
  event {EventType}" — a false failure on every event — while the scope's remaining disposables leak
  (#60).
- **Fix:** `await using AsyncServiceScope scope = _scopeFactory.CreateAsyncScope();`. It disposes
  async-disposables via `DisposeAsync` and still disposes plain `IDisposable` services synchronously, so
  it is strictly safer and the documented best practice for async paths. No ADR (routine async-idiom fix,
  no API/design change).
- **Test:** `Disposes_an_async_disposable_scoped_service_without_failing_dispatch` — a scoped
  `IAsyncDisposable`-only handler; asserts it both handled the event and was disposed via `DisposeAsync`
  (bounded by a `WaitAsync` timeout so a regression fails fast, not hangs). Pre-fix the dispose throws
  and `disposed` never completes. `dotnet build -warnaserror` clean; `dotnet format` clean; 146 pass /
  2 Redis skip.
- Docs: CHANGELOG `[Unreleased]` Fixed.

## State

- v1.0.0 released. No public surface change → patch-level. PR drafted on `fix/eventbroker-async-scope`,
  awaiting maintainer merge. Actions still billing-blocked; verified locally.

## Next

- Continue Medium/Low safest-first: the assembly-scan DI fixes #65 (open-generic impls break
  BuildServiceProvider) and #67 (parameterized specs break ValidateOnBuild), then the core Result
  invariants (#29/#27/#28).
