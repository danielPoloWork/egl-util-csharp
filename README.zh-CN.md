# egl-util-csharp

> Strongly-typed DDD, Clean Architecture, CQRS and functional Result building blocks for .NET 8

> **Languages:** [English](README.md) · [简体中文](README.zh-CN.md) · [日本語](README.ja.md)

![Status](https://img.shields.io/badge/Status-v1.0.0-brightgreen)

一个使用 **C# 12 / .NET 8** 编写的类库，其构建与治理均遵循企业级质量标准：完整的 CI 矩阵、静态分析、消毒器（sanitizers）、有据可查的设计决策，以及遵循 SemVer 的版本发布。

## 它是什么

egl-util-csharp 是一个 NuGet 工具类库，为采用领域驱动设计（DDD）、整洁架构（Clean Architecture）与 CQRS 的 .NET 应用提供强类型、不可变且可扩展的架构构建块。它免去了重复实现实体、值对象、聚合、Result 错误处理模式、仓储 / 规约 / 工作单元、内存事件代理、密码哈希、验证以及两级缓存等样板代码，让团队能够在编译期安全的前提下专注于领域逻辑，而不必在每个项目中反复搭建相同的脚手架。

冻结的规范位于
[`docs/specs/01_spec_util.md`](docs/specs/01_spec_util.md)。

## 构建、测试、运行

```bash
dotnet build -c Release
dotnet test -c Release
```

- **工具链：** dotnet SDK（MSBuild）、Directory.Build.props、xUnit（+ 用于属性测试的 FsCheck）、dotnet format / CSharpier、Roslyn analyzers + .editorconfig（将警告视为错误）。
- **支持的平台：** Linux x64、Windows x64、macOS arm64（.NET 8）。
- 使用者通过分层命名空间 `It.D4np.Util.Domain`、`It.D4np.Util.Application` 和 `It.D4np.Util.Infrastructure` 引入公共接口。

## 安装

该类库以单个 NuGet 包发布，捆绑了领域层、应用层和基础设施层：

```bash
dotnet add package It.D4np.Util
```

完整的本地环境搭建请参阅 [`docs/development/local-build.md`](docs/development/local-build.md)。

## 本项目如何运作

| 文档 | 用途 |
|---|---|
| [`AGENTS.md`](AGENTS.md) | AI 代理（以及人类）在本仓库中的工作方式——即协作契约。 |
| [`ROADMAP.md`](ROADMAP.md) | 编号计划以及已完成的内容。 |
| [`docs/adr/`](docs/adr/) | 为何以这种方式构建（架构决策记录，ADR）。 |
| [`docs/patterns/`](docs/patterns/) | 已采纳、已否决或经过考量的设计模式。 |
| [`docs/workflow/`](docs/workflow/) | Git、文档、发布与维护相关的约定。 |
| [`CHANGELOG.md`](CHANGELOG.md) | 每次发布中用户可见的变更。 |
| [`SECURITY.md`](SECURITY.md) | 如何报告安全漏洞。 |

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

MIT © 2026 Daniel Polo。参见 [`LICENSE`](LICENSE)。
