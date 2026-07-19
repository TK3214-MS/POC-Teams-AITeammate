# Phase 7: RAG検索・Copilot Studio統合

## 概要

蓄積されたナレッジベースをAzure AI SearchによるRAG（Retrieval-Augmented Generation）で活用し、会議中の分析精度を向上させます。さらにCopilot Studio経由でM365 Copilotからもナレッジを検索・活用できるように統合します。

## 前提条件

- Phase 6 が完了していること
- Azure AI Search にナレッジインデックスが作成済み
- Copilot Studio ライセンスが利用可能

## GitHub Copilotへの指示

### 1. RAG パイプライン

会議中のAI分析時に、過去のナレッジベースから関連知識を検索・注入するRAGパイプラインを実装してください。

```csharp
public interface IKnowledgeRetriever
{
    // 会話コンテキストに基づくナレッジ検索
    Task<IReadOnlyList<RelevantKnowledge>> RetrieveAsync(
        RetrievalQuery query, 
        CancellationToken ct);
}

public record RetrievalQuery
{
    public string QueryText { get; init; }
    public string TenantId { get; init; }
    public RetrievalStrategy Strategy { get; init; }
    public int MaxResults { get; init; } = 10;
    public float MinRelevanceScore { get; init; } = 0.7f;
    
    // フィルター条件
    public IReadOnlyList<TacitKnowledgeCategory>? CategoryFilter { get; init; }
    public DateTimeOffset? DateFrom { get; init; }
    public DateTimeOffset? DateTo { get; init; }
    public IReadOnlyList<string>? TagFilter { get; init; }
}

public enum RetrievalStrategy
{
    HybridSearch,      // ベクトル + キーワード（推奨）
    VectorOnly,        // ベクトル検索のみ
    KeywordOnly,       // キーワード検索のみ
    SemanticRanking    // セマンティックランキング
}

public record RelevantKnowledge
{
    public KnowledgeEntry Entry { get; init; }
    public float RelevanceScore { get; init; }
    public string MatchHighlight { get; init; }  // 検索ハイライト
    public RetrievalSource Source { get; init; }
}
```

### 2. Azure AI Search ハイブリッド検索実装

```csharp
public class AzureAISearchRetriever : IKnowledgeRetriever
{
    public async Task<IReadOnlyList<RelevantKnowledge>> RetrieveAsync(
        RetrievalQuery query, CancellationToken ct)
    {
        // 1. クエリテキストのベクトル化（text-embedding-3-large）
        // 2. ハイブリッド検索の実行
        //    - ベクトル検索: ContentVector フィールド
        //    - キーワード検索: Title, Content, Summary, Tags
        //    - セマンティックランキング: 有効化
        // 3. テナントフィルター適用（セキュリティ境界）
        // 4. 結果のスコアリングとランキング
        // 5. RelevantKnowledge への変換
    }
}

// 検索プロファイル設定:
// - vectorSearch:
//   - algorithm: HNSW (m=4, efConstruction=400, efSearch=500)
//   - metric: cosine
// - semantic:
//   - configuration: knowledge-semantic-config
//   - prioritizedFields: title > content > summary
```

### 3. RAG統合 — 会議分析への注入

Phase 4のConversationAnalyzerにRAGを統合してください。

```csharp
public class RagEnhancedConversationAnalyzer : IConversationAnalyzer
{
    public async Task<ConversationAnalysis> AnalyzeAsync(
        ConversationWindow conversation,
        AnalysisContext context,
        CancellationToken ct)
    {
        // 1. 会話の要約・キーワードを抽出
        // 2. IKnowledgeRetriever で関連ナレッジを検索
        // 3. 検索結果を AnalysisContext.PriorKnowledge に注入
        // 4. プロンプトに過去ナレッジを含めて分析実行
        //    → 「過去の会議で{knowledge}という知見がありました。
        //       これに関連する追加の質問や議論ポイントはありますか？」
        // 5. 過去ナレッジとの矛盾検出
        //    → 「前回の会議では{old_knowledge}でしたが、
        //       今回の議論では{new_info}と異なります。確認が必要かもしれません。」
    }
}
```

### 4. ナレッジグラフ（オプション）

ナレッジ間の関連性をグラフ構造で管理するオプション機能を実装してください。

```csharp
public interface IKnowledgeGraphService
{
    // ナレッジ間のリレーション追加
    Task AddRelationAsync(string sourceId, string targetId, 
        RelationType type, CancellationToken ct);
    
    // 関連ナレッジのトラバーサル
    Task<IReadOnlyList<KnowledgeEntry>> GetRelatedAsync(
        string knowledgeId, int depth, CancellationToken ct);
    
    // トピッククラスターの検出
    Task<IReadOnlyList<KnowledgeCluster>> DetectClustersAsync(
        string tenantId, CancellationToken ct);
}

public enum RelationType
{
    RelatedTo,      // 関連
    DerivedFrom,    // 派生
    Contradicts,    // 矛盾
    Supersedes,     // 上書き
    Supports,       // 補強
    DependsOn       // 依存
}
```

### 5. Copilot Studio 統合

