# Security Policy

## Supported versions

`egl-util-csharp` follows [SemVer](https://semver.org). Security fixes land on the **latest
released minor of the current major** line; when a new major ships, the **previous major stays
supported for 6 months** from that release date to give consumers a migration window. The
authoritative definition (and how
the SemVer level of a fix is assessed) lives in
[`docs/workflow/maintenance.md`](docs/workflow/maintenance.md).

`v1.0.0` has shipped, so the currently supported versions are:

| Version | Supported |
|---------|-----------|
| latest `1.x` minor | ✅ |
| older `1.x` minors | ❌ |
| `0.x` (pre-release) | ❌ |

## Reporting a vulnerability

**Do not open a public issue or PR for a security problem.** Report it privately via
[GitHub private vulnerability reporting](https://docs.github.com/code-security/security-advisories)
on this repository (**Security** tab → *Report a vulnerability*), to `danielPoloWork`.

Please include:

- the affected version(s) and platform/toolchain;
- a minimal reproduction (a failing test is ideal);
- the observed impact and, if known, the root cause.

## What to expect

1. **Acknowledgement** of the report.
2. **Triage & fix under embargo** on a private branch / draft advisory; the SemVer level of
   the fix is assessed by the decision tree in
   [`docs/workflow/maintenance.md`](docs/workflow/maintenance.md).
3. **Coordinated release**: the fix ships, then the advisory is published. The fix is
   recorded in `CHANGELOG.md` under a **Security** entry with the advisory / CVE reference.
4. **Backport** to every still-supported release line.

Thank you for reporting responsibly.
