# Phase 6: データストア・ナレッジベース — 手動セットアップ手順

## 前提条件

- Phase 5 が完了していること
- Azure サブスクリプションへのアクセス権
- Phase 4 で Azure OpenAI（Embedding モデル含む）がデプロイ済み

---

## 1. Cosmos DB コンテナーの追加設定

Phase 1 で作成済みの Cosmos DB アカウントに、ナレッジストア用の設定を追加します。

### 1.1 Change Feed 用リースコンテナーの作成

```bash
az cosmosdb sql container create \
  --account-name <your-cosmos-account> \
  --resource-group <your-rg> \
  --database-name TeamsAITeammate \
  --name knowledge-leases \
  --partition-key-path /id

# knowledge コンテナーの Composite Index を追加（オプション：クエリ高速化）
az cosmosdb sql container update \
  --account-name <your-cosmos-account> \
  --resource-group <your-rg> \
  --database-name TeamsAITeammate \
  --name knowledge \
  --idx @- <<'EOF'
{
  "indexingMode": "consistent",
  "automatic": true,
  "includedPaths": [{ "path": "/*" }],
  "excludedPaths": [{ "path": "/\"_etag\"/?" }],
  "compositeIndexes": [[
    { "path": "/TenantId", "order": "ascending" },
    { "path": "/MeetingDate", "order": "descending" },
    { "path": "/Category", "order": "ascending" }
  ]]
}
EOF
```

### 1.2 Autoscale 設定（推奨）

```bash
az cosmosdb sql container throughput update \
  --account-name <your-cosmos-account> \
  --resource-group <your-rg> \
  --database-name TeamsAITeammate \
  --name knowledge \
  --max-throughput 4000
```

---

## 2. Azure AI Search のセットアップ

### 2.1 インデックスの作成

```bash
# AI Search リソースの確認
az search service show \
  --name <your-search-service> \
  --resource-group <your-rg>
```

Azure Portal → **Azure AI Search** → **インデックス** → **インデックスの追加** で以下のフィールドを設定:

| フィールド名 | 型 | Key | Searchable | Filterable | Sortable | Facetable |
| --- | --- | --- | --- | --- | --- | --- |
| `Id` | `Edm.String` | ✅ | | | | |
| `TenantId` | `Edm.String` | | | ✅ | | |
| `MeetingId` | `Edm.String` | | | ✅ | | |
| `SessionId` | `Edm.String` | | | ✅ | | |
| `Title` | `Edm.String` | | ✅ (ja.microsoft) | | | |
| `Content` | `Edm.String` | | ✅ (ja.microsoft) | | | |
| `Summary` | `Edm.String` | | ✅ | | | |
| `Category` | `Edm.String` | | | ✅ | | ✅ |
| `Status` | `Edm.String` | | | ✅ | | |
| `SourceSpeaker` | `Edm.String` | | | ✅ | | |
| `MeetingSubject` | `Edm.String` | | ✅ | | | |
| `MeetingDate` | `Edm.DateTimeOffset` | | | ✅ | ✅ | |
| `Language` | `Edm.String` | | | ✅ | | |
| `Tags` | `Collection(Edm.String)` | | ✅ | | | |
| `Confidence` | `Edm.Double` | | | | | |
| `CreatedAt` | `Edm.DateTimeOffset` | | | | ✅ | |
| `UpdatedAt` | `Edm.DateTimeOffset` | | | | | |
| `ContentVector` | `Collection(Edm.Single)` | | | | | |

### 2.2 ベクトル検索プロファイルの設定

インデックス作成時の JSON 定義（REST API または Portal）:

```json
{
  "vectorSearch": {
    "algorithms": [
      {
        "name": "knowledge-hnsw",
        "kind": "hnsw",
        "hnswParameters": {
          "metric": "cosine",
          "m": 4,
          "efConstruction": 400,
          "efSearch": 500
        }
      }
    ],
    "profiles": [
      {
        "name": "knowledge-vector-profile",
        "algorithm": "knowledge-hnsw",
        "vectorizer": null
      }
    ]
  }
}
```

`ContentVector` フィールドに対して:

- **Dimensions**: `3072`（text-embedding-3-large）
- **Vector Search Profile**: `knowledge-vector-profile`

### 2.3 セマンティック構成

