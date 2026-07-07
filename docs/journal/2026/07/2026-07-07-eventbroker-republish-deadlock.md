# 2026-07-07 — Fix: EventBroker self-deadlock on in-handler re-publish (#19)

The **last** of the six High-severity audit findings. Concurrency bug: a handler re-publishing on a
full channel permanently deadlocked the broker.

## Done

- **Root cause:** the single consumer both drains the bounded channel and runs handlers inline. A
  handler awaiting `PublishAsync` while the channel is full blocks on `WriteAsync` (FullMode.Wait) — but
  the only reader that can free a slot is the consumer, parked in that handler. Permanent self-deadlock;
  shutdown drain hangs to `ShutdownTimeout`.
- **Fix (maintainer chose "throw on full"):** an in-handler (re-entrant) publish is now non-blocking.
  An `AsyncLocal<bool>` marks the consumer's dispatch flow; on that flow `PublishAsync` uses `TryWrite`
  (enqueue if room) and throws `InvalidOperationException` if the channel can't accept (full / closed),
  rather than waiting. External publishers are unchanged (blocking back-pressure). Preserves bounded
  back-pressure, single-consumer sequential/in-order, exactly-once. ADR-0018 records the design
  (rejected: offload — needs an unbounded buffer that kills back-pressure; log-drop; document-only;
  all-non-blocking).
- **Tests:** a chaining handler's follow-up is delivered on a non-full channel; on a capacity-1 full
  channel the in-handler publish fails fast (throws, caught) instead of deadlocking — bounded by a
  `WaitAsync` timeout so a regression manifests as a fast failure, not a hang. `dotnet build
  -warnaserror` clean; `dotnet format` clean; 140 pass / 2 Redis skip; the concurrent-publisher
  back-pressure test still passes.
- Docs: ADR-0018, EventBroker/IEventBroker XML contracts, CHANGELOG (Fixed).

## State

- v1.0.0 released. Behaviour addition (in-handler publish throws on saturation), not an API change →
  patch-level. PR drafted on `fix/eventbroker-republish-deadlock`, awaiting maintainer merge.
- **This closes the 6 High-severity audit findings** (#19–#24) plus the lockfile-presence follow-up.

## Next

- After merge, the audit backlog's High tier is done. Remaining audit issues are Medium/Low (#25–#104,
  minus those already closed). Pick the next batch safest-first, or cut a release (the version bump +
  changelog roll) now that packaging/CI gates are in place — maintainer's call.
