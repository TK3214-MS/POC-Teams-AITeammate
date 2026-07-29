# AI Teammate デプロイ手順書

> Teams 会議内で Speech dialog、SSO、リアルタイム分析まで確認する場合は、[Teams 実機テストランブック](teams-live-test-runbook.md)を併用してください。

## 前提条件

- Azure サブスクリプション
- Azure CLI (`az`) インストール済み
- Azure Developer CLI (`azd`) インストール済み
- .NET 10 SDK
- Node.js 22+
- Docker

## 環境構成

| 環境 | ブランチ | トリガー | 承認 |
| ------ | --------- | --------- | ------ |
| Dev | `main` | 自動 (push) | なし |
| Staging | `release/*` | 自動 (push) | なし |
| Production | 手動 | `workflow_dispatch` | 必要 |

## 1. 初回セットアップ

### 1.1 Entra ID アプリ登録

```bash
# Entra IDアプリ登録（Bot用）
./scripts/setup-entra-app.sh

# 出力されるApp IDとPasswordをメモ
```

### 1.2 GitHub Secrets 設定

以下のシークレットをGitHub リポジトリに設定:

| Secret | 説明 |
| -------- | ------ |
| `AZURE_CLIENT_ID` | サービスプリンシパル Client ID |
| `AZURE_TENANT_ID` | テナント ID |
| `AZURE_SUBSCRIPTION_ID` | サブスクリプション ID |
| `BOT_APP_ID` | Bot Entra App ID |
| `BOT_APP_PASSWORD` | Bot Entra App Password |
| `DEV_APP_URL` | Dev環境のURL |
| `STAGING_APP_URL` | Staging環境のURL |

### 1.3 Azure Developer CLI でのデプロイ

```bash
cd TeamsAITeammate

# 環境初期化
azd init

# Dev環境デプロイ
azd up --environment dev

# Staging環境デプロイ
azd up --environment staging
```

## 2. Dev環境デプロイ

`main` ブランチへの push で自動実行:

1. .NET ビルド & 単体テスト
2. フロントエンドビルド（SidePanel）
3. `azd deploy` による Container Apps デプロイ
4. ヘルスチェック

## 3. Staging環境デプロイ

`release/*` ブランチへの push で自動実行:

1. 全テストスイート実行（Unit + Integration + AI Quality）
2. `azd deploy` による Container Apps デプロイ
3. E2Eテスト（Playwright）

## 4. Production環境デプロイ

手動トリガー（`workflow_dispatch`）:

1. Bicep インフラデプロイ
2. Blue-Green デプロイメント
   - 新リビジョン作成（トラフィック 0%）
   - スモークテスト
   - トラフィック切り替え: 10% → 50% → 100%
3. 旧リビジョンの非アクティブ化

### ロールバック手順

```bash
# 前のリビジョンにトラフィックを戻す
az containerapp ingress traffic set \
  --name aiteammate-prod-app \
  --resource-group rg-aiteammate-prod \
  --revision-weight "<previous-revision>=100"
```

## 5. インフラストラクチャ

### Bicep モジュール構成

```text
infra/
├── main.bicep              # メインテンプレート
├── modules/
│   ├── app-insights.bicep  # Application Insights
│   ├── container-app.bicep # Container Apps
│   ├── cosmos-db.bicep     # Cosmos DB
│   ├── openai.bicep        # Azure OpenAI
│   ├── ai-search.bicep     # Azure AI Search
│   ├── key-vault.bicep     # Key Vault
│   ├── workbook.bicep      # Monitor Workbook
│   └── alerts.bicep        # アラートルール
└── parameters/
    ├── dev.bicepparam
    ├── staging.bicepparam
    └── prod.bicepparam
```

## 6. 監視

- **Application Insights**: カスタムテレメトリ、例外追跡
- **Azure Monitor Workbook**: ダッシュボード
- **アラート**: エラー率、レイテンシ、ヘルスチェック
- **ヘルスエンドポイント**: `/healthz`
