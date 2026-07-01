# Changelog

All notable changes to `egl-util-csharp` are documented here, following
[Keep a Changelog 1.1.0](https://keepachangelog.com/en/1.1.0/) and
[Semantic Versioning 2.0.0](https://semver.org/).

Every PR that introduces a user-visible change adds a line to `[Unreleased]` in the same
PR. A release PR moves the `[Unreleased]` entries into a new per-version file under
`docs/changelog/v<MAJOR>/v<X.Y.Z>.md` and adds an index row below.

## [Unreleased]

### Added

- Documentation i18n (Milestone 9): the README translated into Simplified Chinese (`README.zh-CN.md`)
  and Japanese (`README.ja.md`), with a `docs/i18n/` index and a freshness-tracked translation manifest
  (English remains the source of truth; ADR-0011). Docs only — the shipped package is unchanged.

### Changed

### Deprecated

### Removed

### Fixed

### Security

---

## Released versions

| Version | Date | Notes |
|---------|------|-------|
| [v1.0.0](docs/changelog/v1/v1.0.0.md) | 2026-07-01 | First stable release; the full Milestone 1–8 surface, frozen under SemVer. |
