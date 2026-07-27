# Phase 1: プロジェクト基盤構築

## 概要

Teams会議に参加し、リアルタイムで会話を分析・暗黙知を蓄積するAIエージェント「AI Teammate」のプロジェクト基盤を構築します。Microsoft 365 Agents SDKを用いたモダンエージェントアーキテクチャで、Azure Container Appsにデプロイ可能なマルチテナント対応の基盤を整備してください。

## GitHub Copilotへの指示

### 1. ソリューション構成

以下のソリューション構造で.NET 9+（最新のプレビュー含む）プロジェクトを作成してください。

```text
TeamsAITeammate/
├── src/
│   ├── TeamsAITeammate.Agent/          # M365 Agents SDK エージェント本体
│   ├── TeamsAITeammate.Core/           # ドメインロジック・共通ライブラリ
│   ├── TeamsAITeammate.Infrastructure/ # データストア・外部API連携
│   ├── TeamsAITeammate.AI/             # Azure OpenAI・AI分析エンジン
│   ├── TeamsAITeammate.SidePanel/      # React + Fluent UI v9 サイドパネルSPA
│   └── TeamsAITeammate.Admin/          # 管理画面（Blazor or React）
├── tests/
│   ├── TeamsAITeammate.UnitTests/      # xUnit単体テスト
│   ├── TeamsAITeammate.IntegrationTests/  # 結合テスト
│   ├── TeamsAITeammate.E2ETests/       # Playwright E2Eテスト
│   └── TeamsAITeammate.AIQualityTests/ # LLM出力品質テスト
├── infra/                              # Bicep IaCテンプレート
│   ├── main.bicep
│   ├── modules/
│   │   ├── container-app.bicep
│   │   ├── openai.bicep
│   │   ├── ai-search.bicep
│   │   ├── cosmos-db.bicep
│   │   ├── key-vault.bicep
│   │   ├── app-insights.bicep
│   │   └── entra-app.bicep
│   └── parameters/
│       ├── dev.bicepparam
│       ├── staging.bicepparam
│       └── prod.bicepparam
├── .github/
│   └── workflows/
│       ├── ci.yml
│       ├── cd-dev.yml
│       ├── cd-staging.yml
│       └── cd-prod.yml
├── appPackage/                         # Teamsアプリマニフェスト
│   ├── manifest.json
│   ├── color.png
│   └── outline.png
├── azure.yaml                          # azd設定ファイル
└── TeamsAITeammate.sln
```

### 2. NuGet パッケージ（最新プレビュー含む）

```xml
<!-- TeamsAITeammate.Agent -->
<PackageReference Include="Microsoft.Agents.Builder" Version="*-*" />
<PackageReference Include="Microsoft.Agents.Hosting.AspNetCore" Version="*-*" />
<PackageReference Include="Microsoft.Agents.Extensions.Teams" Version="*-*" />

<!-- TeamsAITeammate.AI -->
<PackageReference Include="Azure.AI.OpenAI" Version="*-*" />
<PackageReference Include="Microsoft.SemanticKernel" Version="*-*" />
<PackageReference Include="Microsoft.Extensions.AI" Version="*-*" />

<!-- TeamsAITeammate.Infrastructure -->
<PackageReference Include="Microsoft.Graph" Version="*-*" />
<PackageReference Include="Azure.Search.Documents" Version="*-*" />
<PackageReference Include="Microsoft.Azure.Cosmos" Version="*-*" />
<PackageReference Include="Azure.Identity" Version="*-*" />
<PackageReference Include="Azure.Storage.Blobs" Version="*-*" />

<!-- テスト -->
<PackageReference Include="xunit" Version="*" />
<PackageReference Include="Microsoft.Playwright" Version="*" />
```

> **注意**: バージョンは `*-*` で最新プレビューを取得してください。ビルド時に安定版がある場合は安定版を優先し、プレビューのみの場合はプレビューを使用する方針です。

### 3. Microsoft Entra ID アプリ登録

以下のAPI権限を持つEntra IDアプリ登録のBicepテンプレートまたはセットアップスクリプトを作成してください。

**必要なAPI権限（Application）:**

- `OnlineMeetings.ReadWrite.All` — 会議情報の読み書き
- `OnlineMeetingTranscript.Read.All` — トランスクリプト読み取り
- `Chat.ReadWrite.All` — 会議チャットへの送信
- `User.Read.All` — ユーザー情報の取得
- `CallRecords.Read.All` — 通話記録の読み取り

**必要なAPI権限（Delegated）:**

- `OnlineMeetings.ReadWrite`
- `Chat.ReadWrite`

**シングルテナント構成:**

- `signInAudience`: `AzureADMyOrg`
- Bot Channel Registrationとの連携
- OAuth2 redirect URI設定

### 4. Teams アプリマニフェスト（manifest.json）

