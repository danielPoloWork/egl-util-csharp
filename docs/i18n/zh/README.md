# egl-util-csharp

> Strongly-typed DDD, Clean Architecture, CQRS and functional Result building blocks for .NET 8

> **Languages:** [English](../../../README.md) · [简体中文](README.md) · [日本語](../jp/README.md)

[![NuGet](https://img.shields.io/nuget/v/It.D4np.Util.svg?logo=nuget&label=NuGet)](https://www.nuget.org/packages/It.D4np.Util)
[![Downloads](https://img.shields.io/nuget/dt/It.D4np.Util.svg?logo=nuget&label=downloads)](https://www.nuget.org/packages/It.D4np.Util)
[![CI](https://img.shields.io/github/actions/workflow/status/danielPoloWork/egl-util-csharp/ci.yml?branch=main&logo=github&label=CI)](https://github.com/danielPoloWork/egl-util-csharp/actions/workflows/ci.yml)
[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](../../../LICENSE)
![Status](https://img.shields.io/badge/Status-v1.0.0-brightgreen)

egl-util-csharp 是一个使用 **C# 12 / .NET 8** 编写的类库，其构建与治理均遵循企业级质量标准：完整的 CI 矩阵（Linux/Windows/macOS）、以“可空即错误”为准的静态分析、真实 Redis 集成测试、有据可查的设计决策（ADR），以及遵循 SemVer 的版本发布。

## 它是什么

egl-util-csharp 是一个 NuGet 工具类库，为采用领域驱动设计（DDD）、整洁架构（Clean Architecture）与 CQRS 的 .NET 应用提供强类型、不可变且可扩展的架构构建块。它免去了重复实现实体、值对象、聚合、Result 错误处理模式、仓储 / 规约 / 工作单元、内存事件代理、密码哈希、验证以及两级缓存等样板代码，让团队能够在编译期安全的前提下专注于领域逻辑，而不必在每个项目中反复搭建相同的脚手架。

冻结的规范位于
[`docs/specs/01_spec_util.md`](../../../docs/specs/01_spec_util.md)。

## 安装

该类库以单个 NuGet 包发布，捆绑了领域层、应用层和基础设施层：

```bash
dotnet add package It.D4np.Util
```

## 快速开始

使用 `Result` 进行函数式、无异常的错误处理（示例）：

```csharp
using It.D4np.Util.Domain;

public Result<User> Register(string email) =>
    string.IsNullOrWhiteSpace(email)
        ? new Error("User.Email.Required", "Email is required.")   // Error 会隐式转换为失败结果
        : Result.Success(new User(Guid.NewGuid(), email));
```

一次调用即可完成依赖注入装配——注册这些构建块，并通过程序集扫描注册你的事件处理器、FluentValidation 验证器与规约：

```csharp
using It.D4np.Util.Infrastructure;

services.AddD4npUtil(typeof(Program).Assembly);
```

## 构建与测试

```bash
dotnet build -c Release
dotnet test -c Release
```

- **工具链：** dotnet SDK（MSBuild）、Directory.Build.props、xUnit（+ 用于属性测试的 FsCheck）、dotnet format / CSharpier、Roslyn analyzers + .editorconfig（将警告视为错误）。
- **支持的平台：** Linux x64、Windows x64、macOS arm64（.NET 8）。
- 使用者通过分层命名空间 `It.D4np.Util.Domain`、`It.D4np.Util.Application` 和 `It.D4np.Util.Infrastructure` 引入公共接口。

完整的本地环境搭建请参阅 [`docs/development/local-build.md`](../../../docs/development/local-build.md)。

## 文档

| 文档 | 用途 |
|---|---|
| [`AGENTS.md`](../../../AGENTS.md) | AI 代理（以及人类）在本仓库中的工作方式——即协作契约。 |
| [`docs/ROADMAP.md`](../../../docs/ROADMAP.md) | 编号计划以及已完成的内容。 |
| [`docs/adr/`](../../../docs/adr/) | 为何以这种方式构建（架构决策记录，ADR）。 |
| [`docs/patterns/`](../../../docs/patterns/) | 已采纳、已否决或经过考量的设计模式。 |
| [`docs/workflow/`](../../../docs/workflow/) | Git、文档、发布与维护相关的约定。 |
| [`CONTRIBUTING.md`](../../../CONTRIBUTING.md) | 如何提出变更以及贡献流程。 |
| [`CHANGELOG.md`](../../../CHANGELOG.md) | 每次发布中用户可见的变更。 |
| [`SECURITY.md`](../../../SECURITY.md) | 如何报告安全漏洞。 |
| [`CODE_OF_CONDUCT.md`](../../../CODE_OF_CONDUCT.md) | 参与社区的行为准则。 |

## 里程碑

| # | 标题 | 状态 |
|---|---|---|
| 1 | 项目引导与 CI | ✅ 已完成 |
| 2 | DDD 核心构建块 | ✅ 已完成 |
| 3 | 函数式 Result 错误模型 | ✅ 已完成 |
| 4 | 持久化模式 | ✅ 已完成 |
| 5 | 内存事件与消息传递 | ✅ 已完成 |
| 6 | 安全与验证 | ✅ 已完成 |
| 7 | 缓存与优化 | ✅ 已完成 |
| 8 | 工具集、依赖注入与 1.0 加固 | ✅ 已完成 |

## 许可证

MIT © 2026 Daniel Polo。参见 [`LICENSE`](../../../LICENSE)。
