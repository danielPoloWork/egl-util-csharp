# 2026-07-08 — Docs: SECURITY.md supported-versions + maintenance.md support window (#94)

Fifteenth from the zero-production-risk tier; a docs-accuracy fix with a small maintainer policy call.
No code/behavior change, so no CHANGELOG and no ADR.

## Done

- **Gap:** `SECURITY.md` still read "*Until `v1.0.0`, only the latest released minor line receives
  security fixes*" with a table listing only `0.x` — but `v1.0.0` shipped 2026-07-01, so a reporter
  reading it today would conclude **no released version is supported**. `SECURITY.md` deferred the
  post-1.0 window to `docs/workflow/maintenance.md`, but that file's "Security fixes" section only said
  "backport to every supported line" and linked back to `SECURITY.md` — a **circular reference** defining
  the window in neither doc (#94).
- **Policy call (maintainer via AskUserQuestion):** chose **current major + prior major for 6 months**.
  Within a major only the latest released MINOR gets fixes (minors are backward-compatible); when a new
  MAJOR ships, the previous MAJOR stays supported for **6 months** from that release as a bounded
  migration window; only one prior major is kept alive; `0.x` is unsupported post-1.0.
- **Fix (docs-only):** added an authoritative **"Supported window"** definition to `maintenance.md`'s
  "Security fixes" section (the doc `SECURITY.md` points to, resolving the circular reference), and
  refreshed `SECURITY.md`'s prose + table for the 1.x era (latest `1.x` minor ✅ / older `1.x` ❌ / `0.x`
  ❌), with the general rule in prose and the concrete current state in the table. Left the "Reporting"
  and "What to expect" sections untouched (their "backport to every still-supported line" now resolves to
  a concrete window).
- **Review:** a 3-lens adversarial pass (consistency / policy-faithfulness / skeptic) — two SHIP with no
  findings, confirming the two files agree, the circular reference is resolved (`maintenance.md` now
  defines the window and `SECURITY.md` points to it one-directionally), and the encoded policy matches the
  maintainer's choice exactly (neither more nor less generous). Took the one nit: `SECURITY.md` stated
  "6 months" without the start anchor `maintenance.md` carries → added "from that release date" so the
  file consumers hit first is self-contained. Reviewer confirmed the future-tense prose + concrete
  today-only table (no prior-major row) does not confuse a reader given 1.0 is the first major.
- No `.cs` changed → `dotnet build -warnaserror` and `dotnet format` confirmed clean; 172 pass / 3 Redis
  skip (unchanged).

## State

- v1.0.0 released. Docs-only (`SECURITY.md` + `docs/workflow/maintenance.md`); no behavior/API change. PR
  drafted on `docs/security-policy-supported-window`, awaiting merge. Wording adversarially reviewed
  before drafting. CI still billing-blocked (verified locally). No local Python → `consistency_lint`
  hand-verified; touches only `SECURITY.md`, `maintenance.md`, this journal (+ index row), and the ROADMAP
  pointer — no lint invariant affected. README untouched → no i18n digest recompute.

## Next

- Docs-accuracy tier essentially drained. Next: #51/#52 (cache/validation concurrency — flakier, closer
  to tests than pure docs). The remaining `[fix]` Mediums (#31/#33/#25/#26/#62/#71/#61) carry design
  decisions → ADR + a maintainer AskUserQuestion.
