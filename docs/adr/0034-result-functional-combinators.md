# ADR-0034: Add functional combinators and non-throwing access to Result

- **Status:** Accepted
- **Date:** 2026-07-15
- **Deciders:** Maintainer
- **Related:** ADR-0005 (Result/Error model in Domain), ADR-0020 (dropped implicit Error conversion), spec §2, #69

## Context

Every artifact brands the model as a *functional*, *exception-free* Result (README, spec §2 "no exceptions
for expected control flow", ADR-0005), yet the public surface was `IsSuccess`/`IsFailure`/`Errors`/`Error`
plus a `Value` getter that **throws** on failure (#69). There was no `Match`/`Map`/`Bind`, no
`GetValueOrDefault`/`TryGetValue`, and `Combine` existed only for the non-generic `Result`. Consequences a
consumer hits immediately: results cannot be composed (every chain degenerates into imperative
`if (r.IsFailure) return …; var v = r.Value;`), and the only value-extraction path throws — contradicting
the "no exceptions for expected control flow" principle. The API is frozen under SemVer, but these members
are purely additive (MINOR-safe).

## Decision

Add the core consumption/composition surface as **instance members** in the Domain assembly (a MINOR):

- **`Result<TValue>`:** `Match<TResult>(Func<TValue,TResult> onSuccess, Func<IReadOnlyList<Error>,TResult> onFailure)`
  (fold), `Map<TNew>(Func<TValue,TNew>)` (functor map — success value transformed, errors propagated),
  `Bind<TNew>(Func<TValue,Result<TNew>>)` (monadic chain), and the non-throwing accessors
  `GetValueOrDefault()` / `GetValueOrDefault(TValue fallback)` / `TryGetValue(out TValue)`.
- **`Result`:** `Match<TResult>(Func<TResult> onSuccess, Func<IReadOnlyList<Error>,TResult> onFailure)`.
- `Map`/`Bind` run the delegate only on success and carry the failure's errors unchanged to the new type.
  `Map` still upholds the no-null invariant (a map returning `null` fails the `Success` guard of ADR/​#27);
  `GetValueOrDefault`/`TryGetValue` never throw for an expected failure.

Scope for this change (per the maintainer): the **synchronous core** only — no `Tap`/`OnSuccess`/`OnFailure`
side-effect combinators, no value-preserving `Combine<T>`/zip, and no `async` (`Task<Result<T>>`) overloads.
Those remain available for a later MINOR if demand appears.

## Alternatives Considered

- **Soften the "exception-free" claim in README/spec instead of adding the members.** Rejected — the
  maintainer chose to fulfil the advertised model rather than retract it.
- **Extension methods rather than members.** Members are the most discoverable for the synchronous core and
  need no extra import; extension methods would be required only for `async`/`Task<Result<T>>` chaining,
  which is out of scope here.
- **Side-effect combinators (`Tap`/`OnSuccess`/`OnFailure`) and a typed `Combine<T>` now.** Deferred to keep
  this PR's surface and test matrix focused.
- **`async` overloads now.** Deferred — they roughly double the surface and add `Task<Result<T>>` extension
  chains; sync-only ships first.

## Consequences

- Results compose (`r.Map(f).Bind(g).Match(onOk, onErr)`) and expose exception-free access
  (`GetValueOrDefault`, `TryGetValue`) — the model matches its "functional, exception-free" branding, so no
  README/spec claim needs softening. Existing code is unaffected (purely additive); accrues toward the next
  MINOR (1.1.0). Spec §2 amended. Covered by tests for each combinator (success/failure), the no-null `Map`
  guard, non-throwing accessors on failure, and null-delegate guards.

## References

- Issue #69; ADR-0005/0020/0027; spec §2; `Result`, `Result<TValue>`.
