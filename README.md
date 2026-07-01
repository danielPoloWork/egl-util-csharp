# egl-util-csharp

> Strongly-typed DDD, Clean Architecture, CQRS and functional Result building blocks for .NET 8

[![NuGet](https://img.shields.io/nuget/v/It.D4np.Util.svg?logo=nuget&label=NuGet)](https://www.nuget.org/packages/It.D4np.Util)
[![Downloads](https://img.shields.io/nuget/dt/It.D4np.Util.svg?logo=nuget&label=downloads)](https://www.nuget.org/packages/It.D4np.Util)
[![CI](https://img.shields.io/github/actions/workflow/status/danielPoloWork/egl-util-csharp/ci.yml?branch=main&logo=github&label=CI)](https://github.com/danielPoloWork/egl-util-csharp/actions/workflows/ci.yml)
[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
![Status](https://img.shields.io/badge/Status-v1.0.0-brightgreen)

egl-util-csharp is a library written in **C# 12 / .NET 8**, built and governed to an enterprise
quality bar: a full CI matrix (Linux/Windows/macOS), static analysis with nullable-as-errors,
real-Redis integration tests, documented design decisions (ADRs), and SemVer releases.

## What it is

egl-util-csharp is a NuGet utility library of strongly-typed, immutable, extensible architectural building blocks for .NET applications that adopt Domain-Driven Design, Clean Architecture, and CQRS. It removes the boilerplate of re-implementing entities, value objects, aggregates, the Result error-handling pattern, repositories / specifications / unit-of-work, an in-memory event broker, password hashing, validation, and two-level caching, so teams focus on domain logic with compile-time safety instead of re-deriving the same scaffolding per project.

The frozen specification is in
[`docs/specs/01_spec_util.md`](docs/specs/01_spec_util.md).

## Install

The library ships as a single NuGet package bundling the domain, application, and infrastructure
layers:

```bash
dotnet add package It.D4np.Util
```

## Quick start

Functional, exception-free error handling with `Result` (illustrative):

```csharp
using It.D4np.Util.Domain;

public Result<User> Register(string email) =>
    string.IsNullOrWhiteSpace(email)
        ? new Error("User.Email.Required", "Email is required.")   // Error implicitly converts to a failure
        : Result.Success(new User(Guid.NewGuid(), email));
```

One-call dependency-injection wiring — registers the building blocks and assembly-scans for your
event handlers, FluentValidation validators, and specifications:

```csharp
using It.D4np.Util.Infrastructure;

services.AddD4npUtil(typeof(Program).Assembly);
```

## Build & test

```bash
dotnet build -c Release
dotnet test -c Release
```

- **Toolchain:** dotnet SDK (MSBuild), Directory.Build.props, xUnit (+ FsCheck for property tests), dotnet format / CSharpier, Roslyn analyzers + .editorconfig (warnings as errors).
- **Supported platforms:** Linux x64, Windows x64, macOS arm64 (.NET 8).
- Consumers import the public surface via the layer namespaces `It.D4np.Util.Domain`, `It.D4np.Util.Application`, and `It.D4np.Util.Infrastructure`.

See [`docs/development/local-build.md`](docs/development/local-build.md) for the full local setup.

## Documentation

| Document | Purpose |
|---|---|
| [`AGENTS.md`](AGENTS.md) | How AI agents (and humans) work in this repo — the contract. |
| [`docs/ROADMAP.md`](docs/ROADMAP.md) | The numbered plan and what is done. |
| [`docs/adr/`](docs/adr/) | Why it is built the way it is (Architecture Decision Records). |
| [`docs/patterns/`](docs/patterns/) | Design patterns adopted, rejected, or considered. |
| [`docs/workflow/`](docs/workflow/) | Git, documentation, release, and maintenance conventions. |
| [`CONTRIBUTING.md`](CONTRIBUTING.md) | How to propose changes and the contribution workflow. |
| [`CHANGELOG.md`](CHANGELOG.md) | User-visible changes per release. |
| [`SECURITY.md`](SECURITY.md) | How to report a vulnerability. |
| [`CODE_OF_CONDUCT.md`](CODE_OF_CONDUCT.md) | Community standards for participation. |

**Translations:** [简体中文](docs/i18n/zh/README.md) · [日本語](docs/i18n/jp/README.md)

## Milestones

| # | Title | Status |
|---|---|---|
| 1 | Project bootstrap & CI | ✅ done |
| 2 | DDD core building blocks | ✅ done |
| 3 | Functional Result error model | ✅ done |
| 4 | Persistence patterns | ✅ done |
| 5 | In-memory eventing & messaging | ✅ done |
| 6 | Security & validation | ✅ done |
| 7 | Caching & optimization | ✅ done |
| 8 | Utilities, DI, and 1.0 hardening | ✅ done |

## License

MIT © 2026 Daniel Polo. See [`LICENSE`](LICENSE).
