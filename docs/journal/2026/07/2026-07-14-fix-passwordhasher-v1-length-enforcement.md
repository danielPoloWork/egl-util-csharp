# 2026-07-14 — Fix: enforce the V1 salt/key length in PasswordHasher.Verify (#40)

First PR of the triaged backlog roadmap (security/correctness first). A small security-correctness fix;
CHANGELOG `Fixed`, no ADR.

## Done

- **Bug:** `PasswordHasher.Verify` guarded only `salt.Length == 0 || expectedKey.Length == 0`, not equality
  with `SaltSize` (16) / `KeySize` (32), so a well-formed but off-spec hash (e.g. a 1-byte salt, an 8-byte
  key) passed the guards and was fed to PBKDF2 — the documented "V1" parameters were unenforced on the
  verify path (#40). No auth bypass, but off-spec.
- **Fix:** the guard is now `salt.Length != SaltSize || expectedKey.Length != KeySize`, so only hashes with
  the V1 scheme's fixed 16-byte salt and 32-byte key validate. Documented on the `<remarks>`. No API change.
- **Test:** off-spec salt (8B) and off-spec key (8B) hashes, each carrying a **correctly-derived** key for
  those parameters — so they would pass the pre-fix guards (a wrong-length *zero* key fails regardless and
  wouldn't distinguish the fix) — now return `false`. Mutation-verified below.
- `dotnet build -warnaserror` clean; `dotnet format` clean; PasswordHasher suite 21 pass; full suite green.

## State

- v1.0.0 released. Security-correctness fix in `PasswordHasher.cs` (+ test, CHANGELOG `Fixed`); no API
  change (PATCH). PR drafted on `fix/passwordhasher-v1-length-enforcement`, awaiting merge. CI still
  billing-blocked (verified locally). No local Python → `consistency_lint` hand-verified; no ADR, README
  untouched. First item of the backlog roadmap (see the roadmap memory).

## Next

- Roadmap PR 2: **#35 + #41** — PasswordHasher rehash-detection member + span-based plaintext. Additive API
  on the frozen `IPasswordHasher` → **AskUserQuestion** before implementing.
