# 2026-07-14 — Fix: EventBroker failure visibility (EventSource) + per-broker dispatch cache (#43, #44)

Roadmap PR 5 (correctness/observability cluster). Two EventBroker robustness fixes, tight batch, same file.
CHANGELOG `Added` (#43) + `Fixed` (#44); no ADR (no frozen-surface change; the EventSource name is a new
diagnostics contract, documented in the type remarks). Maintainer chose both approaches up front (EventSource
for #43, instance-scoped cache for #44).

## Done

- **#43 — failures invisible without a logger.** A throwing handler was isolated and logged via
  `_logger?.LogError(...)`; a broker built with **no** logger (manual construction, or a host with no logging
  provider) dropped the failure with zero trace. **Fix:** added an internal `EventBrokerEventSource`
  (`[EventSource(Name = "It.D4np.Util.EventBroker")]`, singleton) with `HandlerFailed` / `DispatchFailed`
  events, and emit through it **unconditionally** in both catch blocks (handler-level and the last-resort
  dispatch guard), in addition to the optional logger. Failures now surface via ETW / `dotnet-trace` / an
  `EventListener` regardless of logging config. No public-API change; the source name is the subscribe
  contract (documented in the type/ctor remarks).
- **#44 — process-static dispatcher cache leak.** The `ConcurrentDictionary<Type, EventDispatch>` was
  `static`, outliving every broker/provider and pinning a closed generic type + compiled delegate per
  distinct published runtime type for the process lifetime (a slow, uncollectable leak under dynamic/plugin
  hosts or unbounded generic event types like `Envelope<TPayload>`). **Fix:** made it a per-broker instance
  field (`_dispatchers`), collected with the broker and its provider; per-broker recompute is one-time per
  event type. No API/behavior change.
- **Tests (both non-vacuous, mutation-verified):**
  - #43: `A_throwing_handler_is_reported_through_the_EventSource_even_without_a_logger` — a `CollectingEventListener`
    (an `EventListener` subclass) subscribes to the source, the broker is built with **no** logger, and after
    a drain the test asserts a `HandlerFailed` event naming the specific throwing handler was captured.
    Mutation: removing the emit → listener captures nothing → test fails.
  - #44: `The_dispatch_plan_cache_is_instance_scoped_not_process_static` — reflection guard asserting **no**
    `static` `ConcurrentDictionary<,>` field and **an** instance one. Mutation: reverting the field to
    `static` → test fails on the `DoesNotContain` assertion.
- **EventListener pitfall handled:** `OnEventSourceCreated` fires during the base `EventListener` constructor,
  before the derived ctor body runs, so the source-name match uses the compile-time const (not an instance
  field, which would still be null during that replay and would miss an already-created source from an
  earlier test).
- `dotnet build -warnaserror` clean; `dotnet format --verify-no-changes` clean; full suite **217 pass / 3
  Redis skip** (+2 new). The EventSource constructed without a manifest error (the #43 test exercises it).

## State

- v1.0.0 released. EventBroker now: (1) reports every isolated failure through the `It.D4np.Util.EventBroker`
  EventSource even without a logger (#43, additive); (2) caches dispatch plans per-broker, not process-static
  (#44, leak fix). CHANGELOG `Added` (#43) + `Fixed` (#44); no ADR. PR drafted on
  `fix/eventbroker-failure-visibility`, awaiting merge. CI billing-blocked (verified locally). No local
  Python → `consistency_lint` hand-verified; README untouched (EventBroker isn't in the README surface, no
  i18n churn).

## Next

- Roadmap PR 6: **#92** — an `IEventBroker` override (`TryAddSingleton<IEventBroker>`) leaves the concrete
  `EventBroker` still registered as the `IHostedService`, so a replacement broker isn't the one that runs.
  Standalone DI-wiring fix in `ServiceCollectionExtensions`.