```json
{
  "semantic": {
    "configurations": [
      {
        "name": "knowledge-semantic-config",
        "prioritizedFields": {
          "titleField": { "fieldName": "Title" },
          "contentFields": [
            { "fieldName": "Content" },
            { "fieldName": "Summary" }
          ]
        }
      }
    ]
  }
}
```

### 2.4 RBAC 設定

```bash
# Managed Identity に検索サービスへのアクセス権を付与
az role assignment create \
  --assignee <managed-identity-principal-id> \
  --role "Search Index Data Contributor" \
  --scope /subscriptions/<sub-id>/resourceGroups/<rg>/providers/Microsoft.Search/searchServices/<search-service>

az role assignment create \
  --assignee <managed-identity-principal-id> \
  --role "Search Service Contributor" \
  --scope /subscriptions/<sub-id>/resourceGroups/<rg>/providers/Microsoft.Search/searchServices/<search-service>
```

---

## 3. Blob Storage コンテナーの追加

ナレッジの原本保存用コンテナーを作成します。

```bash
az storage container create \
  --name knowledge \
  --account-name <your-storage-account> \
  --auth-mode login
```

---

## 4. Dataverse 環境セットアップ（Dataverse プロバイダー使用時のみ）

> **注意:** Dataverse は Power Platform 環境が必要です。CosmosDB のみ使用する場合はこのセクションをスキップしてください。

### 4.1 テーブルの作成

