# Phase 8: 管理画面・監視・テスト・本番化

## 概要

管理者向け設定画面の構築、Microsoft 365 Agents SDKによるエージェント登録・監視、Application Insightsによる包括的な監視体制の整備、全テストレベルの完成、本番デプロイパイプラインの構築を行います。

## 前提条件

- Phase 1〜7 が完了していること
- Application Insightsリソースがプロビジョニング済み
- GitHub Actions のシークレットが設定済み

## GitHub Copilotへの指示

### 1. 管理者向け設定画面

管理者がAI Teammateの動作を制御する管理画面を `TeamsAITeammate.Admin` プロジェクトに実装してください。Teams タブアプリとして表示可能にしてください。

**技術スタック:**
- React + Fluent UI v9 + TypeScript（サイドパネルと共通の技術スタック）
- バックエンド: ASP.NET Core Web API

**管理画面の機能:**

**1a. ダッシュボード**
- テナント全体のナレッジ蓄積統計（日次/週次/月次）
- 会議セッション数・分析実行数
- カテゴリ別ナレッジ分布
- アクティブユーザー数
- AI利用コスト（トークン消費量）

**1b. エージェント設定**
```typescript
interface AgentSettings {
  // 介入設定
  intervention: {
    frequency: 'low' | 'medium' | 'high';  // 介入頻度
    silenceThresholdSeconds: number;         // 沈黙検知閾値
    maxInterventionsPerMeeting: number;      // 1会議あたり最大介入回数
    enableProactiveIntervention: boolean;    // プロアクティブ介入の有効/無効
    cooldownSeconds: number;                 // 介入間隔
  };
  
  // 質問生成設定
  questionGeneration: {
    enabledCategories: QuestionType[];       // 有効な質問カテゴリ
    maxQuestionsPerIntervention: number;     // 1回の介入あたり最大質問数
    priorityThreshold: QuestionPriority;     // 投稿する質問の最低優先度
  };
  
  // データストア設定
  dataStore: {
    primaryProvider: 'Dataverse' | 'CosmosDB' | 'AzureAISearch' | 'SharePoint';
    enableRAG: boolean;
    ragMinRelevanceScore: number;
  };
  
  // 言語設定
  language: {
    autoDetect: boolean;
    preferredLanguage: string;  // BCP-47
    supportedLanguages: string[];
  };
  
  // 対象会議フィルター
  meetingFilter: {
    includeAllMeetings: boolean;
    includedOrganizers: string[];     // 特定オーガナイザーの会議のみ
    excludedMeetingPatterns: string[]; // 除外パターン（例: "1on1"）
    minimumParticipants: number;      // 最小参加者数
  };
}
```

**1c. ナレッジ管理**
- ナレッジ一覧（検索・フィルター・ソート）
- ナレッジ詳細表示・編集
- ナレッジの手動追加
- バルク操作（エクスポート、アーカイブ、削除）
- ナレッジ品質レポート（陳腐化警告、矛盾検出結果）

**1d. ユーザー管理**
- テナント内のユーザー一覧
- ユーザーごとの利用統計
- 権限設定（管理者/ユーザー/閲覧のみ）

### 2. M365 Agents SDK エージェント登録

Microsoft 365 Agents SDKを使用したエージェント登録・管理の手順とコードを実装してください。

```csharp
// エージェント宣言型定義（agent.json or code-first）
public class AITeammateAgentDefinition
{
    // エージェントのメタデータ
    public string Name => "AI Teammate";
    public string Description => "Teams会議の暗黙知を自動蓄積するAIチームメイト";
    
    // 能力の宣言
    public AgentCapabilities Capabilities => new()
    {
        SupportsTeamsMeetings = true,
        SupportsGroupChat = true,
        SupportsAdaptiveCards = true,
        SupportsFileAttachments = false,
        SupportsProactiveMessaging = true
    };
}
```

**エージェント登録手順を以下のスクリプト/ドキュメントとして出力:**

1. Entra IDアプリ登録の自動化スクリプト
2. Bot Channel Registration の設定
3. Teams Developer Portal でのアプリ公開
4. 組織内アプリカタログへの公開手順
5. マルチテナント同意フロー（Admin Consent）の設定

### 3. 監視・オブザーバビリティ

**3a. Application Insights テレメトリ**

