# Phase 6: データストア・ナレッジベース

## 概要

ユーザーが選択可能な4種類のデータストア（Dataverse、Azure Cosmos DB、Azure AI Search + Blob Storage、SharePoint）へのプラガブルなデータ永続化レイヤーと、暗黙知をナレッジベースとして構造化・保存する機能を構築します。

## 前提条件

- Phase 5 が完了していること
- 各データストアのAzureリソースがプロビジョニング済み（Phase 1のBicep）
- Dataverse環境が利用可能（Power Platform環境）

## GitHub Copilotへの指示

### 1. プラガブルデータストア抽象化レイヤー

ユーザーが管理画面からデータ保存先を選択できるよう、Strategy パターンでデータストアを抽象化してください。

```csharp
public interface IKnowledgeStore
{
    string ProviderName { get; }
    
    // ナレッジの保存
    Task<string> SaveKnowledgeAsync(KnowledgeEntry entry, CancellationToken ct);
    
    // ナレッジの更新
    Task UpdateKnowledgeAsync(string id, KnowledgeEntry entry, CancellationToken ct);
    
    // ナレッジの取得
    Task<KnowledgeEntry?> GetKnowledgeAsync(string id, CancellationToken ct);
    
    // ナレッジの検索（フルテキスト）
    Task<IReadOnlyList<KnowledgeEntry>> SearchAsync(
        string query, 
        KnowledgeSearchOptions options,
        CancellationToken ct);
    
    // ナレッジの削除
    Task DeleteKnowledgeAsync(string id, CancellationToken ct);
    
    // 会議セッション別のナレッジ一覧
    Task<IReadOnlyList<KnowledgeEntry>> GetBySessionAsync(
        string sessionId, 
        CancellationToken ct);
    
    // テナント別の統計情報
    Task<KnowledgeStoreStats> GetStatsAsync(string tenantId, CancellationToken ct);
}

public record KnowledgeEntry
{
    public string Id { get; init; }
    public string TenantId { get; init; }
    public string MeetingId { get; init; }
    public string SessionId { get; init; }
    
    // ナレッジ内容
    public string Title { get; init; }
    public string Content { get; init; }
    public string Summary { get; init; }
    public TacitKnowledgeCategory Category { get; init; }
    
    // メタデータ
    public string SourceSpeaker { get; init; }
    public string SourceTranscriptSegmentId { get; init; }
    public string MeetingSubject { get; init; }
    public DateTimeOffset MeetingDate { get; init; }
    public IReadOnlyList<string> Participants { get; init; }
    
    // 分類・タグ
    public IReadOnlyList<string> Tags { get; init; }
    public IReadOnlyList<string> RelatedTopics { get; init; }
    public string Language { get; init; }
    
    // 品質メタデータ
    public float Confidence { get; init; }
    public KnowledgeStatus Status { get; init; }
    public string? ValidatedBy { get; init; }
    public DateTimeOffset? ValidatedAt { get; init; }
    
    // ベクトル埋め込み（AI Search用）
    public float[]? Embedding { get; init; }
    
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }
}

public enum KnowledgeStatus
{
    Draft,       // AI抽出直後
    Confirmed,   // ユーザーが確認済み
    Edited,      // ユーザーが編集済み
    Rejected,    // ユーザーが拒否
    Archived     // アーカイブ済み
}
```

### 2. データストアファクトリー

```csharp
public interface IKnowledgeStoreFactory
{
    IKnowledgeStore CreateStore(string providerName);
    IReadOnlyList<string> GetAvailableProviders();
}

// テナント設定に基づいてデータストアを選択
public class TenantAwareKnowledgeStoreResolver
{
    public async Task<IKnowledgeStore> ResolveAsync(string tenantId, CancellationToken ct)
    {
        // テナント設定テーブルから保存先を取得
        // デフォルト: CosmosDB
    }
}
```

### 3. Azure Cosmos DB プロバイダー

`CosmosKnowledgeStore` を実装してください。

```csharp
// データベース: TeamsAITeammate
// コンテナー構成:
// - knowledge: パーティションキー = /tenantId
// - sessions: パーティションキー = /tenantId
// - analytics: パーティションキー = /tenantId

// Cosmos DB 設計指針:
// - Change Feed で他のデータストアとの同期をサポート
// - TTL設定で古いセッションデータの自動削除
// - Composite Index: tenantId + meetingDate + category
// - RU/s: Autoscale (400-4000)
```

### 4. Dataverse プロバイダー

`DataverseKnowledgeStore` を実装してください。

```csharp
// Dataverse テーブル設計:
// - cr_aiteammate_knowledge (ナレッジエントリ)
//   - cr_knowledgeid (GUID, PK)
//   - cr_tenantid (string)
//   - cr_title (string)
//   - cr_content (multiline text)
//   - cr_category (optionset)
//   - cr_status (optionset)
//   - cr_meetingsubject (string)
//   - cr_meetingdate (datetime)
//   - cr_sourcespeaker (string)
//   - cr_confidence (decimal)
//   - cr_tags (string, comma-separated)
//   - cr_language (string)

// - cr_aiteammate_session (会議セッション)
// - cr_aiteammate_setting (テナント設定)

// Dataverse Web API を使用
// 認証: Managed Identity → Dataverse App User
```

### 5. Azure AI Search + Blob Storage プロバイダー

`AzureAISearchKnowledgeStore` を実装してください。

