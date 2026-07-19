# POC-Teams-AITeammate

会議内でギャップを提示、会話をガイドする AI チームメイトエージェントのサンプルです。

Teams 会議にAIチームメイトとして参加し、会話をリアルタイムで分析して組織の暗黙知を自動的に蓄積します。

## アーキテクチャ

| プロジェクト | 説明 |
| --- | --- |
| `TeamsAITeammate.Agent` | ASP.NET Core Web App (M365 Agents SDK Bot) — エントリポイント |
| `TeamsAITeammate.Core` | ドメインモデル・インターフェース（外部依存なし） |
| `TeamsAITeammate.Infrastructure` | Cosmos DB、Graph API、AI Search、Blob Storage 等の実装 |
| `TeamsAITeammate.AI` | Azure OpenAI 分析エンジン、Semantic Kernel プロンプト |
| `TeamsAITeammate.SidePanel` | React 19 + Fluent UI v9 サイドパネル SPA |
| `TeamsAITeammate.Admin` | React 19 + Fluent UI v9 管理画面 SPA |

## 前提条件

- .NET SDK 10+
- Node.js 22+（SidePanel / Admin 開発用）
- Azure CLI (`az`)
- Azure Developer CLI (`azd`)
- Dev Tunnel CLI (`devtunnel`)

## クイックスタート

```bash
cd TeamsAITeammate

# ビルド
dotnet build

# ユニットテスト
dotnet test tests/TeamsAITeammate.UnitTests

# ローカル起動
dotnet run --project src/TeamsAITeammate.Agent
```

## フェーズ別セットアップ

各フェーズの手動セットアップ手順は `docs/` フォルダを参照してください:

| フェーズ | 内容 | 手順書 |
| --- | --- | --- |
| Phase 1 | プロジェクト基盤・Entra ID・CI | [phase1-manual-setup.md](docs/phase1-manual-setup.md) |
| Phase 2 | Teams Bot・会議イベント | [phase2-manual-setup.md](docs/phase2-manual-setup.md) |
| Phase 3 | リアルタイムトランスクリプト | [phase3-manual-setup.md](docs/phase3-manual-setup.md) |
| Phase 4 | AI 分析エンジン | [phase4-manual-setup.md](docs/phase4-manual-setup.md) |
| Phase 5 | エージェント介入 UI | [phase5-manual-setup.md](docs/phase5-manual-setup.md) |
| Phase 6 | データストア・ナレッジベース | [phase6-manual-setup.md](docs/phase6-manual-setup.md) |
| Phase 7 | RAG 検索・Copilot 統合 | [phase7-manual-setup.md](docs/phase7-manual-setup.md) |
| Phase 8 | 管理画面・監視・本番運用 | [phase8-manual-setup.md](docs/phase8-manual-setup.md) |

## ドキュメント

| ドキュメント | 内容 |
| --- | --- |
| [アーキテクチャ](docs/guides/architecture.md) | システム構成・レイヤー設計 |
| [デプロイガイド](docs/guides/deployment-guide.md) | Azure へのデプロイ手順 |
| [管理者ガイド](docs/guides/admin-guide.md) | エージェント設定・管理画面 |
| [ユーザーガイド](docs/guides/user-guide.md) | エンドユーザー向け操作説明 |
| [API リファレンス](docs/guides/api-reference.md) | REST API / SignalR 仕様 |
| [データモデル](docs/guides/data-model.md) | Cosmos DB コンテナー・スキーマ |
| [セキュリティ](docs/guides/security.md) | 認証・認可・データ保護 |
| [トラブルシューティング](docs/guides/troubleshooting.md) | よくある問題と対処法 |

## インフラストラクチャ

`infra/` フォルダに Bicep テンプレートがあり、`azd up` で以下をプロビジョニングします:

- Azure Container Apps
- Azure Cosmos DB
- Azure OpenAI
- Azure AI Search
- Azure Blob Storage
- Azure Key Vault
- Application Insights

## ライセンス

POC（概念実証）プロジェクトです。
