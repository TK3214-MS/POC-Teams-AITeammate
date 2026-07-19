# Phase 5: 手動セットアップ手順

## 前提条件

- Phase 4 が完了していること
- Azure サブスクリプションがあること
- Node.js 20+ がインストールされていること（サイドパネル開発用）

---

## 1. SignalR の設定

### 1.1 Azure SignalR Service の作成（本番環境）

本番環境では Azure SignalR Service を使用します。ローカル開発ではインプロセス SignalR で動作するため不要です。

```bash
RESOURCE_GROUP="rg-teams-ai-teammate"
LOCATION="japaneast"
SIGNALR_NAME="sigr-teams-ai-teammate"

az signalr create \
  --name $SIGNALR_NAME \
  --resource-group $RESOURCE_GROUP \
  --location $LOCATION \
  --sku Standard_S1 \
  --unit-count 1 \
  --service-mode Default

# 接続文字列の取得
az signalr key list \
  --name $SIGNALR_NAME \
  --resource-group $RESOURCE_GROUP \
  --query primaryConnectionString -o tsv
```

### 1.2 CORS の設定（Azure SignalR Service 使用時）

```bash
az signalr cors add \
  --name $SIGNALR_NAME \
  --resource-group $RESOURCE_GROUP \
  --allowed-origins "https://<your-hostname>"
```

---

## 2. サイドパネル SPA のセットアップ

### 2.1 依存関係のインストール

```bash
cd TeamsAITeammate/src/TeamsAITeammate.SidePanel
npm install
```

### 2.2 ローカル開発サーバーの起動

```bash
npm run dev
```

Vite 開発サーバーが `http://localhost:5173` で起動します。  
API リクエスト（`/api/*`）と SignalR（`/hubs/*`）は `http://localhost:5000` にプロキシされます。

### 2.3 本番ビルド

```bash
npm run build
```

`dist/` フォルダにビルドされた静的ファイルが生成されます。このファイルを ASP.NET Core の `wwwroot/` にコピーするか、Azure Static Web Apps にデプロイしてください。

---

## 3. Teams アプリマニフェストの更新

### 3.1 サイドパネル URL の設定

`appPackage/manifest.json` の `configurableTabs` セクションが Phase 2 で設定済みですが、サイドパネル URL が正しいことを確認してください:

```json
"configurableTabs": [
  {
    "configurationUrl": "https://<YOUR_HOSTNAME>/configure",
    "canUpdateConfiguration": true,
    "scopes": ["groupChat"],
    "meetingSurfaces": ["sidePanel"],
    "context": ["meetingSidePanel"]
  }
]
```

`<YOUR_HOSTNAME>` をデプロイ先のホスト名に置き換えてください。

### 3.2 マニフェストの再パッケージ

```bash
cd TeamsAITeammate/appPackage

# ZIP ファイルを作成（color.png, outline.png, manifest.json を含む）
zip -j ai-teammate.zip manifest.json color.png outline.png
```

### 3.3 Teams への再インストール

