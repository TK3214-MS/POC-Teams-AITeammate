# Phase 4: AI分析・質問生成エンジン

## 概要

Azure OpenAI（GPT-5.5、フォールバック: GPT-4.1）とSemantic Kernelを使用して、会議トランスクリプトをリアルタイムで分析し、追加質問の生成・議題提案・暗黙知の抽出を行うAIエンジンを構築します。

## 前提条件

- Phase 3 が完了していること
- Azure OpenAIリソースにGPT-5.5（またはGPT-4.1）がデプロイ済み
- `text-embedding-3-large` モデルがデプロイ済み

## GitHub Copilotへの指示

### 1. Semantic Kernel 統合

`TeamsAITeammate.AI` プロジェクトにSemantic Kernelベースのサービスを構成してください。

```csharp
// DI登録
services.AddKernel()
    .AddAzureOpenAIChatCompletion(
        deploymentName: config["AzureOpenAI:DeploymentName"],  // gpt-55
        endpoint: config["AzureOpenAI:Endpoint"],
        credentials: new DefaultAzureCredential())
    .AddAzureOpenAITextEmbeddingGeneration(
        deploymentName: "text-embedding-3-large",
        endpoint: config["AzureOpenAI:Endpoint"],
        credentials: new DefaultAzureCredential());

// Microsoft.Extensions.AI 統合
services.AddChatClient(builder => builder
    .UseOpenTelemetry()
    .UseDistributedCache()
    .Use(new RateLimitingMiddleware())
    .UseAzureOpenAI(config));
```

### 2. 会話分析パイプライン

`IConversationAnalyzer` を実装してください。トランスクリプトを受け取り、複数の観点で分析します。

```csharp
public interface IConversationAnalyzer
{
    Task<ConversationAnalysis> AnalyzeAsync(
        ConversationWindow conversation,
        AnalysisContext context,
        CancellationToken ct);
}

public record ConversationAnalysis
{
    // 検出されたトピック一覧
    public IReadOnlyList<DetectedTopic> Topics { get; init; }
    
    // 暗黙知候補
    public IReadOnlyList<TacitKnowledgeCandidate> TacitKnowledgeCandidates { get; init; }
    
    // 生成された追加質問
    public IReadOnlyList<GeneratedQuestion> Questions { get; init; }
    
    // 議論すべき追加議題
    public IReadOnlyList<SuggestedAgendaItem> SuggestedAgenda { get; init; }
    
    // 意思決定の検出
    public IReadOnlyList<DetectedDecision> Decisions { get; init; }
    
    // 未解決のアクションアイテム
    public IReadOnlyList<ActionItem> ActionItems { get; init; }
    
    // 分析メタデータ
    public AnalysisMetadata Metadata { get; init; }
}

public record AnalysisContext
{
    public string SessionId { get; init; }
    public string MeetingSubject { get; init; }
    public IReadOnlyList<string> Participants { get; init; }
    public string DetectedLanguage { get; init; }
    
    // 過去のナレッジベースからの関連知識（RAG）
    public IReadOnlyList<RelevantKnowledge> PriorKnowledge { get; init; }
    
    // 前回の分析結果（差分分析用）
    public ConversationAnalysis? PreviousAnalysis { get; init; }
}
```

### 3. トピック検出

```csharp
public record DetectedTopic
{
    public string Id { get; init; }
    public string Title { get; init; }
    public string Summary { get; init; }
    public DateTimeOffset FirstMentionedAt { get; init; }
    public DateTimeOffset LastMentionedAt { get; init; }
    public TopicStatus Status { get; init; }  // Active, Concluded, Tabled
    public float DiscussionDepth { get; init; }  // 0.0-1.0 深掘り度合い
    public IReadOnlyList<string> KeyTerms { get; init; }
    public IReadOnlyList<string> InvolvedSpeakers { get; init; }
}
```

### 4. 暗黙知抽出

以下のカテゴリの暗黙知を自動抽出するロジックを実装してください。

