# Phase 5: エージェント介入・UI

## 概要

AI分析結果を基に、Teams会議チャットへのメッセージ送信、Adaptive Cardによる構造化表示、サイドパネルUIでのリアルタイムダッシュボードを実装します。エージェントの自律的な介入タイミング制御も含みます。

## 前提条件

- Phase 4 が完了していること
- Adaptive Card Designer でのプレビュー環境が利用可能

## GitHub Copilotへの指示

### 1. 介入オーケストレーター

AI分析結果を受け取り、適切な介入方法・タイミングを制御する `InterventionOrchestrator` を実装してください。

```csharp
public class InterventionOrchestrator
{
    // Phase 2 の InterventionTimer と連携
    // 介入判定ロジック:
    
    // 1. @メンション時 → 即座に応答
    // 2. 沈黙検知時（30秒以上）→ 優先度Highの質問 or 議題提案を投稿
    // 3. 議題切替検知時 → 前トピックの暗黙知サマリー + 質問投稿
    // 4. 定期分析時（5分ごと）→ 蓄積された質問がある場合に投稿
    // 5. 常時監視 → 重要な暗黙知候補検出時に投稿
    
    // 介入抑制ロジック:
    // - 直前の介入から最低1分間は間隔を空ける
    // - 1会議あたりの最大介入回数制限（デフォルト20回）
    // - ユーザーが pause した場合は介入停止
    // - 会議終了5分前は介入を控える
}

public record InterventionAction
{
    public InterventionType Type { get; init; }
    public InterventionTrigger Trigger { get; init; }
    public object Content { get; init; }  // message, card, etc.
    public InterventionPriority Priority { get; init; }
    public DateTimeOffset ScheduledAt { get; init; }
}

public enum InterventionType
{
    ChatMessage,        // テキストメッセージ
    AdaptiveCard,       // Adaptive Card
    SidePanelUpdate,    // サイドパネルのリアルタイム更新
    ProactiveNotification  // プロアクティブ通知
}

public enum InterventionTrigger
{
    UserMention,        // @メンション
    SilenceDetected,    // 沈黙検知
    TopicChange,        // 議題切替
    PeriodicAnalysis,   // 定期分析
    CriticalInsight,    // 重要な知見検出
    UserCommand         // ユーザーコマンド
}
```

### 2. Adaptive Card テンプレート

以下のAdaptive Card テンプレートを作成してください。Adaptive Card Schema v1.6を使用し、Teams対応のデザインとしてください。

**2a. 質問カード (`QuestionCard`)**

```json
// 生成された質問を構造化表示
// - 質問テキスト
// - 質問の理由（折りたたみ可能）
// - 回答入力欄（テキストボックス）
// - 「回答する」「スキップ」「後で回答」ボタン
// - 質問カテゴリのバッジ表示
```

**2b. 議題提案カード (`AgendaSuggestionCard`)**

```json
// 追加議題の提案を表示
// - 提案された議題一覧（チェックリスト形式）
// - 各議題の重要度インジケーター
// - 「この議題を議論する」ボタン
// - 「すべてスキップ」ボタン
```

**2c. 暗黙知確認カード (`TacitKnowledgeConfirmCard`)**

```json
// 抽出された暗黙知の確認・承認
// - 検出された暗黙知の内容
// - カテゴリ表示
// - ソース発言の引用
// - 「正しい」「修正が必要」「削除」ボタン
// - 修正時のテキスト入力欄
```

**2d. 会話サマリーカード (`ConversationSummaryCard`)**

```json
// 会話の要約を表示
// - トピック一覧と各トピックのステータス
// - 意思決定事項
// - アクションアイテム
// - 蓄積されたナレッジ数
// - 話者別の発言統計（グラフ）
// - 「詳細をサイドパネルで見る」ボタン
```

**2e. 設定カード (`SettingsCard`)**

```json
// エージェント設定の変更
// - 介入頻度（スライダー: 低/中/高）
// - 質問カテゴリのオン/オフ
// - 対象言語の選択
// - データ保存先の選択（Dataverse/CosmosDB/AI Search/SharePoint）
// - 「保存」「キャンセル」ボタン
```

### 3. Adaptive Card アクションハンドラー

Adaptive Cardのユーザーアクション（ボタン押下、フォーム送信）を処理するハンドラーを実装してください。

```csharp
public interface ICardActionHandler
{
    Task<InvokeResponse> HandleAdaptiveCardActionAsync(
        ITurnContext turnContext, 
        AdaptiveCardInvokeValue invokeValue,
        CancellationToken ct);
}

// 各カードアクションの処理:
// - QuestionAnswer: 回答をナレッジとして保存、追加質問の生成
// - QuestionSkip: スキップ記録、次の質問を表示
// - QuestionDefer: 後で回答キューに追加
// - AgendaAccept: 議題としてマーク
// - KnowledgeConfirm: ナレッジベースに保存
// - KnowledgeEdit: 修正内容で更新して保存
// - KnowledgeReject: 候補を削除
// - SettingsUpdate: 設定を更新
```

### 4. React + Fluent UI v9 サイドパネル

`TeamsAITeammate.SidePanel` プロジェクトにTeams会議サイドパネルSPAを実装してください。

