# egl-util-csharp

> Strongly-typed DDD, Clean Architecture, CQRS and functional Result building blocks for .NET 8

![Status](https://img.shields.io/badge/Status-v0.0.0-blue)

A
library written in **C# 12 / .NET 8**, built and governed to an enterprise quality
bar: full CI matrix, static analysis, sanitizers, documented design decisions, and SemVer
releases.

## What it is

egl-util-csharp is a NuGet utility library of strongly-typed, immutable, extensible architectural building blocks for .NET applications that adopt Domain-Driven Design, Clean Architecture, and CQRS. It removes the boilerplate of re-implementing entities, value objects, aggregates, the Result error-handling pattern, repositories / specifications / unit-of-work, an in-memory event broker, password hashing, validation, and two-level caching, so teams focus on domain logic with compile-time safety instead of re-deriving the same scaffolding per project.

The frozen specification is in
[`docs/specs/01_spec_util.md`](docs/specs/01_spec_util.md).

## Build, test, run

```bash
dotnet build -c Release
dotnet test -c Release
```

- **Toolchain:** dotnet SDK (MSBuild), Directory.Build.props, xUnit (+ FsCheck for property tests), dotnet format / CSharpier, Roslyn analyzers + .editorconfig (warnings as errors).
- **Supported platforms:** Linux x64, Windows x64, macOS arm64 (.NET 8).
- Consumers import the public surface via: `using It.D4np.Util;`.

See [`docs/development/local-build.md`](docs/development/local-build.md) for the full local
setup.

## How this project is run

| Document | Purpose |
|---|---|
| [`AGENTS.md`](AGENTS.md) | How AI agents (and humans) work in this repo — the contract. |
| [`ROADMAP.md`](ROADMAP.md) | The numbered plan and what is done. |
| [`docs/adr/`](docs/adr/) | Why it is built the way it is (Architecture Decision Records). |
| [`docs/patterns/`](docs/patterns/) | Design patterns adopted, rejected, or considered. |
| [`docs/workflow/`](docs/workflow/) | Git, documentation, release, and maintenance conventions. |
| [`CHANGELOG.md`](CHANGELOG.md) | User-visible changes per release. |
| [`SECURITY.md`](SECURITY.md) | How to report a vulnerability. |

## Milestones

| # | Title | Status |
|---|---|---|
| 1 | Project bootstrap & CI | ✅ done |
| 2 | DDD core building blocks | ✅ done |
| 3 | Functional Result error model | ✅ done |
| 4 | Persistence patterns | ✅ done |
| 5 | In-memory eventing & messaging | ✅ done |
| 6 | Security & validation | ⏳ planned |
| 7 | Caching & optimization | ⏳ planned |
| 8 | Utilities, DI, and 1.0 hardening | ⏳ planned |

## License

MIT © 2026 Daniel Polo. See [`LICENSE`](LICENSE).