```csharp
// Blob Storage:
// - コンテナー: knowledge
// - パス: {tenantId}/{category}/{id}.json
// - ナレッジの原本を保存

// Azure AI Search インデックス:
// インデックス名: knowledge-index
// フィールド構成:
public class KnowledgeSearchIndex
{
    [SimpleField(IsKey = true)]
    public string Id { get; set; }
    
    [SimpleField(IsFilterable = true)]
    public string TenantId { get; set; }
    
    [SearchableField(AnalyzerName = "ja.microsoft")]
    public string Title { get; set; }
    
    [SearchableField(AnalyzerName = "ja.microsoft")]
    public string Content { get; set; }
    
    [SearchableField]
    public string Summary { get; set; }
    
    [SimpleField(IsFilterable = true, IsFacetable = true)]
    public string Category { get; set; }
    
    [SimpleField(IsFilterable = true)]
    public string Status { get; set; }
    
    [SimpleField(IsFilterable = true, IsSortable = true)]
    public DateTimeOffset MeetingDate { get; set; }
    
    [SimpleField(IsFilterable = true)]
    public string Language { get; set; }
    
    [SearchableField]
    public string[] Tags { get; set; }
    
    // ベクトルフィールド（text-embedding-3-large: 3072次元）
    [VectorSearchField(
        VectorSearchDimensions = 3072,
        VectorSearchProfileName = "knowledge-vector-profile")]
    public float[] ContentVector { get; set; }
}

// セマンティック構成:
// - SemanticConfiguration: title, content, summary をセマンティックフィールドに設定
// - ベクトル検索: HNSW アルゴリズム
```

### 6. SharePoint プロバイダー

`SharePointKnowledgeStore` を実装してください。

```csharp
// SharePoint サイト構成:
// - サイト: AI Teammate Knowledge Base
// - リスト: Knowledge Entries
// - ドキュメントライブラリ: Knowledge Documents

// Graph API を使用した SharePoint 操作:
// - リストアイテムの CRUD
// - ドキュメントライブラリへのファイルアップロード
// - メタデータ列の管理

// ナレッジをSharePointページとしてもエクスポート可能に
```

### 7. ベクトル埋め込み生成サービス

```csharp
public interface IEmbeddingService
{
    Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken ct);
    Task<IReadOnlyList<float[]>> GenerateEmbeddingsAsync(
        IReadOnlyList<string> texts, CancellationToken ct);
}

// Azure OpenAI text-embedding-3-large モデルを使用
// チャンキング戦略:
// - ナレッジエントリのTitle + Content + Summary を結合
// - 長文の場合はチャンク分割（1000トークン、200トークンオーバーラップ）
// - 各チャンクにメタデータを付与
```

### 8. ナレッジ構造化パイプライン

AI分析結果からナレッジエントリを生成するパイプラインを実装してください。

```csharp
public class KnowledgeIngestionPipeline
{
    // 1. TacitKnowledgeCandidate を受け取る
    // 2. 重複チェック（既存ナレッジとの類似度検索）
    // 3. タイトル・サマリーの自動生成（LLM）
    // 4. タグの自動付与（LLM）
    // 5. ベクトル埋め込みの生成
    // 6. 選択されたデータストアに保存
    // 7. AI Search インデックスへの登録（RAG用）
    
    public async Task<KnowledgeEntry> IngestAsync(
        TacitKnowledgeCandidate candidate,
        IngestionContext context,
        CancellationToken ct);
}

// ユーザーがAdaptive Cardで「確認済み」にした場合:
// - Status = Confirmed に更新
// - ベクトル埋め込みを再生成（ユーザー修正を反映）
// - インデックスを更新
```

### 9. データ同期サービス

複数のデータストア間でデータを同期するサービスを実装してください。（例: プライマリがCosmos DBだが、RAG検索用にAI Searchにもインデックスを作成する場合）

```csharp
public interface IDataSyncService
{
    // プライマリストアからセカンダリストアへの同期
    Task SyncToSecondaryAsync(string tenantId, CancellationToken ct);
    
    // Cosmos DB Change Feed ベースのリアルタイム同期
    Task StartChangeFeedProcessorAsync(CancellationToken ct);
}
```

### 10. 単体テスト・結合テスト

- `CosmosKnowledgeStoreTests` — CRUD操作、パーティション分割、クエリテスト
- `DataverseKnowledgeStoreTests` — Web API操作テスト
- `AzureAISearchKnowledgeStoreTests` — インデックス操作、ベクトル検索テスト
- `SharePointKnowledgeStoreTests` — リスト・ドキュメント操作テスト
- `KnowledgeIngestionPipelineTests` — パイプライン統合テスト
- `EmbeddingServiceTests` — 埋め込み生成テスト
- 結合テスト: 各ストアへの実データ保存・検索の統合テスト

## 完了条件

- [ ] AI抽出された暗黙知が選択されたデータストアに正しく保存される
- [ ] 4種類のデータストアすべてで CRUD 操作が動作する
- [ ] テナント設定に基づいてデータストアが正しく切り替わる
- [ ] ベクトル埋め込みが生成されAI Searchインデックスに登録される
- [ ] 重複ナレッジの検出が動作する
- [ ] ユーザーのAdaptive Card操作（確認・編集・拒否）が正しくデータストアに反映される
- [ ] 全テストがグリーンである
