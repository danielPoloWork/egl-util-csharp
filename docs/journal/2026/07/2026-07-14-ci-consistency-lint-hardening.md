# 2026-07-14 — CI: consistency_lint hardening + drop the deep clone (#72, #74, #82, #96)

Roadmap PR 9 (build/CI cluster). Four `tools/consistency_lint.py` fixes + one `ci.yml` tweak, same
subsystem, tight batch. CHANGELOG `Changed` (repo/CI only; shipped package unchanged); no ADR. **No local
Python** → every change hand-verified by reading the script and tracing each check against the real data
files (README/ROADMAP milestones, patterns catalogue, i18n manifest); the .NET suite is untouched by this PR.

## Done

- **#72 `check_milestones`:** the check claimed bidirectional README ↔ ROADMAP congruence but only failed
  when the README marked a milestone complete while the ROADMAP had unchecked items. Now it also fails when
  the ROADMAP has every milestone item checked but the README row is not ✅, and when a README milestone has
  no `## Milestone N` ROADMAP section at all. Reconciles over the README's milestones only, so the ROADMAP
  may still carry milestones the README table omits (e.g. Milestone 9, post-1.0 doc work).
- **#74 `check_i18n_freshness`:** it hashed only the English-source link and **never checked the translation
  file existed**, and silently skipped any row lacking a literal `` `translated` ``. Now it parses both link
  columns, verifies the translation file exists (any status), and fails on an unrecognised status cell
  (vocabulary `translated`/`outdated`) instead of skipping it — so a deleted translation or a reformatted
  status cell fails loudly. Freshness (digest) still applies to `translated` rows.
- **#82 `check_patterns`:** it required an existing `src/…` code location for **every** Implemented/Planned
  row, contradicting the catalogue's own vocabulary (`Planned` = "decided in an ADR, not yet landed"). Now it
  selects on the Status **cell** (not a whole-row substring), requires the code location only for
  `Implemented`, and for `Planned` tolerates an absent path while still rejecting a *cited* path that does
  not exist. Latent today (all 13 rows Implemented) but unblocks the documented Planned workflow.
- **#96:** the `git()` subprocess helper had zero call sites (dead code) and the consistency CI job's
  `fetch-depth: 0` was justified by a false premise ("i18n/version checks need full history" — the i18n
  check is content-digest based and no check reads history). Removed the helper (and its now-unused
  `import subprocess`) and dropped `fetch-depth: 0` (default shallow checkout), speeding every consistency
  run.
- **Verification (no runner):** re-read the whole edited script; grepped for leftover `git()`/`subprocess`
  references (none); traced each edited check against the current data — README milestones 1–8 all ✅ with
  all-checked ROADMAP sections (M9 ROADMAP-only, not cross-checked); patterns' 13 numbered rows all
  `Implemented` with existing ADR+code (Rejected/Superseded carry only `| — |` placeholders); i18n manifest's
  two `translated` rows have existing translations and a source digest that still matches
  (`a2639f36acf8bcfe`). All checks PASS on the current repo. `dotnet build -warnaserror` + full suite still
  222 pass / 3 Redis skip (no C# changed).

## State

- v1.0.0 released. The consistency lint now enforces what its docstring promised (bidirectional milestones,
  i18n translation existence + status vocabulary, Planned-aware pattern code-location) and the CI job clones
  shallow. CHANGELOG `Changed`; no ADR. PR drafted on `ci/consistency-lint-hardening`, awaiting merge. CI
  billing-blocked and no local Python → lint hand-verified by tracing. README untouched (no i18n churn).

## Next

- Roadmap PR 10: **#48 + #86** — version hand-duplication (the `Version.cs` constant vs the csproj
  `<Version>`) and public-`const` inlining (switch consumer-visible `public const` to `static readonly` so a
  version/number bump doesn't require recompiling consumers). Tight, both small; #86 may touch the frozen
  surface (const → static readonly is a binary-compat consideration) → assess and possibly AskUserQuestion.