Copilot Studio から AI Teammate のナレッジベースを検索・活用するためのコネクタを実装してください。

**5a. カスタムコネクタ（Power Platform）:**

```csharp
// REST APIエンドポイントの公開
// Copilot Studio のカスタムコネクタとして登録

[ApiController]
[Route("api/copilot")]
public class CopilotIntegrationController : ControllerBase
{
    // ナレッジ検索API（Copilot Studioから呼び出し）
    [HttpPost("search")]
    public async Task<ActionResult<CopilotSearchResponse>> SearchKnowledge(
        [FromBody] CopilotSearchRequest request)
    {
        // 1. テナントID の検証（認証トークンから）
        // 2. RAG検索の実行
        // 3. Copilot Studio が解釈可能な形式で返却
    }
    
    // ナレッジ詳細取得API
    [HttpGet("knowledge/{id}")]
    public async Task<ActionResult<KnowledgeEntry>> GetKnowledge(string id);
    
    // ナレッジ統計API
    [HttpGet("stats")]
    public async Task<ActionResult<KnowledgeStoreStats>> GetStats();
}

// OpenAPI仕様（Copilot Studio用）の自動生成
// Swagger/OpenAPI 3.0 でエンドポイントをドキュメント化
```

**5b. Copilot Studio トピック設計:**

Copilot Studio側で設定する以下のトピックの設計ドキュメントを生成してください:

1. **ナレッジ検索トピック**
   - トリガー: 「〜について知りたい」「〜の背景は？」
   - アクション: カスタムコネクタ経由でRAG検索
   - 応答: 関連ナレッジのサマリーと出典

2. **会議サマリー取得トピック**
   - トリガー: 「先週の会議のまとめ」「〜プロジェクトの会議サマリー」
   - アクション: セッション検索API呼び出し
   - 応答: サマリーカード

3. **ナレッジ閲覧トピック**
   - トリガー: 「最近蓄積されたナレッジ」「〜カテゴリのナレッジ」
   - アクション: ナレッジ一覧API呼び出し
   - 応答: ナレッジリスト

### 6. Microsoft Graph Connectors（オプション）

蓄積ナレッジをMicrosoft 365検索に表示するためのGraph Connectorを実装してください。

```csharp
public class KnowledgeGraphConnector
{
    // 外部接続の作成
    public async Task CreateConnectionAsync(CancellationToken ct)
    {
        // connectionId: "aiteammateknowledge"
        // name: "AI Teammate Knowledge Base"
        // description: "Teams会議から自動抽出された暗黙知ナレッジベース"
    }
    
    // スキーマ定義
    public async Task CreateSchemaAsync(CancellationToken ct)
    {
        // properties: title, content, category, meetingSubject, 
        //             meetingDate, sourceSpeaker, tags
        // labels: title → title, content → body
    }
    
    // アイテムのインジェスト
    public async Task IngestItemAsync(KnowledgeEntry entry, CancellationToken ct)
    {
        // ExternalItem として登録
        // ACL: テナント内の全ユーザーに読み取り権限
    }
}
```

### 7. ナレッジ品質管理

時間経過によるナレッジの陳腐化や、矛盾するナレッジの検出・管理機能を実装してください。

```csharp
public interface IKnowledgeQualityService
{
    // 陳腐化チェック（一定期間更新されていないナレッジを検出）
    Task<IReadOnlyList<KnowledgeEntry>> DetectStaleKnowledgeAsync(
        string tenantId, TimeSpan staleThreshold, CancellationToken ct);
    
    // 矛盾検出（新しいナレッジが既存ナレッジと矛盾する場合）
    Task<IReadOnlyList<KnowledgeConflict>> DetectConflictsAsync(
        KnowledgeEntry newEntry, CancellationToken ct);
    
    // ナレッジの統合提案（類似ナレッジのマージ）
    Task<IReadOnlyList<MergeSuggestion>> SuggestMergesAsync(
        string tenantId, CancellationToken ct);
}
```

### 8. 単体テスト・結合テスト

- `AzureAISearchRetrieverTests` — ハイブリッド検索、フィルタリング、スコアリング
- `RagEnhancedConversationAnalyzerTests` — RAG注入後の分析精度
- `CopilotIntegrationControllerTests` — API エンドポイントテスト
- `KnowledgeQualityServiceTests` — 陳腐化・矛盾検出テスト
- `KnowledgeGraphConnectorTests` — Graph Connectorの操作テスト
- AI品質テスト: RAGあり/なしでの分析品質比較

## 完了条件

- [ ] 会議中の分析時に過去ナレッジがRAGで検索・注入される
- [ ] ハイブリッド検索（ベクトル + キーワード + セマンティック）が正しく動作する
- [ ] Copilot Studio カスタムコネクタ経由でナレッジ検索が可能
- [ ] Copilot Studio トピックの設計ドキュメントが完成している
- [ ] 過去ナレッジとの矛盾が検出された場合、ユーザーに通知される
- [ ] ナレッジの陳腐化検出が動作する
- [ ] RAGあり/なしでの分析品質がAI品質テストで比較検証されている
- [ ] 全テストがグリーンである
