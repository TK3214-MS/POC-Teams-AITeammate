# Phase 3: リアルタイムトランスクリプト取得

## 概要

Teams会議中のトランスクリプトをリアルタイムで取得・バッファリングし、AI分析エンジンに供給するパイプラインを構築します。WorkIQ APIを優先的に使用し、フォールバックとしてGraph APIを使用するデュアルプロバイダー構成とします。

## 前提条件

- Phase 2 が完了していること
- Teams会議でトランスクリプション機能が有効化されていること
- Graph API権限 `OnlineMeetingTranscript.Read.All` が付与済み

## GitHub Copilotへの指示

### 1. トランスクリプトプロバイダー抽象化

複数のトランスクリプト取得手段を切り替え可能な抽象化レイヤーを実装してください。

```csharp
public interface ITranscriptProvider
{
    string ProviderName { get; }
    
    // リアルタイムトランスクリプトストリームの購読開始
    IAsyncEnumerable<TranscriptSegment> StreamTranscriptAsync(
        string meetingId, 
        TranscriptStreamOptions options,
        CancellationToken ct);
    
    // プロバイダーの利用可能性チェック
    Task<bool> IsAvailableAsync(string meetingId, CancellationToken ct);
    
    // 会議開始からの全トランスクリプト取得（途中参加時用）
    Task<IReadOnlyList<TranscriptSegment>> GetFullTranscriptAsync(
        string meetingId,
        CancellationToken ct);
}

public record TranscriptSegment
{
    public string Id { get; init; }
    public string MeetingId { get; init; }
    public string SpeakerId { get; init; }
    public string SpeakerName { get; init; }
    public string Text { get; init; }
    public string Language { get; init; }  // BCP-47 言語コード
    public DateTimeOffset Timestamp { get; init; }
    public TimeSpan Duration { get; init; }
    public float Confidence { get; init; }
}

public record TranscriptStreamOptions
{
    public string PreferredLanguage { get; init; } = "auto";
    public bool IncludeSpeakerIdentification { get; init; } = true;
    public TimeSpan BufferInterval { get; init; } = TimeSpan.FromSeconds(3);
}
```

### 2. WorkIQ API トランスクリプトプロバイダー

`WorkIQTranscriptProvider` を実装してください。

- WorkIQ API（Microsoft Workplace Intelligence API）のリアルタイムトランスクリプト取得エンドポイントを使用
- WebSocket または Server-Sent Events でのストリーミング受信
- 接続断の自動再接続（exponential backoff: 1s → 2s → 4s → 8s → 最大30s）
- レートリミット対応

> **注意**: WorkIQ APIが利用できない場合やリアルタイムストリーミングをサポートしていない場合は、この実装はスキップし、Graph APIプロバイダーにフォールバックする設計としてください。APIの可用性は実行時に `IsAvailableAsync` で判定します。

### 3. Graph API トランスクリプトプロバイダー

`GraphTranscriptProvider` を実装してください。

- `GET /communications/onlineMeetings/{meetingId}/transcripts` APIを使用
- トランスクリプトの差分ポーリング（設定可能なインターバル、デフォルト5秒）
- vttフォーマットのパース
- 話者識別情報の抽出
- Change Notifications（Webhook）対応が可能であれば併用

```csharp
// Graph API のリアルタイムトランスクリプト差分取得
// 1. サブスクリプションを作成してChange Notificationsを受信
// 2. フォールバックとして定期ポーリング
// 3. 新規セグメントのみを抽出してストリームに供給
```

### 4. トランスクリプトバッファ管理

`ITranscriptBuffer` を実装してください。リアルタイムで受信するトランスクリプトを効率的にバッファリングし、AI分析エンジンに適切な単位で供給します。