```csharp
public record TacitKnowledgeCandidate
{
    public string Id { get; init; }
    public TacitKnowledgeCategory Category { get; init; }
    public string Content { get; init; }
    public string Context { get; init; }  // どの会話文脈から抽出されたか
    public string SourceSpeaker { get; init; }
    public float Confidence { get; init; }
    public IReadOnlyList<string> RelatedTopics { get; init; }
    public bool RequiresValidation { get; init; }  // 人間の確認が必要か
}

public enum TacitKnowledgeCategory
{
    DecisionBackground,       // 意思決定の背景・理由
    UndocumentedProcess,      // 未文書化の業務プロセス
    ExpertKnowledge,          // 個人の専門知識・ノウハウ
    DiscussionHistory,        // 議論の経緯・コンテキスト
    OrganizationalContext,    // 組織的な背景情報
    TechnicalInsight,         // 技術的な知見
    LessonsLearned,           // 教訓・過去の失敗から学んだこと
    StakeholderRelationship,  // ステークホルダー関係性
    ImplicitAssumption,       // 暗黙の前提条件
    DomainExpertise           // ドメイン固有の専門知識
}
```

### 5. 深掘り質問生成エンジン

`IQuestionGenerator` を実装してください。

```csharp
public interface IQuestionGenerator
{
    Task<IReadOnlyList<GeneratedQuestion>> GenerateQuestionsAsync(
        ConversationWindow conversation,
        AnalysisContext context,
        QuestionGenerationOptions options,
        CancellationToken ct);
}

public record GeneratedQuestion
{
    public string Id { get; init; }
    public string Question { get; init; }
    public QuestionType Type { get; init; }
    public QuestionPriority Priority { get; init; }
    public string Rationale { get; init; }  // なぜこの質問が重要か
    public string TargetSpeaker { get; init; }  // 回答を期待する話者
    public string RelatedTopicId { get; init; }
    public TacitKnowledgeCategory ExpectedKnowledgeCategory { get; init; }
}

public enum QuestionType
{
    WhyQuestion,          // 「なぜ〜ですか？」理由・背景の深掘り
    ImpactQuestion,       // 「他に影響を受ける〜は？」影響範囲の確認
    ClarificationQuestion,// 「〜について詳しく教えてください」曖昧な点の明確化
    AlternativeQuestion,  // 「他の選択肢は検討しましたか？」代替案の確認
    TimelineQuestion,     // 「期限の根拠は？」時間軸の確認
    StakeholderQuestion,  // 「他に関係者はいますか？」ステークホルダーの確認
    RiskQuestion,         // 「リスクは何ですか？」リスクの洗い出し
    ProcessQuestion,      // 「通常どのように〜しますか？」プロセスの確認
    PrecedentQuestion,    // 「過去に同様のケースは？」前例の確認
    AssumptionQuestion    // 「前提条件は何ですか？」暗黙の前提の顕在化
}

public enum QuestionPriority
{
    Critical,   // 会議中に必ず確認すべき
    High,       // できれば確認すべき
    Medium,     // 時間があれば確認
    Low         // フォローアップで確認可能
}

public record QuestionGenerationOptions
{
    public int MaxQuestions { get; init; } = 5;
    public IReadOnlyList<QuestionType> PreferredTypes { get; init; }
    public bool AvoidDuplicates { get; init; } = true;
    public IReadOnlyList<string> AlreadyAskedQuestionIds { get; init; }
}
```

### 6. プロンプトテンプレート

Semantic Kernelのプロンプトテンプレートとして以下を `Prompts/` ディレクトリに配置してください。

**`Prompts/AnalyzeConversation/config.json` + `skprompt.txt`:**

会話分析用のメインプロンプト。以下の出力をJSON構造化形式で生成:
- トピック一覧とステータス
- 暗黙知候補の抽出
- 意思決定の検出
- アクションアイテムの検出

**`Prompts/GenerateQuestions/config.json` + `skprompt.txt`:**

深掘り質問生成プロンプト。以下を考慮:
- 会話の文脈を理解した上で、文書化されていない知識を引き出す質問を生成
- 同じ質問を繰り返さない（既出質問リストを入力）
- 質問の優先度を判定
- 回答を期待する話者を推定
- 会議の言語で質問を生成

