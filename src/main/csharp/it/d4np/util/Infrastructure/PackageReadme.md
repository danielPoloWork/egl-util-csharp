# It.D4np.Util

Strongly-typed **DDD, Clean Architecture, CQRS, and functional-Result** building blocks for **.NET 8 / C# 12** —
entities, value objects, aggregates, the `Result` / `Error` model, repository / specification / unit-of-work, an
in-memory event broker, PBKDF2 password hashing, and a two-level cache. The Domain, Application, and
Infrastructure layers ship in this one package, with **no MediatR or FluentValidation dependency**.

## Install

```bash
dotnet add package It.D4np.Util
```

MediatR / FluentValidation integration (the `DomainEventNotification` adapter and the FluentValidation
`ValidationPipelineBehavior`) is the opt-in
[It.D4np.Util.MediatR](https://www.nuget.org/packages/It.D4np.Util.MediatR) package.

## Quick start

Functional, exception-free error handling with `Result`:

```csharp
using It.D4np.Util.Domain;

public Result<User> Register(string email) =>
    string.IsNullOrWhiteSpace(email)
        ? Result.Failure<User>(new Error("User.Email.Required", "Email is required."))
        : Result.Success(new User(Guid.NewGuid(), email));
```

One-call dependency-injection wiring (registers the building blocks and assembly-scans your event handlers):

```csharp
using It.D4np.Util.Infrastructure;

services.AddD4npUtil(typeof(Program).Assembly);
```

Consumers import via the layer namespaces `It.D4np.Util.Domain`, `It.D4np.Util.Application`, and
`It.D4np.Util.Infrastructure`.

## Links

- [Source, documentation, and issues on GitHub](https://github.com/danielPoloWork/egl-util-csharp)
- [Changelog](https://github.com/danielPoloWork/egl-util-csharp/blob/main/CHANGELOG.md)
- [Architecture Decision Records](https://github.com/danielPoloWork/egl-util-csharp/tree/main/docs/adr)

MIT © 2026 Daniel Polo.
