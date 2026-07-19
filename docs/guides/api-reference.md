# AI Teammate API リファレンス

## Base URL

```
https://<app-url>/api
```

## 認証

全APIエンドポイントは Bearer Token (Entra ID) で保護されています。

```http
Authorization: Bearer <access-token>
```

テナントIDはトークンのクレーム `tenantid` から自動取得されます。

---

## Copilot Integration API

### GET /api/copilot/search

RAGナレッジ検索

**Query Parameters:**
| パラメータ | 型 | 必須 | 説明 |
|-----------|-----|------|------|
| query | string | Yes | 検索クエリ |
| top | int | No | 最大結果数 (default: 5) |

**Response:** `CopilotSearchResponse`

### GET /api/copilot/knowledge/{id}

ナレッジエントリの取得

### GET /api/copilot/stats

テナント統計の取得

---

## Admin API

### GET /api/admin/dashboard

ダッシュボード統計

**Response:**
```json
{
  "tenantId": "string",
  "totalKnowledgeEntries": 0,
  "totalMeetingSessions": 0,
  "totalAnalysisExecutions": 0,
  "activeUsers": 0,
  "knowledgeByCategory": { "ExpertiseSkill": 10 },
  "aiCost": {
    "totalPromptTokens": 0,
    "totalCompletionTokens": 0,
    "estimatedCostUsd": 0.0
  }
}
```

### GET /api/admin/settings

エージェント設定の取得

### PUT /api/admin/settings

エージェント設定の更新

**Request Body:** `AgentSettings`

### GET /api/admin/knowledge

ナレッジ一覧の取得

**Query Parameters:**
| パラメータ | 型 | 説明 |
|-----------|-----|------|
| query | string | 検索クエリ |
| limit | int | 最大件数 (default: 50) |

### GET /api/admin/knowledge/{id}

ナレッジエントリの取得

### POST /api/admin/knowledge

ナレッジエントリの作成

### PUT /api/admin/knowledge/{id}

ナレッジエントリの更新

### DELETE /api/admin/knowledge/{id}

ナレッジエントリの削除

### GET /api/admin/users

テナントユーザー一覧の取得

### PUT /api/admin/users/{userId}/role

ユーザー権限の更新

**Request Body:**
```json
{ "role": "Admin" | "User" | "Viewer" }
```

### GET /api/admin/audit-logs

監査ログの取得

**Query Parameters:**
| パラメータ | 型 | 説明 |
|-----------|-----|------|
| from | DateTimeOffset | 開始日時 |
| to | DateTimeOffset | 終了日時 |
| limit | int | 最大件数 (default: 100) |

---

## SignalR Hub

### Endpoint: /hubs/meeting-analysis

**Client → Server:**
- `JoinMeeting(sessionId)` — 会議セッションに参加
- `LeaveMeeting(sessionId)` — 会議セッションから退出

**Server → Client:**
- `AnalysisUpdate(analysis)` — 分析結果の更新
- `QuestionGenerated(questions)` — 質問生成通知
- `KnowledgeExtracted(knowledge)` — ナレッジ抽出通知

---

## Health Check

### GET /healthz

ヘルスチェックエンドポイント

**Checks:**
- `azure-openai` — Azure OpenAI接続
- `cosmos-db` — Cosmos DB接続
- `ai-search` — Azure AI Search接続
- `graph-api` — Graph API設定
- `transcript-provider` — トランスクリプトプロバイダー
