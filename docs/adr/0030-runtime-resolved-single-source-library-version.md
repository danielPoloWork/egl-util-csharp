# ADR-0030: Resolve the library version at runtime from a single MSBuild source — breaking (2.0)

- **Status:** Accepted
- **Date:** 2026-07-14
- **Deciders:** Maintainer
- **Related:** ADR-0010 (utilities/DI/API freeze & packaging), #48, #86

## Context

The library version lived in two hand-maintained places and one of them was compile-time inlined:

- **#48 — hand-duplicated version.** The literal `1.0.0` was written both in the packable project's
  `<Version>` and in `LibraryVersion.Version`, coupled only by a manually-run `tools/consistency_lint.py`
  (and there is no local Python). A bump touching only one place ships a `LibraryVersion.Version` that
  disagrees with the NuGet package version whenever the lint is skipped.
- **#86 — `public const` inlining.** `LibraryVersion.Version` was a C# `const`, so every consumer assembly
  that reads it bakes the literal into its own IL at compile time. If the `It.D4np.Util` binaries are later
  upgraded in place without recompiling the consumer (shared-runtime deployment, plugin host, a
  transitive-dependency bump resolved at runtime), the consumer keeps reporting the **old** version while
  running the new library — exactly the mismatch the "single source of truth for the library version" type
  exists to prevent. Only the Infrastructure (packable) project set `<Version>`, so the Domain assembly that
  actually hosts `LibraryVersion` was not even stamped with the real version.

`LibraryVersion` is a public type, frozen at 1.0. Changing `Version` from `const` to a property changes its
IL access from `ldstr` (inline literal) to `ldsfld`/a getter call — a **binary-breaking** change, so it is
scheduled for the next major (2.0), consistent with the other 2.0-bound breaks (ADR-0020, ADR-0027,
ADR-0028).

## Decision

Make the version single-sourced in MSBuild and resolved at runtime:

- Move `<Version>` to `Directory.Build.props` as the **single source**, applied to every assembly. The SDK
  stamps `AssemblyVersion`/`FileVersion`/`AssemblyInformationalVersion` from it; the packable project
  inherits it as its NuGet `<Version>`. The Infrastructure csproj no longer pins a `<Version>` literal.
- Change `LibraryVersion.Version` from `public const string` to a **`public static` get-only property**
  resolved once at runtime from `typeof(LibraryVersion).Assembly`'s `AssemblyInformationalVersionAttribute`
  (trimming any `+build.metadata` the SDK appends, e.g. once SourceLink is enabled), falling back to the
  assembly version's three-part string. No hand-duplicated literal remains, and the reported value tracks
  the **loaded** binary.
- Update the `version-lockstep` lint: the version source is now `Directory.Build.props` `<Version>`; there
  is no second literal to cross-check (the package inherits it), so `package_version_file` is dropped.

## Alternatives Considered

- **Generate `Version.cs` from `$(Version)` at build, keeping `const` (#48 only).** Rejected: it removes the
  duplication but leaves the const **inlined** into consumers (#86 unfixed), and it adds throwaway build
  machinery the runtime-resolution approach then supersedes. The maintainer chose to fix both together.
- **Keep `const` and only document the inlining caveat.** Rejected — the type's whole purpose is to report
  the running library's version; documenting that it may lie defeats it.
- **`static readonly` field instead of a get-only property.** Equivalent for the (binary-breaking) inlining
  fix; a get-only property was chosen so the backing computation can evolve without a further surface change.
- **Derive from `AssemblyName.Version` (AssemblyVersion) instead of the informational version.** Kept only as
  the fallback: AssemblyVersion is four-part and is often frozen to major-only to avoid binding breaks, so it
  cannot carry a pre-release label; the informational version carries the full SemVer.

## Consequences

- **Breaking (2.0):** a consumer compiled against the 1.x `const` has `"1.0.0"` inlined and must be
  recompiled to pick up the property; `Version` can no longer be used in a `const` context (attribute
  argument, `case` label). Source-compatible for ordinary reads. Accrues toward the maintainer-assembled 2.0.
- A single version bump in `Directory.Build.props` now flows to every assembly, the NuGet package, and
  `LibraryVersion.Version` at once; the lint still cross-checks it against the README badge and the release
  files. Covered by tests: the value equals the current release, and `Version` is a property (not a `const`
  field) — a structural guard against regressing to `const`.

## References

- Issues #48, #86; ADR-0010; `Directory.Build.props`, `LibraryVersion` (`Domain/Version.cs`),
  `tools/consistency_lint.py` (version-lockstep).
