# 2026-07-07 — Fix: drain domain events to a fixed point during dispatch (#30)

Twelfth fix from the Medium/Low audit tier. A concurrency/correctness fix to the domain-event dispatch
contract. Because it touches the documented at-least-once contract (ADR-0012), the maintainer chose the
approach — "drain to a fixed point" (over "clear only published" / "document only") — via an
AskUserQuestion, as with #67 and #28.

## Done

- **Bug:** both `DispatchDomainEventsAsync` overloads published an aggregate's buffered events and then
  called `ClearDomainEvents()`, wiping the aggregate's **entire** buffer. If a synchronous, in-process
  `publish` caused a handler to raise a further event on the same aggregate, that event was appended but
  not in the iterated snapshot — cleared without being published. Silent, exception-free loss; the
  documented at-least-once contract did not cover handler-raised events (#30).
- **Fix (both overloads):** extracted `PublishThenDrainAndClearAsync` — publish the current batch, then
  re-read the aggregate's buffer and publish anything that appeared while handlers ran, looping to a fixed
  point, and clear only once nothing remains. Published events are tracked by reference identity
  (`ReferenceEqualityComparer`), so the whole-buffer clear is deferred until everything — including
  handler-raised events — has been delivered. The change-tracker overload seeds the batch with the
  aggregate's live events; the snapshot overload seeds it with the captured (pre-save) copy, then drains
  the live buffer.
- **Contract preserved:** on a publish failure the exception propagates before the clear, so the failing
  aggregate and every aggregate after it keep their events for an at-least-once retry, and already-cleared
  aggregates are not re-published — the per-aggregate clear-on-success semantics from ADR-0012 are
  unchanged. Livelock: a handler that raises events on the same aggregate without bound will not terminate
  (a consumer error). Residual (documented in ADR-0022): an event raised on a *different*, already-cleared
  aggregate earlier in the same pass is not re-visited — raise cross-aggregate follow-ups in a new unit of
  work.
- **ADR-0022** records the decision and **amends ADR-0012's delivery contract** (0012 got an "Amended in
  part by ADR-0022" header note). Indexed. No public API/signature change.
- **Tests:** `DispatchDomainEventsAsync_drains_events_a_handler_raises_during_dispatch` and
  `Snapshot_dispatch_drains_events_a_handler_raises_during_dispatch` — a handler raises a follow-up on the
  aggregate during dispatch; both assert the follow-up is delivered (in order) and the buffer ends empty.
  Both verified red pre-fix (only the first event delivered), green post-fix. `dotnet build -warnaserror`
  clean; `dotnet format` clean; 156 pass / 2 Redis skip.
- **Docs:** CHANGELOG `[Unreleased]` → Fixed; the XML `<remarks>` on both overloads now describe draining
  + the livelock caveat.

## State

- v1.0.0 released. No public surface change → patch-level (behavioral: at-least-once now covers
  handler-raised events). PR drafted on `fix/drain-domain-events-fixed-point`, awaiting merge. CI still
  billing-blocked (jobs don't start; verified locally). No local Python → `consistency_lint` hand-verified;
  touched invariant: **adr-index** (0022 added, indexed, sequential 0001–0022). Others unaffected.

## Next

- Continue Medium/Low safest-first. Remaining `[fix]`: #31 (Specification single sort key / no `ThenBy` →
  adds to the frozen `ISpecification` surface — a design decision, ADR + AskUserQuestion), #33 (cache
  polymorphic static-`T` loss — document-vs-`$type` decision), the Entity-equality #25/#26 (property-tested,
  higher blast radius), and #62/#71/#61 (design decisions). No-code-change tiers ([test]/[docs]/[ci]) also
  remain and are zero production risk.