1. [Teams 管理センター](https://admin.teams.microsoft.com) → **Teams のアプリ** → **アプリを管理**
2. 既存の `AI Teammate` を選択 → **アプリの更新** → 新しい ZIP をアップロード
3. または、Teams クライアントから **アプリ** → **カスタム アプリをアップロード** で再アップロード

---

## 4. Adaptive Card の動作確認

### 4.1 Adaptive Card Designer でのプレビュー

各カードテンプレートの確認には [Adaptive Card Designer](https://adaptivecards.io/designer/) を使用できます:

1. Designer を開く
2. **Host App** で `Microsoft Teams` を選択
3. コード内の `AdaptiveCardTemplates.Build*()` メソッドで生成された JSON をペースト
4. レイアウトとアクションボタンの表示を確認

### 4.2 テスト用の確認コマンド

Bot に以下のコマンドを送信し、Adaptive Card のレスポンスを確認:

| コマンド | 期待される動作 |
| --------- | -------------- |
| `join` | 会議参加後、沈黙検知で質問カード（`QuestionCard`）が投稿される |
| `settings` | テキストで設定一覧が表示される（サイドパネルから設定カード利用可能） |
| `summarize` | サマリーテキストが表示される |

---

## 5. Graph API: チャットメッセージ送信の権限確認

### 5.1 必要な権限（Phase 2 で追加済み）

Adaptive Card をチャットに投稿するには以下の Graph API 権限が必要です（Phase 2 で設定済みであることを確認）:

- `Chat.ReadWrite.All`（アプリケーション権限）

### 5.2 確認方法

```bash
# アプリ登録の権限を確認
az ad app show --id <BOT_ID> --query "requiredResourceAccess[].resourceAccess[].id" -o tsv
```

---

## 6. ローカル開発環境の設定

### 6.1 appsettings.Development.json の確認

`src/TeamsAITeammate.Agent/appsettings.Development.json` に開発用の設定が含まれていることを確認してください。SignalR はローカルではインプロセスで動作するため、追加設定は不要です。

### 6.2 同時起動（バックエンド + フロントエンド）

ローカル開発時は 2 つのターミナルで同時に起動します:

**ターミナル 1 — バックエンド:**

```bash
cd TeamsAITeammate/src/TeamsAITeammate.Agent
dotnet run
```

**ターミナル 2 — フロントエンド:**

```bash
cd TeamsAITeammate/src/TeamsAITeammate.SidePanel
npm run dev
```

### 6.3 dev tunnel の設定（Teams からの接続用）

```bash
# dev tunnel の作成（既存のトンネルがあればスキップ）
devtunnel create --allow-anonymous
devtunnel port create -p 5000

# トンネルの起動
devtunnel host
```

表示された URL を `manifest.json` の `${{HOSTNAME}}` に設定してください。

---

## 7. E2E テストの実行

### 7.1 Playwright ブラウザのインストール

```bash
cd TeamsAITeammate/tests/TeamsAITeammate.E2ETests
dotnet build

# Playwright ブラウザをインストール
pwsh bin/Debug/net10.0/playwright.ps1 install
```

### 7.2 E2E テストの実行

サイドパネルの開発サーバー（`localhost:5173`）が起動している状態で:

```bash
cd TeamsAITeammate
dotnet test tests/TeamsAITeammate.E2ETests
```

> **注意**: E2E テストはデフォルトで `Skip` 属性が付いています。実行するには `[Fact(Skip = "...")]` の `Skip` パラメータを削除してください。

---

## 8. 動作確認チェックリスト

Phase 5 の完了を確認するためのチェックリスト:

- [ ] `dotnet build` が 0 エラー / 0 警告で成功する
- [ ] `dotnet test tests/TeamsAITeammate.UnitTests` で 229 テストが全パスする
- [ ] サイドパネル SPA が `npm run dev` で起動する
- [ ] サイドパネルが `http://localhost:5173` で表示される
- [ ] ダッシュボード / ナレッジ / 質問 / サマリー / 設定の 5 タブが表示される
- [ ] `join` コマンド後、沈黙検知で Adaptive Card が会議チャットに投稿される
- [ ] Adaptive Card のボタン（回答・スキップ・後で回答）が正常に動作する
- [ ] 回答内容がナレッジとして Cosmos DB に保存される
- [ ] 暗黙知確認カードの「正しい」「修正が必要」「削除」が正常に動作する
- [ ] 介入頻度が設定（最小60秒間隔、最大20回/会議）に従って制御される
- [ ] 日本語 / 英語のメッセージが正しくフォーマットされる
- [ ] SignalR 接続でサイドパネルにリアルタイム更新が配信される

---

## 実装済みコンポーネント一覧

| コンポーネント | ファイル | 説明 |
| ------------- | -------- | ------ |
| InterventionOrchestrator | `Infrastructure/Services/InterventionOrchestrator.cs` | 介入判定・実行オーケストレーター |
| NotificationThrottler | `Infrastructure/Services/NotificationThrottler.cs` | 通知スロットリング（60秒間隔、20回上限、連続カード3枚上限） |
| MessageFormatter | `Infrastructure/Services/MessageFormatter.cs` | 多言語メッセージフォーマッター（ja/en） |
| AdaptiveCardTemplates | `Infrastructure/Services/AdaptiveCardTemplates.cs` | 5種のAdaptive Card テンプレート（v1.6） |
| CardActionHandler | `Infrastructure/Services/CardActionHandler.cs` | カードアクション処理（10種のverb） |
| MeetingAnalysisHub | `Agent/Hubs/MeetingAnalysisHub.cs` | SignalR Hub（リアルタイム更新配信） |
| SidePanel SPA | `src/TeamsAITeammate.SidePanel/` | React 19 + Fluent UI v9 + Vite |

### Adaptive Card テンプレート

| カード | メソッド | 用途 |
| ------- | -------- | ------ |
| QuestionCard | `BuildQuestionCard()` | AI生成質問の表示・回答 |
| AgendaSuggestionCard | `BuildAgendaSuggestionCard()` | 追加議題の提案 |
| TacitKnowledgeConfirmCard | `BuildTacitKnowledgeConfirmCard()` | 暗黙知の確認・承認 |
| ConversationSummaryCard | `BuildConversationSummaryCard()` | 会話サマリー表示 |
| SettingsCard | `BuildSettingsCard()` | エージェント設定変更 |

### ユニットテスト（65テスト追加、合計229テスト）

| テストクラス | テスト数 | 対象 |
| ------------ | --------- | ------ |
| InterventionOrchestratorTests | 16 | 介入判定・実行・一時停止 |
| CardActionHandlerTests | 12 | 全カードアクション処理 |
| MessageFormatterTests | 16 | 多言語フォーマット |
| NotificationThrottlerTests | 10 | スロットリングロジック |
| AdaptiveCardTemplateTests | 11 | カードJSON生成・バリデーション |