```json
{
  "$schema": "https://developer.microsoft.com/en-us/json-schemas/teams/v1.19/MicrosoftTeams.schema.json",
  "manifestVersion": "1.19",
  "version": "1.0.0",
  "id": "{{BOT_ID}}",
  "developer": { ... },
  "name": {
    "short": "AI Teammate",
    "full": "AI Teammate - Tacit Knowledge Accumulator"
  },
  "description": {
    "short": "会議中の暗黙知を自動蓄積するAIチームメイト",
    "full": "Teams会議にAIチームメイトとして参加し、リアルタイムで会話を分析。追加質問や議題提案を行い、暗黙知をナレッジベースに自動蓄積します。"
  },
  "bots": [
    {
      "botId": "{{BOT_ID}}",
      "scopes": ["team", "personal", "groupChat"],
      "supportsFiles": false,
      "isNotificationOnly": false,
      "commandLists": [
        {
          "scopes": ["groupChat"],
          "commands": [
            { "title": "join", "description": "会議に参加してトランスクリプト分析を開始" },
            { "title": "status", "description": "現在の分析状態を表示" },
            { "title": "summarize", "description": "これまでの会話サマリーを表示" },
            { "title": "knowledge", "description": "蓄積されたナレッジを表示" },
            { "title": "settings", "description": "エージェント設定を変更" }
          ]
        }
      ]
    }
  ],
  "configurableTabs": [
    {
      "configurationUrl": "https://{{HOSTNAME}}/configure",
      "canUpdateConfiguration": true,
      "scopes": ["groupChat"],
      "meetingSurfaces": ["sidePanel"],
      "context": ["meetingSidePanel"]
    }
  ],
  "permissions": ["identity", "messageTeamMembers"],
  "validDomains": ["{{HOSTNAME}}"]
}
```

### 5. Azure Container Apps 基盤（Bicep）

`infra/main.bicep` にて以下のリソースをプロビジョニングしてください。

- **Azure Container Apps Environment** — エージェントホスティング
- **Azure Container App** — エージェントアプリ本体（min replicas: 1, max: 10, HTTP scaling rule）
- **Azure Container Registry** — イメージ管理
- **Azure OpenAI** — GPT-5.5デプロイメント（フォールバック: GPT-4.1）
- **Azure AI Search** — ナレッジベースインデックス
- **Azure Cosmos DB (NoSQL)** — セッション・ナレッジデータ
- **Azure Blob Storage** — ドキュメント・トランスクリプト保存
- **Azure Key Vault** — シークレット管理
- **Application Insights + Log Analytics** — 監視
- **Managed Identity** — サービス間認証

### 6. GitHub Actions CI パイプライン

`.github/workflows/ci.yml`:

- .NET build & test（xUnit）
- Node.js build（サイドパネルSPA）
- Bicep lint & validation
- Docker image build
- セキュリティスキャン（CodeQL推奨）

### 7. appsettings.json 構成

```json
{
  "Agents": {
    "Type": "SingleTenant",
    "MicrosoftAppId": "",
    "MicrosoftAppPassword": "",
    "MicrosoftAppTenantId": ""
  },
  "AzureOpenAI": {
    "Endpoint": "",
    "DeploymentName": "gpt-55",
    "FallbackDeploymentName": "gpt-41",
    "ApiVersion": "2025-06-01-preview"
  },
  "MeetingTranscript": {
    "Provider": "WorkIQ",
    "PollingIntervalMs": 5000,
    "FallbackProvider": "GraphAPI"
  },
  "DataStore": {
    "DefaultProvider": "CosmosDB",
    "AvailableProviders": ["Dataverse", "CosmosDB", "AzureAISearch", "SharePoint"]
  },
  "KnowledgeBase": {
    "SearchProvider": "AzureAISearch",
    "EmbeddingModel": "text-embedding-3-large",
    "ChunkSize": 1000,
    "ChunkOverlap": 200
  }
}
```

### 8. 開発環境セットアップ

以下のスクリプトを `scripts/setup-dev.ps1` および `scripts/setup-dev.sh` として作成してください。

1. .NET SDK最新版の確認
2. Node.js 22+ の確認
3. Azure CLI & azd CLI のインストール確認
4. Microsoft 365 Agents Toolkit CLI (`atk`) のインストール確認（未導入の場合は `npm install -g @microsoft/m365agentstoolkit-cli@beta`）
5. dev tunnel の設定
6. `azd init` & `azd provision`（開発環境）
7. ローカル実行用の `appsettings.Development.json` テンプレート生成

## 完了条件

- [ ] ソリューションがビルドでき、全プロジェクトの依存関係が解決される
- [ ] `azd up` でAzureリソースがプロビジョニングされる
- [ ] Teams Developer PortalにアプリがサイドロードでTインストールできる
- [ ] CI パイプラインがPRトリガーで正常に動作する
- [ ] ローカルでdev tunnelを使ったBot接続テストが通る
