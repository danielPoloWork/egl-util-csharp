# 2026-07-14 — Fix: overriding IEventBroker disables the default broker + hosted service (#92)

Roadmap PR 6 (correctness/api-design cluster). Standalone DI-wiring fix in `ServiceCollectionExtensions`.
CHANGELOG `Fixed`; no ADR (no frozen-surface change — the fix makes the already-documented "overridable"
contract true; the XML remarks were tightened inline). Proceeded without asking: not an API/MAJOR change
(non-overriding consumers are unaffected; no public signature changes).

## Done

- **Gap:** `AddD4npUtil` advertised the core singletons as "overridable — each uses `TryAdd`", but for the
  broker only the `IEventBroker` **mapping** was gated. The concrete `EventBroker` (`TryAddSingleton<EventBroker>`)
  and its hosted service (`AddHostedService(sp => sp.GetRequiredService<EventBroker>())`) were registered
  **unconditionally**. A consumer that pre-registered their own `IEventBroker` therefore still got the
  library's broker constructed and **started** as an `IHostedService` — a phantom consumer loop + channel
  nothing publishes to — while their replacement went un-hosted. Half-true override contract (#92).
- **Fix:** gate the whole broker cluster (concrete `EventBroker`, the `IEventBroker` mapping, and the hosted
  service) on `IEventBroker` being absent —
  `if (services.All(d => d.ServiceType != typeof(IEventBroker))) { … }`. A pre-registered override now fully
  replaces the default and no phantom broker runs. Also makes the registrations idempotent across repeated
  `AddD4npUtil` calls by construction (first call adds the mapping; a second finds it present and skips).
- **Docs:** tightened the `AddD4npUtil` XML remarks with an "Overriding a default" paragraph — override by
  pre-registration (first `TryAdd` wins); overriding `IEventBroker` disables the default broker entirely; if
  you want your replacement hosted, register it as an `IHostedService` yourself.
- **Test:** `Overriding_the_event_broker_disables_the_default_broker_and_its_hosted_service` — pre-registers a
  `CustomBroker : IEventBroker`, then asserts the override wins (`Same(custom, …)`), the concrete
  `EventBroker` is not registered (`GetService<EventBroker>()` is null), and nothing hosts it
  (`GetServices<IHostedService>().OfType<EventBroker>()` is empty). Mutation-verified: forcing the gate
  always-true reddens it (`Assert.Null` fails — concrete broker re-registered). The existing
  `Exposes_the_event_broker_as_a_single_hosted_instance` remains the non-override companion (default path
  still hosts one broker), and `Registering_twice_leaves_a_single_hosted_broker` still passes.
- `dotnet build -warnaserror` clean; `dotnet format --verify-no-changes` clean; full suite **218 pass / 3
  Redis skip** (+1 new).

## State

- v1.0.0 released. `AddD4npUtil` broker registration now honors the documented override contract (#92): the
  concrete broker + hosted service are registered only when no `IEventBroker` is present. CHANGELOG `Fixed`;
  no ADR. PR drafted on `fix/eventbroker-override-disables-default`, awaiting merge. CI billing-blocked
  (verified locally). No local Python → `consistency_lint` hand-verified; README untouched (no i18n churn).

## Next

- Roadmap PR 7: **#90 + #76** — two tiny correctness fixes. #90: `GetCallingAssembly` needs
  `[MethodImpl(MethodImplOptions.NoInlining)]` so the calling-assembly default-scan resolves correctly under
  inlining. #76: `Error` constructor null validation. Tight batch, unrelated-but-both-trivial.
