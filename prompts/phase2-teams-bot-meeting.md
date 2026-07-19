# Phase 2: Teams Bot + 会議参加機能

## 概要

Microsoft 365 Agents SDKを用いてTeams Botを実装し、Teams会議へのAI Teammateとしての参加・退出ライフサイクル、@メンション処理、基本的なチャットメッセージ送信機能を構築します。

## 前提条件

- Phase 1 が完了していること
- Entra IDアプリ登録が完了していること
- dev tunnelが設定済みであること

## GitHub Copilotへの指示

### 1. M365 Agents SDK ベースのBot実装

`TeamsAITeammate.Agent` プロジェクトに以下を実装してください。

**Program.cs（エントリポイント）:**

```csharp
// Microsoft 365 Agents SDK のホスティングパターンを使用
var builder = WebApplication.CreateBuilder(args);

// M365 Agents SDK サービス登録
builder.Services.AddAgents<AITeammateAgent>(builder.Configuration);

// Agentハンドラー登録
builder.Services.AddTransient<IAgentActivityHandler, AITeammateActivityHandler>();

var app = builder.Build();
app.MapAgents();
app.Run();
```

**AITeammateAgent.cs（エージェント本体）:**

M365 Agents SDK の `AgentApplication` を継承し、以下のイベントハンドラーを実装してください：

- `OnMembersAddedAsync` — Bot が会議に追加された時の処理
- `OnMembersRemovedAsync` — Bot が会議から削除された時の処理
- `OnMessageActivityAsync` — テキストメッセージ受信時の処理
- `OnTeamsMeetingStartAsync` — 会議開始イベント
- `OnTeamsMeetingEndAsync` — 会議終了イベント
- `OnTeamsMeetingParticipantsJoinAsync` — 参加者入室
- `OnTeamsMeetingParticipantsLeaveAsync` — 参加者退出

### 2. 会議参加ライフサイクル管理

`IMeetingSessionManager` インターフェースとその実装を作成してください。

```csharp
public interface IMeetingSessionManager
{
    Task<MeetingSession> JoinMeetingAsync(string meetingId, CancellationToken ct);
    Task LeaveMeetingAsync(string sessionId, CancellationToken ct);
    Task<MeetingSession?> GetActiveSessionAsync(string meetingId, CancellationToken ct);
    Task<IReadOnlyList<MeetingSession>> GetActiveSessionsAsync(CancellationToken ct);
    Task UpdateSessionStateAsync(string sessionId, SessionState state, CancellationToken ct);
}

public record MeetingSession
{
    public string SessionId { get; init; }
    public string MeetingId { get; init; }
    public string OrganizerId { get; init; }
    public string TenantId { get; init; }
    public DateTimeOffset JoinedAt { get; init; }
    public SessionState State { get; init; }
    public MeetingContext Context { get; init; }
}

public enum SessionState
{
    Joining,
    Active,
    Analyzing,
    Paused,
    Leaving,
    Completed
}
```

### 3. @メンション処理

エージェントが@メンションされた場合の処理を実装してください。

**対応コマンド:**

| コマンド | 動作 |
|---------|------|
| `@AI Teammate join` | 会議に参加してトランスクリプト分析を開始 |
| `@AI Teammate status` | 現在の分析状態・検出トピック数・蓄積ナレッジ数を表示 |
| `@AI Teammate summarize` | これまでの会話サマリーをAdaptive Cardで表示 |
| `@AI Teammate ask [質問]` | 蓄積ナレッジに対して質問 |
| `@AI Teammate pause` | 分析を一時停止 |
| `@AI Teammate resume` | 分析を再開 |
| `@AI Teammate settings` | 設定変更用Adaptive Cardを表示 |
| `@AI Teammate leave` | 会議から退出 |

**コマンドパーサー:**

`ICommandParser` を実装し、@メンションテキストからコマンドと引数を抽出してください。自然言語でのコマンド入力も受け付けるようにしてください（例：「まとめて」→ `summarize`）。多言語対応（日本語・英語ほか主要言語）を考慮してください。

### 4. Graph APIクライアント

`IGraphMeetingClient` を実装してください。

```csharp
public interface IGraphMeetingClient
{
    // 会議情報取得
    Task<OnlineMeeting> GetMeetingAsync(string meetingId, CancellationToken ct);
    
    // 会議参加者一覧取得
    Task<IReadOnlyList<MeetingParticipant>> GetParticipantsAsync(string meetingId, CancellationToken ct);
    
    // 会議チャットにメッセージ送信
    Task SendChatMessageAsync(string chatId, string message, CancellationToken ct);
    
    // 会議チャットにAdaptive Card送信
    Task SendAdaptiveCardAsync(string chatId, AdaptiveCard card, CancellationToken ct);
    
    // 会議のchatIdを取得
    Task<string> GetMeetingChatIdAsync(string meetingId, CancellationToken ct);
}
```

**認証:**
- Managed Identity を使用したトークン取得
- マルチテナント対応のためテナント別トークンキャッシュ
- `Azure.Identity` の `DefaultAzureCredential` を使用

### 5. 自律介入タイミングエンジン（基盤）

`IInterventionTimer` を実装してください。Phase 4で詳細ロジックを実装しますが、基盤として以下を用意してください。

```csharp
public interface IInterventionTimer
{
    // 沈黙検知（一定時間会話がない場合）
    event Func<SilenceDetectedEvent, Task> OnSilenceDetected;
    
    // 議題切替検知
    event Func<TopicChangeEvent, Task> OnTopicChanged;
    
    // 定期的な分析タイミング
    event Func<PeriodicAnalysisEvent, Task> OnPeriodicAnalysis;
    
    Task StartAsync(string sessionId, InterventionSettings settings, CancellationToken ct);
    Task StopAsync(string sessionId, CancellationToken ct);
    Task ResetSilenceTimerAsync(string sessionId, CancellationToken ct);
}

public record InterventionSettings
{
    public TimeSpan SilenceThreshold { get; init; } = TimeSpan.FromSeconds(30);
    public TimeSpan PeriodicInterval { get; init; } = TimeSpan.FromMinutes(5);
    public bool EnableProactiveIntervention { get; init; } = true;
    public int MaxInterventionsPerMeeting { get; init; } = 20;
}
```

### 6. ヘルスチェック・起動確認

- `/health` エンドポイントを実装
- Bot Framework messaging endpoint `/api/messages` の動作確認
- Teams からの着信リクエストのSecurity Token Validation

### 7. 単体テスト

以下のテストを `TeamsAITeammate.UnitTests` に作成してください：

- `AITeammateActivityHandlerTests` — 各イベントハンドラーの動作テスト
- `CommandParserTests` — コマンドパース正常系・異常系
- `MeetingSessionManagerTests` — セッションライフサイクルテスト
- `InterventionTimerTests` — タイマー動作テスト

## 完了条件

- [ ] Teams会議チャットで `@AI Teammate join` と送信するとエージェントが応答する
- [ ] 会議開始・終了イベントをBotが検知しログに出力される
- [ ] 全コマンドが正しくパースされ適切なハンドラーにディスパッチされる
- [ ] Graph APIでの会議情報取得が動作する
- [ ] マルチテナントでのトークン取得が正しく動作する
- [ ] 単体テストが全てグリーンである
- [ ] ヘルスチェックエンドポイントが200を返す
