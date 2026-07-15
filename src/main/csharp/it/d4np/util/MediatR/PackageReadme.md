# It.D4np.Util.MediatR

Opt-in **MediatR + FluentValidation** building blocks for
[It.D4np.Util](https://www.nuget.org/packages/It.D4np.Util): the `DomainEventNotification` adapter (domain
events → MediatR notifications) and the FluentValidation `ValidationPipelineBehavior`. Depends on
`It.D4np.Util`, `MediatR`, and `FluentValidation` — the core package imposes none of these.

## Install

```bash
dotnet add package It.D4np.Util.MediatR
```

## Quick start

```csharp
using It.D4np.Util.Infrastructure;
using It.D4np.Util.MediatR;

services.AddD4npUtil(typeof(Program).Assembly)          // core services + event-handler scan
        .AddD4npUtilMediatR(typeof(Program).Assembly);  // + MediatR validation behavior & validator scan
```

- `DomainEventNotification<TEvent>` and the `DomainEventNotification.Create` factory — namespace
  `It.D4np.Util.MediatR`.
- `ValidationPipelineBehavior<TRequest, TResponse>` — namespace `It.D4np.Util.MediatR.FluentValidation`.

## Links

- [Source, documentation, and issues on GitHub](https://github.com/danielPoloWork/egl-util-csharp)
- [Changelog](https://github.com/danielPoloWork/egl-util-csharp/blob/main/CHANGELOG.md)

MIT © 2026 Daniel Polo.