```csharp
public interface ITranscriptBuffer
{
    // セグメント追加
    Task AppendAsync(TranscriptSegment segment, CancellationToken ct);
    
    // 分析用にバッファ内容を取得（直近N分間）
    Task<ConversationWindow> GetRecentWindowAsync(
        string sessionId, 
        TimeSpan window, 
        CancellationToken ct);
    
    // 全会話履歴を取得
    Task<ConversationWindow> GetFullConversationAsync(
        string sessionId, 
        CancellationToken ct);
    
    // 話者別の発言統計
    Task<IReadOnlyDictionary<string, SpeakerStats>> GetSpeakerStatsAsync(
        string sessionId, 
        CancellationToken ct);
    
    // 沈黙区間の検出
    Task<IReadOnlyList<SilencePeriod>> DetectSilencePeriodsAsync(
        string sessionId, 
        TimeSpan threshold,
        CancellationToken ct);
}

public record ConversationWindow
{
    public string SessionId { get; init; }
    public IReadOnlyList<TranscriptSegment> Segments { get; init; }
    public DateTimeOffset WindowStart { get; init; }
    public DateTimeOffset WindowEnd { get; init; }
    public TimeSpan TotalDuration { get; init; }
    public int UniqueSpearkerCount { get; init; }
    public string DetectedLanguage { get; init; }
    
    // AI分析用のフォーマット済みテキスト
    public string ToFormattedTranscript();
}

public record SpeakerStats
{
    public string SpeakerId { get; init; }
    public string SpeakerName { get; init; }
    public int SegmentCount { get; init; }
    public TimeSpan TotalSpeakingTime { get; init; }
    public DateTimeOffset LastSpokenAt { get; init; }
}

public record SilencePeriod
{
    public DateTimeOffset Start { get; init; }
    public DateTimeOffset End { get; init; }
    public TimeSpan Duration { get; init; }
}
```

### 5. トランスクリプトパイプラインオーケストレーター

`TranscriptPipelineOrchestrator` を実装してください。トランスクリプトの取得からバッファリング、AI分析エンジンへの供給までを統括します。

```csharp
public class TranscriptPipelineOrchestrator : IHostedService
{
    // 1. MeetingSessionManagerから有効なセッションを監視
    // 2. 各セッションに対してTranscriptProviderを選択・起動
    // 3. 受信セグメントをTranscriptBufferに蓄積
    // 4. InterventionTimerと連携して沈黙検知
    // 5. 新規セグメント受信時にAI分析をトリガー（Channel<T>経由）
    // 6. エラー時のフォールバック（WorkIQ → Graph API）
}
```

### 6. 言語自動検出

トランスクリプトの言語を自動検出し、エージェントの応答言語を切り替える機能を実装してください。

```csharp
public interface ILanguageDetector
{
    // セグメント群から主要言語を検出
    Task<LanguageDetectionResult> DetectLanguageAsync(
        IReadOnlyList<TranscriptSegment> segments,
        CancellationToken ct);
}

public record LanguageDetectionResult
{
    public string PrimaryLanguage { get; init; }  // BCP-47
    public float Confidence { get; init; }
    public IReadOnlyDictionary<string, float> LanguageDistribution { get; init; }
}
```

- Azure AI Language Service または Azure OpenAI の言語検出機能を使用
- セグメント単位の言語タグが利用可能な場合はそれを優先
- 複数言語が混在する会議にも対応（話者別の言語追跡）

### 7. トランスクリプト永続化

分析対象のトランスクリプトをAzure Blob Storageに永続化してください。

- 会議セッション単位でBlob保存
- フォーマット: JSON Lines（1行1セグメント）
- パーティション: `{tenantId}/{year}/{month}/{meetingId}/{sessionId}.jsonl`
- ストリーミング中は定期的にフラッシュ（30秒ごと）
- 会議終了時にファイナライズ

### 8. 単体テスト・結合テスト

- `GraphTranscriptProviderTests` — VTTパース、差分取得ロジック
- `TranscriptBufferTests` — バッファリング、ウィンドウ取得、沈黙検出
- `TranscriptPipelineOrchestratorTests` — パイプライン統合、フォールバック動作
- `LanguageDetectorTests` — 言語検出の精度テスト
- 結合テスト: Graph APIモッキングによるエンドツーエンドパイプラインテスト

## 完了条件

- [ ] Teams会議でトランスクリプションを有効にした状態でリアルタイムにセグメントが受信される
- [ ] WorkIQ API不可時にGraph APIに自動フォールバックする
- [ ] バッファから直近N分の会話ウィンドウが正しく取得できる
- [ ] 沈黙区間が正しく検出される
- [ ] 話者識別が正しく動作する
- [ ] 言語が自動検出されログに出力される
- [ ] トランスクリプトがBlob Storageに永続化される
- [ ] 全テストがグリーンである
