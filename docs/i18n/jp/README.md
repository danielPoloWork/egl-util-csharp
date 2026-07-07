# egl-util-csharp

> Strongly-typed DDD, Clean Architecture, CQRS and functional Result building blocks for .NET 8

> **Languages:** [English](../../../README.md) · [简体中文](../zh/README.md) · [日本語](README.md)

[![NuGet](https://img.shields.io/nuget/v/It.D4np.Util.svg?logo=nuget&label=NuGet)](https://www.nuget.org/packages/It.D4np.Util)
[![Downloads](https://img.shields.io/nuget/dt/It.D4np.Util.svg?logo=nuget&label=downloads)](https://www.nuget.org/packages/It.D4np.Util)
[![CI](https://img.shields.io/github/actions/workflow/status/danielPoloWork/egl-util-csharp/ci.yml?branch=main&logo=github&label=CI)](https://github.com/danielPoloWork/egl-util-csharp/actions/workflows/ci.yml)
[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](../../../LICENSE)
![Status](https://img.shields.io/badge/Status-v1.0.0-brightgreen)

egl-util-csharp は **C# 12 / .NET 8** で書かれたライブラリで、エンタープライズ品質水準に沿って構築・管理されています。完全な CI マトリックス（Linux/Windows/macOS）、null 許容をエラーとして扱う静的解析、実際の Redis を用いた統合テスト、文書化された設計判断（ADR）、そして SemVer に基づくリリースを備えています。

## 概要

egl-util-csharp は、ドメイン駆動設計 (DDD)、クリーンアーキテクチャ、CQRS を採用する .NET アプリケーション向けに、強く型付けされ、イミュータブルで拡張可能なアーキテクチャ構成要素を提供する NuGet ユーティリティライブラリです。エンティティ、値オブジェクト、集約、Result エラーハンドリングパターン、リポジトリ / 仕様 / ユニットオブワーク、インメモリのイベントブローカー、パスワードハッシュ化、バリデーション、2 階層キャッシュといった要素を再実装する定型作業を排除します。これにより、チームはプロジェクトごとに同じ土台を作り直すのではなく、コンパイル時の型安全性を確保しながらドメインロジックに集中できます。

確定した仕様は
[`docs/specs/01_spec_util.md`](../../../docs/specs/01_spec_util.md) にあります。

## インストール

本ライブラリは、ドメイン層・アプリケーション層・インフラストラクチャ層をまとめた単一の NuGet パッケージとして提供されます。

```bash
dotnet add package It.D4np.Util
```

## クイックスタート

`Result` による関数型・例外を使わないエラーハンドリング（例）：

```csharp
using It.D4np.Util.Domain;

public Result<User> Register(string email) =>
    string.IsNullOrWhiteSpace(email)
        ? Result.Failure<User>(new Error("User.Email.Required", "Email is required."))
        : Result.Success(new User(Guid.NewGuid(), email));
```

1 回の呼び出しで依存性注入を構成します。構成要素を登録し、イベントハンドラー・FluentValidation バリデーターをアセンブリスキャンで登録します：

```csharp
using It.D4np.Util.Infrastructure;

services.AddD4npUtil(typeof(Program).Assembly);
```

## ビルドとテスト

```bash
dotnet build -c Release
dotnet test -c Release
```

- **ツールチェーン:** dotnet SDK (MSBuild)、Directory.Build.props、xUnit (プロパティテスト用に FsCheck を併用)、dotnet format / CSharpier、Roslyn アナライザー + .editorconfig（警告をエラーとして扱う）。
- **対応プラットフォーム:** Linux x64、Windows x64、macOS arm64 (.NET 8)。
- 利用者は、レイヤーの名前空間 `It.D4np.Util.Domain`、`It.D4np.Util.Application`、`It.D4np.Util.Infrastructure` を通じて公開 API を取り込みます。

ローカル環境の完全なセットアップ手順は [`docs/development/local-build.md`](../../../docs/development/local-build.md) を参照してください。

## ドキュメント

| ドキュメント | 目的 |
|---|---|
| [`AGENTS.md`](../../../AGENTS.md) | AI エージェント（および人間）が本リポジトリでどのように作業するか — その取り決め。 |
| [`docs/ROADMAP.md`](../../../docs/ROADMAP.md) | 番号付きの計画と完了状況。 |
| [`docs/adr/`](../../../docs/adr/) | なぜこの設計になっているのか（アーキテクチャ決定記録）。 |
| [`docs/patterns/`](../../../docs/patterns/) | 採用・却下・検討したデザインパターン。 |
| [`docs/workflow/`](../../../docs/workflow/) | Git、ドキュメント、リリース、保守に関する規約。 |
| [`CONTRIBUTING.md`](../../../CONTRIBUTING.md) | 変更の提案方法と貢献のワークフロー。 |
| [`CHANGELOG.md`](../../../CHANGELOG.md) | リリースごとのユーザーに見える変更点。 |
| [`SECURITY.md`](../../../SECURITY.md) | 脆弱性の報告方法。 |
| [`CODE_OF_CONDUCT.md`](../../../CODE_OF_CONDUCT.md) | コミュニティ参加における行動規範。 |

## マイルストーン

| # | タイトル | ステータス |
|---|---|---|
| 1 | プロジェクトの立ち上げと CI | ✅ 完了 |
| 2 | DDD コア構成要素 | ✅ 完了 |
| 3 | 関数型 Result エラーモデル | ✅ 完了 |
| 4 | 永続化パターン | ✅ 完了 |
| 5 | インメモリのイベント処理とメッセージング | ✅ 完了 |
| 6 | セキュリティとバリデーション | ✅ 完了 |
| 7 | キャッシュと最適化 | ✅ 完了 |
| 8 | ユーティリティ、DI、および 1.0 の安定化 | ✅ 完了 |

## ライセンス

MIT © 2026 Daniel Polo. [`LICENSE`](../../../LICENSE) を参照してください。
