# Changelog

All notable changes to `egl-util-csharp` are documented here, following
[Keep a Changelog 1.1.0](https://keepachangelog.com/en/1.1.0/) and
[Semantic Versioning 2.0.0](https://semver.org/).

Every PR that introduces a user-visible change adds a line to `[Unreleased]` in the same
PR. A release PR moves the `[Unreleased]` entries into a new per-version file under
`docs/changelog/v<MAJOR>/v<X.Y.Z>.md` and adds an index row below.

## [Unreleased]

### Added

- Documentation i18n & README polish (Milestone 9): the README translated into Simplified Chinese
  (`docs/i18n/zh/README.md`) and Japanese (`docs/i18n/jp/README.md`), with a `docs/i18n/` index and a content-digest
  freshness manifest (English remains the source of truth; ADR-0011); plus an enterprise README refresh
  — a NuGet version/downloads/CI/.NET/License badge row, a quick-start, and a translations switcher. The
  `i18n-freshness` lint is now content-digest based (squash-merge-safe). Docs only — the shipped package
  is unchanged.
- Capture-before-save domain-event dispatch: `DbContextExtensions.CaptureDomainEvents(this DbContext)`
  returns a `DomainEventSnapshot` taken before `SaveChangesAsync`, and
  `DispatchDomainEventsAsync(this DomainEventSnapshot, …)` publishes it after the commit — preserving
  the at-least-once, clear-on-success contract while delivering events for aggregates the save removes
  (ADR-0012). Additive to the frozen 1.0 surface.

### Changed

### Deprecated

### Removed

### Fixed

- Domain events raised on an aggregate that is then removed in the same unit of work are no longer
  silently dropped (#21). EF Core detaches `Deleted` entities from the change tracker once
  `SaveChanges` completes, so the post-save `DispatchDomainEventsAsync(this DbContext, …)` overload
  could not see them; the new capture-before-save flow (`CaptureDomainEvents` + `DomainEventSnapshot`)
  delivers them. The change-tracker overload's documentation now states the limitation and points to
  the safe flow.

### Security

---

## Released versions

| Version | Date | Notes |
|---------|------|-------|
| [v1.0.0](docs/changelog/v1/v1.0.0.md) | 2026-07-01 | First stable release; the full Milestone 1–8 surface, frozen under SemVer. |