Power Apps Maker Portal (<https://make.powerapps.com>) で以下のテーブルを作成:

**テーブル: AI Teammate Knowledge (`cr_aiteammate_knowledge`)**

| 列名 | 表示名 | 型 | 必須 |
| --- | --- | --- | --- |
| `cr_knowledgeid` | Knowledge ID | 1行テキスト | ✅ |
| `cr_tenantid` | Tenant ID | 1行テキスト | ✅ |
| `cr_sessionid` | Session ID | 1行テキスト | |
| `cr_meetingid` | Meeting ID | 1行テキスト | |
| `cr_title` | Title | 1行テキスト | ✅ |
| `cr_content` | Content | 複数行テキスト | |
| `cr_summary` | Summary | 複数行テキスト | |
| `cr_category` | Category | 選択肢 | |
| `cr_status` | Status | 選択肢 | |
| `cr_meetingsubject` | Meeting Subject | 1行テキスト | |
| `cr_meetingdate` | Meeting Date | 日付と時刻 | |
| `cr_sourcespeaker` | Source Speaker | 1行テキスト | |
| `cr_confidence` | Confidence | 10進数 | |
| `cr_tags` | Tags | 1行テキスト | |
| `cr_language` | Language | 1行テキスト | |
| `cr_createdat` | Created At | 日付と時刻 | |
| `cr_updatedat` | Updated At | 日付と時刻 | |

**Category 選択肢の値:**

| 値 | ラベル |
| --- | --- |
| 0 | DecisionBackground |
| 1 | UndocumentedProcess |
| 2 | ExpertKnowledge |
| 3 | DiscussionHistory |
| 4 | OrganizationalContext |
| 5 | TechnicalInsight |
| 6 | LessonsLearned |
| 7 | StakeholderRelationship |
| 8 | ImplicitAssumption |
| 9 | DomainExpertise |

**Status 選択肢の値:**

| 値 | ラベル |
| --- | --- |
| 0 | Draft |
| 1 | Confirmed |
| 2 | Edited |
| 3 | Rejected |
| 4 | Archived |

### 4.2 アプリケーションユーザーの登録

1. Power Platform 管理センター → 環境 → **設定** → **ユーザー + 権限** → **アプリケーション ユーザー**
2. **新しいアプリ ユーザー** → Entra ID で登録したアプリの **App ID** を指定
3. セキュリティロール: **System Administrator** または必要最小限のカスタムロールを付与

---

## 5. SharePoint サイト セットアップ（SharePoint プロバイダー使用時のみ）

> **注意:** SharePoint は M365 テナントが必要です。CosmosDB のみ使用する場合はこのセクションをスキップしてください。

### 5.1 サイトの作成

```bash
# SharePoint サイトの作成（管理者権限が必要）
# Microsoft 365 管理センター → SharePoint → サイト → 作成
# サイト名: "AI Teammate Knowledge Base"
# テンプレート: チームサイト
```

### 5.2 リストの作成

サイト内に **Knowledge Entries** リストを作成し、以下の列を追加:

| 列名 | 型 |
| --- | --- |
| TenantId | 1行テキスト |
| SessionId | 1行テキスト |
| MeetingId | 1行テキスト |
| Content | 複数行テキスト |
| Summary | 複数行テキスト |
| Category | 選択肢 |
| Status | 選択肢 |
| SourceSpeaker | 1行テキスト |
| MeetingSubject | 1行テキスト |
| MeetingDate | 日付と時刻 |
| Confidence | 数値 |
| Tags | 1行テキスト |
| Language | 1行テキスト |

### 5.3 ドキュメントライブラリの作成

サイト内に **Knowledge Documents** ドキュメントライブラリを作成。

### 5.4 サイト ID の取得

```bash
# Graph API でサイト ID を取得
az rest --method GET \
  --url "https://graph.microsoft.com/v1.0/sites/<tenant>.sharepoint.com:/sites/AITeammateKnowledgeBase" \
  --query "id" -o tsv
```

取得した値を `appsettings.json` の `SharePoint:SiteId` に設定。

### 5.5 Graph API 権限の追加

Azure Portal → Entra ID アプリ → **API のアクセス許可** で以下を追加:

| 権限 | 種類 |
| --- | --- |
| `Sites.ReadWrite.All` | Application |

管理者の同意を与えてください。

---

## 6. アプリケーション設定

### 6.1 ローカル開発（User Secrets）

```bash
cd TeamsAITeammate/src/TeamsAITeammate.Agent

# Azure AI Search
dotnet user-secrets set "AzureAISearch:Endpoint" "https://<your-search>.search.windows.net"

# Dataverse（使用する場合のみ）
dotnet user-secrets set "Dataverse:EnvironmentUrl" "https://<your-org>.crm.dynamics.com"

# SharePoint（使用する場合のみ）
dotnet user-secrets set "SharePoint:SiteId" "<site-id>"
```

### 6.2 Azure デプロイ（環境変数）

| 設定キー | 値 | 説明 |
| --- | --- | --- |
| `AzureAISearch__Endpoint` | `https://<name>.search.windows.net` | AI Search エンドポイント |
| `AzureAISearch__IndexName` | `knowledge-index` | インデックス名 |
| `DataStore__DefaultProvider` | `CosmosDB` | デフォルトのデータストア |
| `Dataverse__EnvironmentUrl` | `https://<org>.crm.dynamics.com` | Dataverse URL（任意） |
| `SharePoint__SiteId` | `<site-id>` | SharePoint サイト ID（任意） |

---

## 7. 動作確認チェックリスト

### 7.1 基本動作

- [ ] アプリケーションが正常に起動すること
- [ ] `/healthz` エンドポイントが `Healthy` を返すこと
- [ ] 4つのデータストアプロバイダーが登録されていること（起動ログで確認）

### 7.2 CosmosDB プロバイダー

- [ ] ナレッジの保存（Save）が動作すること
- [ ] ナレッジの取得（Get）が動作すること
- [ ] テキスト検索（Search）でフィルタリングが動作すること
- [ ] セッション別取得（GetBySession）が動作すること
- [ ] テナント別統計（GetStats）が動作すること

### 7.3 ナレッジ構造化パイプライン

- [ ] TacitKnowledgeCandidate が KnowledgeEntry に変換されること
- [ ] タイトル・サマリーが LLM により自動生成されること
- [ ] タグが自動付与されること
- [ ] ベクトル埋め込みが生成されること
- [ ] 重複ナレッジが検出されスキップされること

### 7.4 Adaptive Card 連携

- [ ] 「確認」操作で Status が Confirmed に更新されること
- [ ] 「編集」操作で Status が Edited に更新され、埋め込みが再生成されること
- [ ] 「拒否」操作で Status が Rejected に更新・保存されること

### 7.5 データ同期

- [ ] Cosmos DB → AI Search への手動同期が動作すること
- [ ] Change Feed プロセッサーが起動し、新規エントリが自動同期されること

### 7.6 テナント切り替え

- [ ] デフォルト（CosmosDB）でデータが保存されること
- [ ] テナント設定を変更すると、指定されたプロバイダーが使用されること

---

## 8. Phase 6 で作成・変更されたファイル一覧

### Core（モデル・インターフェース）

| ファイル | 説明 |
| --- | --- |
| `src/TeamsAITeammate.Core/Models/KnowledgeEntry.cs` | ✏️ 拡張: MeetingId, Summary, Category, Status, Embedding 等を追加 |
| `src/TeamsAITeammate.Core/Models/KnowledgeStoreModels.cs` | 🆕 KnowledgeSearchOptions, KnowledgeStoreStats, IngestionContext |
| `src/TeamsAITeammate.Core/Interfaces/IKnowledgeStore.cs` | 🆕 プラガブルデータストア抽象化 |
| `src/TeamsAITeammate.Core/Interfaces/IKnowledgeStoreFactory.cs` | 🆕 ストアファクトリー |
| `src/TeamsAITeammate.Core/Interfaces/IEmbeddingService.cs` | 🆕 ベクトル埋め込み生成 |
| `src/TeamsAITeammate.Core/Interfaces/IKnowledgeIngestionPipeline.cs` | 🆕 ナレッジ構造化パイプライン |
| `src/TeamsAITeammate.Core/Interfaces/IDataSyncService.cs` | 🆕 データ同期サービス |

### Infrastructure（データストア実装）

| ファイル | 説明 |
| --- | --- |
| `src/TeamsAITeammate.Infrastructure/Services/CosmosKnowledgeStore.cs` | 🆕 Cosmos DB プロバイダー |
| `src/TeamsAITeammate.Infrastructure/Services/DataverseKnowledgeStore.cs` | 🆕 Dataverse Web API プロバイダー |
| `src/TeamsAITeammate.Infrastructure/Services/AzureAISearchKnowledgeStore.cs` | 🆕 AI Search + Blob Storage プロバイダー |
| `src/TeamsAITeammate.Infrastructure/Services/SharePointKnowledgeStore.cs` | 🆕 SharePoint Graph API プロバイダー |
| `src/TeamsAITeammate.Infrastructure/Services/KnowledgeStoreFactory.cs` | 🆕 Strategy パターン ファクトリー |
| `src/TeamsAITeammate.Infrastructure/Services/TenantAwareKnowledgeStoreResolver.cs` | 🆕 テナント別ストア解決 |
| `src/TeamsAITeammate.Infrastructure/Services/DataSyncService.cs` | 🆕 Change Feed 同期サービス |
| `src/TeamsAITeammate.Infrastructure/Services/CardActionHandler.cs` | ✏️ KnowledgeStatus 対応 |

### AI（分析・埋め込みサービス）

| ファイル | 説明 |
| --- | --- |
| `src/TeamsAITeammate.AI/Services/EmbeddingService.cs` | 🆕 Azure OpenAI text-embedding-3-large |
| `src/TeamsAITeammate.AI/Services/KnowledgeIngestionPipeline.cs` | 🆕 重複検出 → LLM enrichment → 埋め込み → 保存 |

### Agent（DI登録・設定）

| ファイル | 説明 |
| --- | --- |
| `src/TeamsAITeammate.Agent/Program.cs` | ✏️ Phase 6 サービス登録追加 |
| `src/TeamsAITeammate.Agent/appsettings.json` | ✏️ Dataverse, SharePoint 設定セクション追加 |

### テスト（59件追加、合計288件）

| ファイル | テスト数 | 説明 |
| --- | --- | --- |
| `tests/TeamsAITeammate.UnitTests/KnowledgeStoreModelsTests.cs` | 13 | モデル・enum のデフォルト値・record with テスト |
| `tests/TeamsAITeammate.UnitTests/KnowledgeStoreTests.cs` | 12 | 4プロバイダーの CRUD 操作テスト |
| `tests/TeamsAITeammate.UnitTests/KnowledgeStoreFactoryTests.cs` | 6 | ファクトリーの作成・フォールバック・大小文字テスト |
| `tests/TeamsAITeammate.UnitTests/TenantAwareKnowledgeStoreResolverTests.cs` | 5 | テナント別解決・デフォルト・切り替えテスト |
| `tests/TeamsAITeammate.UnitTests/KnowledgeIngestionPipelineTests.cs` | 13 | パイプライン統合・重複検出・ステータス更新テスト |
| `tests/TeamsAITeammate.UnitTests/EmbeddingServiceTests.cs` | 10 | 埋め込み生成・チャンキング・同期サービステスト |
