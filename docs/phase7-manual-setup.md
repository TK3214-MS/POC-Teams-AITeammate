# Phase 7 手動セットアップ手順: RAG検索・Copilot Studio統合

## 前提条件

- Phase 6 が完了していること
- Azure AI Search にナレッジインデックスが作成済み
- Copilot Studio ライセンスが利用可能

## 1. Azure AI Search セマンティック設定

### 1.1 セマンティック構成の追加

Azure Portal → AI Search → インデックス → `knowledge-index` → セマンティック構成:

```json
{
  "name": "knowledge-semantic-config",
  "prioritizedFields": {
    "titleField": { "fieldName": "Title" },
    "contentFields": [
      { "fieldName": "Content" },
      { "fieldName": "Summary" }
    ],
    "keywordsFields": [
      { "fieldName": "Tags" }
    ]
  }
}
```

### 1.2 ベクトル検索プロファイル

インデックスに以下のベクトル検索設定を追加:

```json
{
  "vectorSearch": {
    "algorithms": [
      {
        "name": "hnsw-config",
        "kind": "hnsw",
        "hnswParameters": {
          "m": 4,
          "efConstruction": 400,
          "efSearch": 500,
          "metric": "cosine"
        }
      }
    ],
    "profiles": [
      {
        "name": "vector-profile",
        "algorithm": "hnsw-config"
      }
    ]
  }
}
```

## 2. Copilot Studio カスタムコネクタ設定

### 2.1 Entra ID アプリ登録（API用）

1. Azure Portal → Microsoft Entra ID → アプリの登録 → 新規登録
2. 名前: `AI Teammate Copilot API`
3. API の公開:
   - アプリケーション ID URI: `api://<app-id>`
   - スコープ追加: `Knowledge.Read`
4. API のアクセス許可:
   - `Microsoft Graph > User.Read` (委任)

### 2.2 Power Platform カスタムコネクタ

1. Power Platform 管理センター → カスタムコネクタ → 新規
2. OpenAPI 定義をインポート（`/swagger/v1/swagger.json`）
3. 認証: OAuth 2.0 (Microsoft Entra ID)
4. ベース URL: `https://<app-domain>`

### 2.3 Copilot Studio トピック設定

`docs/copilot-studio-topics.md` の設計に従い、以下のトピックを作成:

- ナレッジ検索トピック
- 会議サマリー取得トピック
- ナレッジ閲覧トピック

## 3. Microsoft Graph Connector 設定（オプション）

### 3.1 アプリケーション権限

Entra ID アプリに以下の権限を追加:

- `ExternalConnection.ReadWrite.OwnedBy`
- `ExternalItem.ReadWrite.OwnedBy`

### 3.2 接続の作成

アプリ起動時に `KnowledgeGraphConnector.CreateConnectionAsync()` が呼び出され、
以下が自動設定されます:

- 接続 ID: `aiteammateknowledge`
- スキーマ: title, content, category, meetingSubject, meetingDate, sourceSpeaker, tags

## 4. 動作確認

### 4.1 RAG検索の確認

```bash
# ナレッジ検索API
curl -X POST https://<app-domain>/api/copilot/search \
  -H "Authorization: Bearer <token>" \
  -H "Content-Type: application/json" \
  -d '{"query": "アーキテクチャ設計", "maxResults": 5}'

# ナレッジ詳細取得
curl https://<app-domain>/api/copilot/knowledge/<id> \
  -H "Authorization: Bearer <token>"

# 統計情報
curl https://<app-domain>/api/copilot/stats \
  -H "Authorization: Bearer <token>"
```

### 4.2 テスト実行

```bash
cd TeamsAITeammate
dotnet test tests/TeamsAITeammate.UnitTests
dotnet test tests/TeamsAITeammate.AIQualityTests
```

## 5. 実装されたコンポーネント

| コンポーネント | パス | 説明 |
| --- | --- | --- |
| IKnowledgeRetriever | Core/Interfaces/ | RAG検索インターフェース |
| IKnowledgeQualityService | Core/Interfaces/ | ナレッジ品質管理 |
| IKnowledgeGraphService | Core/Interfaces/ | ナレッジグラフ |
| AzureAISearchRetriever | Infrastructure/Services/ | ハイブリッド検索実装 |
| KnowledgeQualityService | Infrastructure/Services/ | 陳腐化・矛盾検出 |
| KnowledgeGraphService | Infrastructure/Services/ | グラフ関係管理 |
| KnowledgeGraphConnector | Infrastructure/Services/ | M365 Graph Connector |
| RagEnhancedConversationAnalyzer | AI/Services/ | RAG統合分析 |
| CopilotIntegrationController | Agent/Controllers/ | Copilot Studio API |
| RagModels.cs | Core/Models/ | RAG関連モデル |
| CopilotModels.cs | Core/Models/ | Copilot連携モデル |
