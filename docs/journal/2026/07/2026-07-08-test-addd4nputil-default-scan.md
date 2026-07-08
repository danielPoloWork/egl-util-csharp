# 2026-07-08 — Test: cover AddD4npUtil's zero-argument default scan (#102)

Seventh from the zero-production-risk [test] tier. Pure test-coverage — no production change, so no
CHANGELOG and no ADR.

## Done

- **Gap:** `AddD4npUtil` falls back to `Assembly.GetCallingAssembly()` when no assemblies are supplied
  ("When no assembly is given, the calling assembly is scanned"), but every test passed the assembly
  explicitly, so the zero-argument default-scan path was never exercised (#102).
- **Test:** `Scans_the_calling_assembly_when_no_assembly_is_given` calls `services.AddD4npUtil()` with no
  arguments and asserts the test assembly's `SampleEventHandler` and `SampleValidator` are registered —
  proving `GetCallingAssembly()` resolves to the calling (test) assembly and is scanned.
- **Meaningfulness verified by mutation:** swapping `GetCallingAssembly()` → `GetExecutingAssembly()` (the
  Infrastructure assembly, which has no sample handlers) empties the resolution and fails the test; the
  real source passes. This incidentally confirms the default-scan resolves the caller's assembly, not the
  library's, on this toolchain (the `[MethodImpl(NoInlining)]` hardening is tracked separately as #90).
  `dotnet build -warnaserror` clean; `dotnet format` clean; 166 pass / 2 Redis skip.

## State

- v1.0.0 released. Test-only → no shipped change. PR drafted on `test/addd4nputil-default-scan`, awaiting
  merge. CI still billing-blocked (jobs don't start; verified locally). No local Python →
  `consistency_lint` hand-verified; touches only a test file, this journal (+ index row), and the ROADMAP
  checkpoint pointer — no lint invariant affected.

## Next

- Continue zero-risk [test]/[docs] safest-first: #99 (broker per-event DI-scope / runtime-type dispatch
  end-to-end through AddD4npUtil), #101 (cache TTL/expiration coverage), #100 (Redis-skip silent-vanish),
  #51/#52 (cache/validation concurrency — flakier), or docs-accuracy (#42/#45/#46/#94/#98). The remaining
  `[fix]` Mediums (#31/#33/#25/#26/#62/#71/#61) carry design decisions → ADR + a maintainer
  AskUserQuestion.