**`Prompts/ExtractTacitKnowledge/config.json` + `skprompt.txt`:**

暗黙知抽出プロンプト。以下のパターンを検出:
- 「いつもこうしている」「慣例として」→ UndocumentedProcess
- 「理由は〜」「背景として」→ DecisionBackground
- 「私の経験では」「以前やったときは」→ ExpertKnowledge / LessonsLearned
- 具体的な数値・期限の根拠が語られている → DomainExpertise

**`Prompts/SuggestAgenda/config.json` + `skprompt.txt`:**

追加議題提案プロンプト。以下を提案:
- 議論が不足しているが重要なトピック
- 言及されたが深掘りされていない論点
- 過去のナレッジベースから関連する未議論事項

### 7. 分析スケジューラー

`AnalysisScheduler` を実装してください。Phase 2の `InterventionTimer` と連携し、適切なタイミングでAI分析を実行します。

```csharp
public class AnalysisScheduler
{
    // 新規トランスクリプトセグメント受信時（デバウンス: 10秒）
    // → 増分分析（直近5分のウィンドウ）
    
    // 沈黙検知時
    // → フル分析 + 質問生成
    
    // 議題切替検知時
    // → 前トピックの暗黙知抽出 + 新トピック用の質問準備
    
    // 定期分析（5分ごと）
    // → サマリー更新 + 質問キュー補充
    
    // @メンション時
    // → オンデマンド分析
}
```

### 8. モデルフォールバック

GPT-5.5が利用できない場合（レートリミット、サービス障害等）にGPT-4.1に自動フォールバックするロジックを実装してください。

```csharp
public class ResilientChatClient : IChatClient
{
    // 1. プライマリモデル（GPT-5.5）で実行
    // 2. 429/503エラー時にフォールバックモデル（GPT-4.1）で再実行
    // 3. Circuit Breakerパターンの実装
    // 4. リクエスト/レスポンスのテレメトリ出力
}
```

### 9. 単体テスト

- `ConversationAnalyzerTests` — モック済みLLMレスポンスでの分析結果検証
- `QuestionGeneratorTests` — 質問生成のバリエーション、重複排除テスト
- `TacitKnowledgeExtractorTests` — 各カテゴリの抽出精度テスト
- `AnalysisSchedulerTests` — スケジューリングロジックのテスト
- `ResilientChatClientTests` — フォールバック動作テスト

### 10. AI品質テスト

`TeamsAITeammate.AIQualityTests` に以下の評価テストを作成してください。

```csharp
public class QuestionQualityTests
{
    // テストケース: 日本語の技術会議トランスクリプトサンプル
    [Fact]
    public async Task GeneratedQuestions_ShouldBeRelevantToConversation()
    {
        // 生成された質問が会話内容に関連しているか
        // GPT-4.1 をジャッジとして使用（self-evaluation）
    }
    
    [Fact]
    public async Task GeneratedQuestions_ShouldNotRepeatAlreadyDiscussedTopics()
    {
        // 既に議論済みの内容を再度質問していないか
    }
    
    [Fact]
    public async Task TacitKnowledge_ShouldBeCorrectlyCategorized()
    {
        // 暗黙知のカテゴリ分類が正しいか
    }
    
    [Fact]
    public async Task Analysis_ShouldWorkInMultipleLanguages()
    {
        // 日本語、英語、混在会議での分析精度
    }
}
```

## 完了条件

- [ ] トランスクリプトを入力するとトピック検出・暗黙知抽出・質問生成が実行される
- [ ] 生成される質問が会話文脈に適切で、重複がない
- [ ] 暗黙知が正しいカテゴリに分類される
- [ ] 多言語（日本語・英語）で分析・質問生成が動作する
- [ ] GPT-5.5 → GPT-4.1 のフォールバックが正しく動作する
- [ ] AI品質テストでの合格率が80%以上
- [ ] 分析レイテンシが10秒以内（増分分析）
