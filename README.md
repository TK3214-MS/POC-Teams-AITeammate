# POC-Teams-AITeammate

会議内でギャップを提示、会話をガイドする AI チームメイトエージェントのサンプルです。

Teams 会議にAIチームメイトとして参加し、会話をリアルタイムで分析して組織の暗黙知を自動的に蓄積します。

## はじめに

新しい環境へ構築する場合は、次のガイドだけを上から順に実行してください。

### [AI Teammate 環境再現ガイド](docs/reproduction-guide.md)

ソース取得、前提確認、Entra ID、Azure、Teams Channel、アプリパッケージ、動作確認までを一つの経路にまとめています。

```text
README
  └─ 環境再現ガイド
       ├─ ローカルビルド・テスト
       ├─ Entra ID / Azure構築
       ├─ Agentデプロイ
       ├─ Teams登録
       └─ 動作確認
```

> フェーズ別手順は実装経緯や個別機能を理解するための資料です。初回構築では環境再現ガイドを優先してください。

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
- Microsoft 365 Agents Toolkit（VS Code 拡張機能）
- Microsoft 365 Agents Toolkit CLI (`atk`、任意。パッケージ検証や Teams でのテストに使用)
- Dev Tunnel CLI (`devtunnel`)

> Azure リソースのプロビジョニングとアプリのデプロイには `azd` を使用します。Microsoft 365 Agents Toolkit は Teams アプリの開発、マニフェスト検証、ローカルテストに使用します。

## ローカル検証

```bash
cd TeamsAITeammate

# 前提確認、依存関係復元、ビルド
chmod +x scripts/setup-dev.sh
./scripts/setup-dev.sh

# ユニットテスト
dotnet test tests/TeamsAITeammate.UnitTests/TeamsAITeammate.UnitTests.csproj --no-restore
```

Agentの起動にはBot、Azure OpenAI、Cosmos DBなどの設定が必要です。環境構築は[環境再現ガイド](docs/reproduction-guide.md)を参照してください。

## 目的別ドキュメント

| やりたいこと | 最初に読む資料 |
| --- | --- |
| 新しい環境へ一から構築する | [環境再現ガイド](docs/reproduction-guide.md) |
| システム構成・データフローを理解する | [エージェントアーキテクチャ](docs/guides/agent-architecture-visualization.md) |
| TeamsでAI Teammateを使う | [ユーザーガイド](docs/guides/user-guide.md) |
| 設定・ナレッジ・ユーザーを管理する | [管理者ガイド](docs/guides/admin-guide.md) |
| APIやSignalRを利用する | [APIリファレンス](docs/guides/api-reference.md) |
| 認証・認可・データ保護を確認する | [セキュリティ](docs/guides/security.md) |
| エラーを解決する | [トラブルシューティング](docs/guides/troubleshooting.md) |
| 本番運用の詳細を確認する | [本番環境セットアップ手順](docs/production-setup-guide.md) |

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

## インフラストラクチャ

`infra/` フォルダに Bicep テンプレートがあり、`azd up` で以下をプロビジョニングします:

- Azure Container Apps
- Azure Cosmos DB
- Azure OpenAI
- Azure AI Search
- Azure Key Vault
- Application Insights

Azure Blob Storage、Dataverse、SharePointは構成依存の拡張先で、現行Bicepには含まれません。

## ライセンス

POC（概念実証）プロジェクトです。
