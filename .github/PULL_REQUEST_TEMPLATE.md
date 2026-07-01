## Summary

One or two sentences: what changes and why it matters.

## Motivation

Link to the spec section, ADR, roadmap item, or issue that prompted this work.

## Changes

- bulleted list of meaningful changes (not a file list)

## Design Patterns

- list every pattern adopted/refined/rejected in this PR, with a one-line rationale and a
  link to the ADR.
- if none, write "None — straightforward implementation."

## Verification

- [ ] Builds cleanly on the full CI matrix (Linux x64, Windows x64, macOS arm64 (.NET 8))
- [ ] Unit tests pass; new/changed behavior covered (≥ 80% line)
- [ ] `dotnet format / CSharpier` clean; `Roslyn analyzers + .editorconfig (warnings as errors)` clean on the diff
- [ ] Nullable reference types + Roslyn analyzers (soundness); dotnet-counters (leaks) green (where applicable)
- [ ] `python tools/consistency_lint.py` passes

## Documentation Impact

- [ ] README.md updated (if user-facing surface changed)
- [ ] docs/ROADMAP.md checkbox flipped
- [ ] ADR added/updated (if a non-trivial design decision was made)
- [ ] docs/patterns/README.md updated (if a pattern was introduced, refined, or rejected)
- [ ] Spec updated (if behavior diverges from `docs/specs/`)
- [ ] CHANGELOG.md updated (for user-visible changes)
- [ ] PR metadata set — assignee, one type label, release milestone