```csharp
// カスタムテレメトリの実装
public class AITeammateTelementry
{
    // カスタムメトリクス
    public void TrackAnalysisExecution(string sessionId, TimeSpan duration, 
        int topicsDetected, int questionsGenerated, int knowledgeExtracted);
    
    public void TrackTranscriptProcessing(string sessionId, int segmentCount,
        TimeSpan processingTime);
    
    public void TrackUserInteraction(string sessionId, string actionType,
        string cardType);
    
    public void TrackAIModelUsage(string modelName, int promptTokens, 
        int completionTokens, TimeSpan latency);
    
    public void TrackKnowledgeIngestion(string tenantId, string category,
        string storeProvider);
    
    // カスタムイベント
    public void TrackMeetingJoined(string meetingId, string tenantId, 
        int participantCount);
    public void TrackMeetingLeft(string meetingId, TimeSpan sessionDuration,
        int totalInterventions, int totalKnowledgeEntries);
    
    // 例外トラッキング
    public void TrackTranscriptError(string provider, Exception ex);
    public void TrackAIModelError(string model, Exception ex);
}
```

**3b. ヘルスチェック**

```csharp
// Liveness / Readiness プローブ
services.AddHealthChecks()
    .AddCheck<AzureOpenAIHealthCheck>("azure-openai")
    .AddCheck<CosmosDBHealthCheck>("cosmos-db")
    .AddCheck<AzureAISearchHealthCheck>("ai-search")
    .AddCheck<GraphAPIHealthCheck>("graph-api")
    .AddCheck<TranscriptProviderHealthCheck>("transcript-provider");
```

**3c. Azure Monitor ダッシュボード（Workbooks）**

以下のメトリクスを表示するAzure Monitor Workbookテンプレート（ARM/Bicep）を作成してください:

- 会議セッション数（日次推移）
- AI分析実行数とレイテンシ
- 質問生成数とユーザー回答率
- ナレッジ蓄積数（カテゴリ別）
- AIモデルトークン消費量とコスト
- エラー率とエラー種別
- ユーザーアクティビティ

**3d. アラート設定**

```bicep
// 以下のアラートルールをBicepで定義:
// - エラー率 > 5% → 警告
// - AI分析レイテンシ > 30秒 → 警告
// - トランスクリプト取得エラー連続3回 → 重大
// - Azure OpenAI 429エラー多発 → 警告
// - ヘルスチェック失敗 → 重大
```

### 4. テスト完成

**4a. 単体テスト（xUnit）**

全プロジェクトのカバレッジ目標: **80%以上**

```csharp
// テストプロジェクト構成
TeamsAITeammate.UnitTests/
├── Agent/
│   ├── AITeammateActivityHandlerTests.cs
│   ├── CommandParserTests.cs
│   └── MeetingSessionManagerTests.cs
├── AI/
│   ├── ConversationAnalyzerTests.cs
│   ├── QuestionGeneratorTests.cs
│   └── TacitKnowledgeExtractorTests.cs
├── Infrastructure/
│   ├── CosmosKnowledgeStoreTests.cs
│   ├── DataverseKnowledgeStoreTests.cs
│   ├── AzureAISearchRetrieverTests.cs
│   └── GraphTranscriptProviderTests.cs
└── Core/
    ├── InterventionOrchestratorTests.cs
    ├── NotificationThrottlerTests.cs
    └── TranscriptBufferTests.cs
```

**4b. 結合テスト**

```csharp
TeamsAITeammate.IntegrationTests/
├── TranscriptPipelineIntegrationTests.cs
├── KnowledgeIngestionIntegrationTests.cs
├── RAGSearchIntegrationTests.cs
├── AdaptiveCardFlowIntegrationTests.cs
└── MultiTenantIsolationTests.cs  // テナント間のデータ分離確認
```

**4c. E2Eテスト（Playwright）**

```typescript
// サイドパネルのE2Eテスト
TeamsAITeammate.E2ETests/
├── side-panel/
│   ├── dashboard.spec.ts       // ダッシュボード表示テスト
│   ├── knowledge-list.spec.ts  // ナレッジ一覧テスト
│   └── settings.spec.ts       // 設定変更テスト
├── admin/
│   ├── admin-dashboard.spec.ts
│   ├── agent-settings.spec.ts
│   └── knowledge-management.spec.ts
└── fixtures/
    └── test-data.ts
```

**4d. AI品質テスト**

