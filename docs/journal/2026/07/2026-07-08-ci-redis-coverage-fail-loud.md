# 2026-07-08 — CI: fail (not skip) the Redis integration tests when Docker is expected (#100)

Tenth from the zero-production-risk tier; a [ci]/test-infra reliability fix. Touches the CI workflow, so
it carries a CHANGELOG *Changed* entry (repo/CI change only), but no ADR (a modest reliability tweak, not
a foundational gate like the locked-mode/coverage ADRs).

## Done

- **Problem:** `RedisCacheIntegrationTests.InitializeAsync` wrapped the Testcontainers start in
  `catch (Exception)` and skipped on ANY failure — Docker absent, a bad image tag, a Testcontainers
  regression, a registry hiccup all looked identical. On the ubuntu CI leg (where Docker IS present) the
  only real-Redis coverage could therefore silently stop running while every pipeline stayed green (#100).
- **Fix:** a `SkipOrFailIfNoRedis()` helper — when the container did not start it **fails** if
  `REQUIRE_DOCKER=1` (throwing with the captured startup exception as inner), otherwise **skips** (the
  no-Docker case). The catch now captures the exception instead of discarding it. `ci.yml`'s build-matrix
  Test step sets `REQUIRE_DOCKER: ${{ matrix.os == 'ubuntu-24.04' && '1' || '' }}`, so the Redis tests are
  required on the Docker-capable leg and still skip on windows/macOS (and local dev).
- **Verified BOTH paths locally** (no Docker here): with no env var the three Redis tests skip cleanly
  (172 pass / 3 skip); with `REQUIRE_DOCKER=1` they **fail** with the clear "container did not start"
  message — proving strict mode. `dotnet build -warnaserror` clean; `dotnet format` clean.
- **Caught a bug during verification:** the initial `replace_all` also rewrote the `Skip.IfNot` line
  *inside* the new helper, making it call itself → stack overflow → "Test Run Aborted". The local run
  surfaced it immediately; fixed the helper's final line back to the real `Skip.IfNot`, then re-verified
  both paths. (Lesson: a blanket replace_all can hit the very code you just added — scope it or re-read.)

## State

- v1.0.0 released. Repo/CI change; shipped package unchanged. PR drafted on `ci/redis-coverage-fail-loud`,
  awaiting merge. CI still billing-blocked (the ubuntu leg won't actually exercise this until billing is
  cleared), but the strict-mode mechanism is proven locally. No local Python → `consistency_lint`
  hand-verified; touches `ci.yml`, a test file, `CHANGELOG.md`, this journal (+ index row), and the ROADMAP
  pointer — no lint invariant affected.

## Next

- Continue safest-first: docs-accuracy items (#45 ValueObject collection-equality note, #46 AggregateRoot
  thread-confinement, #42 cross-node L1 staleness, #94 SECURITY.md, #98 IDomainEvent DateTimeKind), then
  #51/#52 (cache/validation concurrency). The remaining `[fix]` Mediums (#31/#33/#25/#26/#62/#71/#61) carry
  design decisions → ADR + a maintainer AskUserQuestion.
