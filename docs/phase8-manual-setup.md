# Phase 8 手動セットアップ手順

## 現在の状態

- **ビルド**: ✅ 成功 (0 エラー、3 警告)
- **ユニットテスト**: ✅ 410 件パス
- **AI品質テスト**: ✅ 2 件パス、34 件スキップ (Azure OpenAI 接続が必要なテストは Skip)
- **E2Eテスト**: Admin Panel 起動後に実行可能 (Skip 設定)

---

## 1. Entra ID アプリ登録

Phase 8 で追加した Admin API を保護するため、既存の Entra アプリ登録にスコープを追加します。

### 手順

1. [Azure Portal](https://portal.azure.com) → Microsoft Entra ID → アプリの登録
2. 既存の `AI Teammate Bot` アプリを選択
3. **API の公開** → スコープの追加:
   - スコープ名: `Admin.ReadWrite`
   - 同意できるユーザー: 管理者のみ
   - 管理者の同意の表示名: `AI Teammate 管理画面アクセス`
4. **アプリのロール** → ロールの追加:
   - 表示名: `AI Teammate Admin`
   - 許可されるメンバー: ユーザー/グループ
   - 値: `Admin`
5. **認証** → リダイレクト URI に Admin Panel の URL を追加:
   - `https://<container-app-url>/admin`
   - 開発時: `http://localhost:5174`

---

## 2. Azure リソースの追加設定

### 2.1 Cosmos DB コンテナ追加

Phase 8 で追加した3つのコンテナを作成します。

```bash
# Azure CLI でコンテナ作成
ACCOUNT_NAME="<cosmos-account-name>"
DB_NAME="ai-teammate"
RG="<resource-group>"

# 設定コンテナ
az cosmosdb sql container create \
  --account-name $ACCOUNT_NAME \
  --database-name $DB_NAME \
  --resource-group $RG \
  --name settings \
  --partition-key-path /TenantId

# ユーザーコンテナ
az cosmosdb sql container create \
  --account-name $ACCOUNT_NAME \
  --database-name $DB_NAME \
  --resource-group $RG \
  --name users \
  --partition-key-path /TenantId

# 監査ログコンテナ
az cosmosdb sql container create \
  --account-name $ACCOUNT_NAME \
  --database-name $DB_NAME \
  --resource-group $RG \
  --name audit-logs \
  --partition-key-path /TenantId
```

> **Note**: `azd up` で Bicep をデプロイすれば自動作成されます。手動で先に作る場合のみ実行してください。

### 2.2 Application Insights 接続文字列

`appsettings.json` の `ApplicationInsights:ConnectionString` に Application Insights の接続文字列を設定します。

```bash
# 接続文字列の取得
az monitor app-insights component show \
  --app <app-insights-name> \
  --resource-group $RG \
  --query connectionString -o tsv
```

Container Apps の環境変数として設定:

```bash
az containerapp update \
  --name <app-name> \
  --resource-group $RG \
  --set-env-vars "ApplicationInsights__ConnectionString=<connection-string>"
```

### 2.3 Azure Monitor ワークブック・アラート

Bicep デプロイで自動作成されます (`infra/modules/workbook.bicep`, `infra/modules/alerts.bicep`)。

`azd up` を実行するか、個別にデプロイ:

```bash
az deployment group create \
  --resource-group $RG \
  --template-file infra/main.bicep \
  --parameters infra/parameters/dev.bicepparam
```

---

## 3. Admin Panel (React) のビルド・デプロイ

### ローカル開発

```bash
cd src/TeamsAITeammate.Admin
npm install
npm run dev
# http://localhost:5174 で起動
```

### プロダクションビルド

```bash
cd src/TeamsAITeammate.Admin
npm install
npm run build
# dist/ に静的ファイルが出力される
```

### デプロイオプション

#### オプション A: Container App に同梱

`Dockerfile` の最終ステージで Admin Panel の `dist/` を ASP.NET の `wwwroot/admin/` にコピーし、静的ファイルとしてサーブ。

#### オプション B: Azure Static Web Apps

Admin Panel を独立した Azure Static Web Apps にデプロイ。CORS 設定で API エンドポイントを許可。

---

## 4. GitHub Actions CD パイプライン

`.github/workflows/` に3つのパイプラインが作成済みです:

| ファイル | トリガー | 内容 |
| --------- | --------- | ------ |
| `cd-dev.yml` | `main` ブランチ push | ビルド・テスト → dev 環境デプロイ → スモークテスト |
| `cd-staging.yml` | dev デプロイ成功後 | フルテストスイート → staging デプロイ → E2E テスト |
| `cd-prod.yml` | 手動承認 | インフラデプロイ → ブルーグリーンデプロイ (10%→50%→100%) |

### 必要な GitHub Secrets

GitHub リポジトリの Settings → Secrets and variables → Actions に以下を設定:

| Secret 名 | 説明 |
| ----------- | ------ |
| `AZURE_CREDENTIALS` | Azure サービスプリンシパルの JSON |
| `ACR_LOGIN_SERVER` | Azure Container Registry のログインサーバー |
| `ACR_USERNAME` | ACR ユーザー名 |
| `ACR_PASSWORD` | ACR パスワード |
| `CONTAINER_APP_NAME_DEV` | dev 環境の Container App 名 |
| `CONTAINER_APP_NAME_STAGING` | staging 環境の Container App 名 |
| `CONTAINER_APP_NAME_PROD` | prod 環境の Container App 名 |
| `RESOURCE_GROUP_DEV` | dev リソースグループ名 |
| `RESOURCE_GROUP_STAGING` | staging リソースグループ名 |
| `RESOURCE_GROUP_PROD` | prod リソースグループ名 |

### サービスプリンシパルの作成

```bash
az ad sp create-for-rbac \
  --name "github-ai-teammate-cd" \
  --role contributor \
  --scopes /subscriptions/<subscription-id>/resourceGroups/<rg> \
  --sdk-auth
```

出力された JSON を `AZURE_CREDENTIALS` Secret に設定。

---

## 5. Teams アプリマニフェストの更新

### Admin タブの追加

`appPackage/manifest.json` に Admin タブを追加:

```json
{
  "staticTabs": [
    {
      "entityId": "admin",
      "name": "AI Teammate 管理",
      "contentUrl": "https://<app-url>/admin",
      "websiteUrl": "https://<app-url>/admin",
      "scopes": ["personal"]
    }
  ]
}
```

### Teams Developer Portal でアプリを更新

1. [Teams Developer Portal](https://dev.teams.microsoft.com/) にアクセス
2. アプリを選択 → App features → Personal Tab → 追加
3. アプリパッケージを更新して再公開

---

## 6. Teams ストア申請準備

Teams ストアに申請する場合、以下を準備:

### 必要なドキュメント

- [ ] プライバシーポリシー (URL)
- [ ] 利用規約 (URL)
- [ ] アプリの詳細説明 (英語/日本語)
- [ ] スクリーンショット (1280x720 以上、最低3枚)
- [ ] アプリアイコン (192x192 カラー、32x32 アウトライン)

### マニフェスト更新項目

```json
{
  "developer": {
    "privacyUrl": "https://<your-domain>/privacy",
    "termsOfUseUrl": "https://<your-domain>/terms",
    "websiteUrl": "https://<your-domain>"
  },
  "description": {
    "short": "会議の暗黙知を自動蓄積する AI チームメイト",
    "full": "AI Teammate は Teams 会議にAIチームメイトとして参加し、会話をリアルタイムで分析して組織の暗黙知を自動的に蓄積します。..."
  }
}
```

---

## 7. セキュリティチェックリスト

デプロイ前に以下を確認:

- [ ] Entra ID 認証が全 API エンドポイントで有効
- [ ] テナント分離 (TenantId によるフィルタリング) が全データアクセスで適用
- [ ] レート制限が設定済み
- [ ] CORS が適切に制限 (許可オリジンのみ)
- [ ] Application Insights の接続文字列が設定済み
- [ ] Key Vault にシークレットが保存済み
- [ ] Managed Identity のロール割り当てが完了
- [ ] ヘルスチェックエンドポイント (`/healthz`) が応答を返す
- [ ] Container Apps のイングレスが HTTPS のみに設定

---

## 8. 開発時の確認コマンド

```bash
# ビルド確認
cd TeamsAITeammate
dotnet build

# ユニットテスト実行
dotnet test tests/TeamsAITeammate.UnitTests

# AI品質テスト実行 (Azure OpenAI 接続が必要)
dotnet test tests/TeamsAITeammate.AIQualityTests

# Admin Panel 起動
cd src/TeamsAITeammate.Admin
npm install && npm run dev

# ヘルスチェック確認
curl https://localhost:5001/healthz
```

---

## Phase 8 で作成・変更されたファイル一覧

### 新規作成ファイル

| パス | 説明 |
| ------ | ------ |
| `src/TeamsAITeammate.Core/Models/AdminModels.cs` | 管理画面用モデル (AgentSettings, DashboardStats等) |
| `src/TeamsAITeammate.Core/Interfaces/IAdminServices.cs` | 管理系インターフェース |
| `src/TeamsAITeammate.Core/Interfaces/IAITeammateTelemetry.cs` | テレメトリインターフェース |
| `src/TeamsAITeammate.Infrastructure/Services/AITeammateTelemetry.cs` | Application Insights 実装 |
| `src/TeamsAITeammate.Infrastructure/Services/HealthChecks.cs` | 5種ヘルスチェック |
| `src/TeamsAITeammate.Infrastructure/Services/AdminServices.cs` | 管理系サービス実装 |
| `src/TeamsAITeammate.Agent/Controllers/AdminController.cs` | Admin REST API |
| `src/TeamsAITeammate.Admin/` | React 管理画面 (一式) |
| `infra/modules/workbook.bicep` | Azure Monitor ワークブック |
| `infra/modules/alerts.bicep` | アラートルール |
| `infra/workbook-template.json` | KQL ダッシュボードテンプレート |
| `tests/**/AITeammateTelemetryTests.cs` | テレメトリテスト |
| `tests/**/HealthCheckTests.cs` | ヘルスチェックテスト |
| `tests/**/AdminModelsTests.cs` | モデルテスト |
| `tests/**/AdminControllerTests.cs` | コントローラーテスト |
| `tests/**/DashboardServiceTests.cs` | ダッシュボードテスト |
| `tests/IntegrationTests/IntegrationTests.cs` | 統合テスト |
| `tests/AIQualityTests/` | AI品質テスト (5ファイル) |
| `tests/E2ETests/AdminPanelTests.cs` | E2E テスト |
| `.github/workflows/cd-dev.yml` | dev CD パイプライン |
| `.github/workflows/cd-staging.yml` | staging CD パイプライン |
| `.github/workflows/cd-prod.yml` | prod CD パイプライン |
| `docs/architecture.md` | アーキテクチャ文書 |
| `docs/deployment-guide.md` | デプロイガイド |
| `docs/admin-guide.md` | 管理者ガイド |
| `docs/user-guide.md` | ユーザーガイド |
| `docs/troubleshooting.md` | トラブルシューティング |
| `docs/api-reference.md` | API リファレンス |
| `docs/data-model.md` | データモデル定義 |
| `docs/security.md` | セキュリティ文書 |

### 変更ファイル

| パス | 変更内容 |
| ------ | --------- |
| `src/TeamsAITeammate.Core/Interfaces/IKnowledgeRepository.cs` | GetByIdAsync, GetByTenantAsync, DeleteAsync 追加 |
| `src/TeamsAITeammate.Infrastructure/Repositories/CosmosKnowledgeRepository.cs` | 新メソッド実装 |
| `src/TeamsAITeammate.Infrastructure/TeamsAITeammate.Infrastructure.csproj` | App Insights, HealthChecks, Http パッケージ追加 |
| `src/TeamsAITeammate.Agent/TeamsAITeammate.Agent.csproj` | App Insights.AspNetCore, RateLimit パッケージ追加 |
| `src/TeamsAITeammate.Agent/Program.cs` | Phase 8 サービス登録、ヘルスチェック追加 |
| `src/TeamsAITeammate.Agent/appsettings.json` | ApplicationInsights セクション追加 |
| `infra/main.bicep` | workbook, alerts モジュール追加 |
| `tests/**/TeamsAITeammate.UnitTests.csproj` | App Insights パッケージ追加 |
| `tests/**/TeamsAITeammate.IntegrationTests.csproj` | MVC Testing, Moq 追加 |
