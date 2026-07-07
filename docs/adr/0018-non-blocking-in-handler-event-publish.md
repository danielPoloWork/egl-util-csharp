# ADR-0018: Make in-handler event publishing non-blocking to prevent broker self-deadlock

- **Status:** Accepted
- **Date:** 2026-07-07
- **Deciders:** Maintainer
- **Related:** ADR-0007 (refines its threading contract), issue #19, `EventBroker`, `IEventBroker`

## Context

ADR-0007's `EventBroker` is a single background consumer that both **drains** the bounded channel and
**runs handlers inline** (sequentially, in publish order, exactly-once). If a handler re-publishes —
event chaining / a saga step — by awaiting `PublishAsync` while the channel is at capacity, the
publish's `WriteAsync` (`FullMode.Wait`) blocks waiting for a free slot. But the only reader that can
free one is the consumer, which is parked inside that very handler. The result is a permanent, silent
**self-deadlock** of the whole broker; shutdown then hangs until `HostOptions.ShutdownTimeout` abandons
the drain. The docs advertised back-pressure and safe concurrent publishing but never warned about this
(issue #19, High).

## Decision

An in-handler (re-entrant) publish is made **non-blocking**. The consumer marks its dispatch flow with
an `AsyncLocal<bool>` (per broker instance) that flows into the handler and the calls it awaits.
`PublishAsync` checks it: on the dispatch flow it uses `TryWrite` — enqueue if there is room, otherwise
**throw `InvalidOperationException`** rather than wait. External (non-handler) publishers see the flag
as `false` and keep the existing blocking `WriteAsync` back-pressure.

This breaks the self-deadlock while preserving every other guarantee ADR-0007 made: bounded
back-pressure for external publishers, single-consumer sequential in-order delivery, and exactly-once
consume. A handler doing chaining should tolerate a failed re-entrant publish (catch it, or keep chains
short and capacity ample); if uncaught, the broker's per-handler isolation logs it and continues.
Verified: a re-entrant publish is delivered on a non-full channel, and on a full (capacity-1) channel
it fails fast instead of deadlocking.

## Alternatives Considered

- **Offload dispatch off the reader** so it keeps draining while handlers run. Rejected — preserving
  sequential/in-order delivery needs an internal handoff buffer between reader and dispatcher; a
  *bounded* one merely relocates the same deadlock, and an *unbounded* one destroys the bounded
  back-pressure ADR-0007 deliberately chose. It cannot preserve the contract.
- **Log-and-drop the re-entrant publish when full** (return instead of throw). Rejected — throwing is
  more visible and gives the handler agency; a silently dropped follow-up is easy to miss.
- **Document-only** (warn that handlers must not block-publish). Rejected — it does not prevent the
  deadlock; a legitimate chaining handler still hangs.
- **Make all publishes non-blocking.** Rejected — that removes back-pressure for external publishers
  (the point of the bounded channel, and the concurrent-publisher guarantee). Only the in-handler path
  needs to change.

## Consequences

- The broker no longer self-deadlocks when a handler re-publishes under back-pressure, and shutdown no
  longer hangs on it. This refines ADR-0007's threading contract.
- New documented behaviour: an in-handler publish that the channel cannot immediately accept (full, or
  closed during shutdown) throws `InvalidOperationException` — isolated and logged by the per-handler
  guard if the handler does not catch it. This is a behaviour addition, not an API change; the
  `PublishAsync` signature is unchanged, so it is a patch-level fix.
- External publisher behaviour (blocking back-pressure) and the delivery/ordering guarantees are
  unchanged; the concurrent-publisher test still passes.

## References

- issue #19; ADR-0007; spec §2 (requirements 15–17), §3 (thread-safety NFR).
- `docs/patterns/design-patterns.md` — Concurrency: *Producer-Consumer*; EIP: *Publish-Subscribe*.
