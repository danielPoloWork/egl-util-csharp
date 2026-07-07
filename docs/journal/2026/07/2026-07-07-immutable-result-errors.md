# 2026-07-07 — Fix: Result.Errors is a truly read-only view (#85)

Tenth fix from the Medium/Low audit tier. A small, self-contained immutability fix on the core `Result`
type — no signature change, no ADR (enforces the documented immutability, like #29/#27).

## Done

- **Bug:** `Result.Errors` is typed `IReadOnlyList<Error>` but was backed by the `Error[]` the factories
  produced (`Defensive` returned `errors.ToArray()`, the ctor stored it directly). `Error[]` implements
  `IReadOnlyList<Error>`, so a consumer could `((Error[])result.Errors)[0] = other;` and mutate a
  supposedly immutable result after construction. `Result` is also a public, non-sealed class with a
  `protected` ctor taking `IReadOnlyList<Error>`, so a subclass could pass a mutable `List<Error>` and keep
  mutating it (#85).
- **Fix:** the ctor now stores `Errors = errors.Count == 0 ? NoErrors : new ReadOnlyCollection<Error>([.. errors])`
  — a defensive copy wrapped in a `ReadOnlyCollection<Error>`, so neither a downcast to `Error[]` nor a
  caller-retained reference can mutate it. `NoErrors` is a single shared empty `ReadOnlyCollection` (the
  happy success path stays allocation-free; only failures allocate the wrapper, and they already allocated
  the array). `Defensive` no longer needs its own `ToArray` (the ctor copies), so it is now purely the
  null/`Error.None` validator. Added `using System.Collections.ObjectModel;`. The declared
  `IReadOnlyList<Error>` surface is unchanged → no ADR.
- **Tests:** `Errors_is_a_read_only_view_that_cannot_be_mutated_through_a_downcast` (asserts
  `result.Errors` is not an `Error[]` and the cast throws `InvalidCastException`) and
  `A_subclass_supplied_mutable_list_cannot_mutate_errors_after_construction` (a nested `SubclassResult`
  passes a `List<Error>`, mutates it post-construction, and the result is unaffected). Both verified red
  pre-fix, green post-fix. `dotnet build -warnaserror` clean; `dotnet format` clean; 153 pass / 2 Redis
  skip.
- **Docs:** CHANGELOG `[Unreleased]` → Fixed.

## State

- v1.0.0 released. No public surface change (declared `IReadOnlyList<Error>` unchanged) → patch-level. PR
  drafted on `fix/immutable-result-errors`, awaiting merge. CI still billing-blocked (jobs don't start;
  verified locally). No local Python → `consistency_lint` hand-verified; touches only `Result.cs`, its
  test, `CHANGELOG.md`, this journal (+ index row), and the ROADMAP checkpoint pointer — no lint invariant
  affected.

## Next

- Continue Medium/Low safest-first among the remaining `[fix]` items. Candidates: #33 / #58 (cache
  serialization — polymorphic value loss / `WhenWritingNull` resurrecting defaults), #30 (handler-raised
  events cleared before publish), #31 (Specification single sort key / no `ThenBy`). The Entity-equality
  Mediums #25 / #26 are property-tested (`EqualityLawProperties`) — higher blast radius, go carefully.
  Several Mediums (#62 L2-outage, #71 handler variance, #61 DomainEventNotification bridge) carry a design
  decision → will need an ADR + a maintainer AskUserQuestion, as with #67 / #28.