```csharp
TeamsAITeammate.AIQualityTests/
├── QuestionRelevanceTests.cs     // 質問の関連性評価
├── QuestionDiversityTests.cs     // 質問の多様性評価
├── KnowledgeCategoryAccuracyTests.cs  // カテゴリ分類精度
├── MultiLanguageQualityTests.cs  // 多言語品質
├── RAGRetrievalQualityTests.cs   // RAG検索精度
└── ConflictDetectionTests.cs     // 矛盾検出精度

// 評価メトリクス:
// - Relevance: 質問が会話文脈に関連しているか（目標: 85%+）
// - Diversity: 質問が多様な観点をカバーしているか（目標: 80%+）
// - Category Accuracy: 暗黙知カテゴリの正確性（目標: 80%+）
// - RAG Precision@5: RAG検索の上位5件の適合率（目標: 75%+）
```

### 5. GitHub Actions CDパイプライン

**5a. Dev環境デプロイ (`cd-dev.yml`)**

```yaml
# トリガー: main ブランチへのpush
# ステップ:
# 1. .NET build & test
# 2. Node.js build（SidePanel & Admin）
# 3. Docker image build & push to ACR
# 4. Bicep deployment (dev parameters)
# 5. Azure Container Apps deployment
# 6. Smoke test
```

**5b. Staging環境デプロイ (`cd-staging.yml`)**

```yaml
# トリガー: release/* ブランチへのpush
# ステップ:
# 1. Full test suite (unit + integration + AI quality)
# 2. Docker image build & push to ACR
# 3. Bicep deployment (staging parameters)
# 4. Azure Container Apps deployment
# 5. E2E test (Playwright)
# 6. 手動承認ゲート
```

**5c. Production環境デプロイ (`cd-prod.yml`)**

```yaml
# トリガー: 手動 (workflow_dispatch) + staging承認後
# ステップ:
# 1. Production Bicep deployment
# 2. Blue-Green deployment to Container Apps
# 3. Smoke test on new revision
# 4. Traffic switching (10% → 50% → 100%)
# 5. Rollback plan（前リビジョンへの自動切り戻し条件）
```

### 6. セキュリティ

以下のセキュリティ要件を実装・検証してください:

- **マルチテナントデータ分離**: テナントIDによる全データアクセスの分離
- **API認証**: Bearer Token（Entra ID）による全APIエンドポイントの保護
- **シークレット管理**: Azure Key Vault経由、コード内ハードコーディング禁止
- **データ暗号化**: 保存時暗号化（Azure既定）+ 転送時TLS
- **RBAC**: 管理者/ユーザー/閲覧者の3段階権限
- **監査ログ**: 全管理操作のログ記録
- **入力バリデーション**: Adaptive Cardユーザー入力のサニタイズ
- **レートリミット**: API エンドポイントのレートリミット

### 7. 本番運用ドキュメント

以下のドキュメントを `docs/` ディレクトリに生成してください:

```
docs/
├── architecture.md          # アーキテクチャ概要図（Mermaid）
├── deployment-guide.md      # デプロイ手順書
├── admin-guide.md           # 管理者ガイド
├── user-guide.md            # ユーザーガイド
├── troubleshooting.md       # トラブルシューティング
├── api-reference.md         # API リファレンス
├── data-model.md            # データモデル定義
└── security.md              # セキュリティ設計書
```

### 8. Teams ストア公開準備

Teamsアプリストア（組織内カタログ or パブリック）への公開に必要な以下を準備してください:

- アプリアイコン（192x192 color, 32x32 outline）
- プライバシーポリシーURL用テンプレート
- 利用規約URL用テンプレート
- アプリ説明文（短文・長文）
- スクリーンショット用のモックアップ指示

## 完了条件

- [ ] 管理画面から全設定が変更でき、エージェント動作に即座に反映される
- [ ] M365 Agents SDK でエージェントが正しく登録・認識される
- [ ] Application Insightsで全カスタムテレメトリが収集される
- [ ] Azure Monitor Workbookでダッシュボードが表示される
- [ ] アラートが正しく発火する（テスト用のエラー注入で確認）
- [ ] 単体テストカバレッジ80%以上
- [ ] 結合テスト・E2Eテストがグリーン
- [ ] AI品質テストが全メトリクスで目標値を達成
- [ ] GitHub ActionsでDev→Staging→Prodのパイプラインが動作する
- [ ] Blue-Greenデプロイメントが正常に動作する
- [ ] マルチテナントデータ分離がテストで検証済み
- [ ] セキュリティチェックリストが全項目クリア
- [ ] 全運用ドキュメントが完成している
- [ ] Teamsアプリストア公開に必要なアセットが揃っている