**技術スタック:**
- React 19+
- Fluent UI React v9 (`@fluentui/react-components`)
- TypeScript 5.x
- Vite（ビルドツール）
- Teams JavaScript SDK v2 (`@microsoft/teams-js`)

**サイドパネルのコンポーネント構成:**

```
SidePanel/
├── src/
│   ├── App.tsx                      # メインアプリ
│   ├── components/
│   │   ├── Dashboard/
│   │   │   ├── AnalysisDashboard.tsx   # 分析ダッシュボード
│   │   │   ├── TopicTimeline.tsx       # トピックタイムライン
│   │   │   └── SpeakerStats.tsx        # 話者統計
│   │   ├── Knowledge/
│   │   │   ├── KnowledgeList.tsx       # 蓄積ナレッジ一覧
│   │   │   ├── KnowledgeDetail.tsx     # ナレッジ詳細
│   │   │   └── KnowledgeSearch.tsx     # ナレッジ検索
│   │   ├── Questions/
│   │   │   ├── QuestionQueue.tsx       # 質問キュー
│   │   │   └── QuestionCard.tsx        # 質問表示
│   │   ├── Summary/
│   │   │   ├── MeetingSummary.tsx       # 会議サマリー
│   │   │   └── ActionItems.tsx          # アクションアイテム
│   │   └── Settings/
│   │       └── AgentSettings.tsx        # エージェント設定
│   ├── hooks/
│   │   ├── useTeamsContext.ts          # Teams SDK連携
│   │   ├── useSignalR.ts              # リアルタイム更新
│   │   └── useAgentApi.ts             # バックエンドAPI
│   ├── services/
│   │   └── signalrClient.ts           # SignalR接続
│   └── types/
│       └── index.ts                   # 型定義
```

**リアルタイム更新:**

ASP.NET Core SignalR を使用してバックエンドからサイドパネルへリアルタイム更新を配信してください。

```csharp
// バックエンド: SignalR Hub
public class MeetingAnalysisHub : Hub
{
    // クライアントグループ: meetingId単位
    public async Task JoinMeeting(string meetingId);
    
    // サーバー → クライアント イベント:
    // - "topicDetected" — 新トピック検出
    // - "knowledgeExtracted" — 暗黙知抽出
    // - "questionGenerated" — 新質問生成
    // - "analysisUpdated" — 分析結果更新
    // - "summaryUpdated" — サマリー更新
}
```

```typescript
// フロントエンド: SignalR クライアント
const useSignalR = (meetingId: string) => {
    // HubConnectionBuilder で接続
    // 各イベントをReact stateに反映
    // 接続断時の自動再接続
};
```

### 5. メッセージフォーマッター

チャットメッセージ送信時のフォーマットを定義してください。

```csharp
public interface IMessageFormatter
{
    // プレーンテキストメッセージ（短い質問・提案）
    string FormatQuestion(GeneratedQuestion question, string language);
    
    // サマリーメッセージ（マークダウン形式）
    string FormatSummary(ConversationAnalysis analysis, string language);
    
    // 多言語対応のテンプレート
    string GetLocalizedTemplate(string templateKey, string language);
}
```

多言語テンプレート例:
- `ja`: 「💡 **追加で確認したい点があります**: {question}\n\n📝 *理由: {rationale}*」
- `en`: 「💡 **I'd like to ask a follow-up**: {question}\n\n📝 *Reason: {rationale}*」

### 6. 通知スロットリング

過剰な通知を防止するスロットリング機構を実装してください。

```csharp
public interface INotificationThrottler
{
    Task<bool> CanSendAsync(string sessionId, InterventionType type, CancellationToken ct);
    Task RecordSentAsync(string sessionId, InterventionType type, CancellationToken ct);
    
    // 設定:
    // - 最小介入間隔: 60秒
    // - 1会議あたり最大介入回数: 20回（設定可能）
    // - 連続質問の最大数: 3問（その後は沈黙まで待機）
    // - カード未回答時の追加カード送信抑制
}
```

### 7. 単体テスト・E2Eテスト

- `InterventionOrchestratorTests` — 各トリガーでの介入判定テスト
- `CardActionHandlerTests` — Adaptive Cardアクションの処理テスト
- `MessageFormatterTests` — 多言語メッセージフォーマットテスト
- `NotificationThrottlerTests` — スロットリングロジックテスト
- `SidePanelE2ETests` (Playwright) — サイドパネルの表示・操作テスト

## 完了条件

- [ ] 沈黙検知時にAI生成質問がAdaptive Cardとして会議チャットに投稿される
- [ ] ユーザーがAdaptive Cardで回答するとナレッジとして保存処理が走る
- [ ] サイドパネルが会議中にリアルタイムで分析結果を表示する
- [ ] SignalRによるリアルタイム更新が1秒以内のレイテンシで動作する
- [ ] 介入頻度が設定に従って制御される
- [ ] 多言語でのメッセージ送信が正しく動作する
- [ ] Adaptive Card の全アクション（回答・スキップ・後で・設定変更）が正常に動作する
- [ ] Playwright E2Eテストでサイドパネルの主要操作が通る
