# 2026-07-08 — Test: pin PaginatedList navigation boundaries (#56)

Second from the zero-production-risk [test] tier. Pure test-coverage — no production change, so no
CHANGELOG entry and no ADR.

## Done

- **Gap:** the constructor accepts an overrun `pageIndex > TotalPages`, and `HasNext`/`HasPrevious` are
  derived (`PageIndex < TotalPages` / `PageIndex > 1`), but no test pinned the navigation flags at the
  boundaries (#56).
- **Tests (3):**
  - `Overrun_page_index_beyond_the_last_page_has_a_previous_but_no_next` — `pageIndex=5` past
    `TotalPages=4`: `HasPrevious` true, `HasNext` false.
  - `Last_page_on_an_exact_multiple_has_no_next` — `totalCount=9`, `pageSize=3` (exact multiple),
    `pageIndex=3=TotalPages`: `HasNext` false. Guards an off-by-one (`<` vs `<=`).
  - `Empty_result_beyond_the_first_page_has_a_previous_but_no_next` — `totalCount=0`
    (`TotalPages=0`), `pageIndex=2`: `HasPrevious` true, `HasNext` false (the derivation follows purely
    from `PageIndex`).
- **Meaningfulness verified by mutation:** flipping `HasNext` to `PageIndex <= TotalPages` makes the
  exact-multiple test fail; the real source passes — a genuine off-by-one guard. `dotnet build
  -warnaserror` clean; `dotnet format` clean; 161 pass / 2 Redis skip.

## State

- v1.0.0 released. Test-only → no shipped change. PR drafted on `test/paginatedlist-boundaries`, awaiting
  merge. CI still billing-blocked (jobs don't start; verified locally). No local Python →
  `consistency_lint` hand-verified; touches only a test file, this journal (+ index row), and the ROADMAP
  checkpoint pointer — no lint invariant affected.

## Next

- Continue zero-risk [test]/[docs] safest-first: #54 (ValueObject equality property tests generate only
  equal instances — strengthen the generators), #51/#52 (cache/validation concurrency coverage), or
  docs-accuracy (#42/#45/#46/#94/#98). The remaining `[fix]` Mediums (#31/#33/#25/#26/#62/#71/#61) carry
  design decisions → ADR + a maintainer AskUserQuestion. (Note: #54 could reveal a real ValueObject
  equality bug, which would expand it beyond test-only.)
