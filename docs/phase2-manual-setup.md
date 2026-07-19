# Phase 2: 手動セットアップ手順

## 前提条件

- Phase 1 が完了していること
- Azure サブスクリプションがあること
- Microsoft 365 開発者テナントがあること

---

## 1. Entra ID アプリ登録

### 1.1 アプリ登録の作成

1. [Azure Portal](https://portal.azure.com) → **Microsoft Entra ID** → **アプリの登録** → **新規登録**
2. 以下を入力:
   - **名前**: `AI Teammate Bot`
   - **サポートされるアカウントの種類**: `任意の組織ディレクトリ内のアカウント（マルチテナント）`
   - **リダイレクト URI**: 空のまま
3. **登録** をクリック
4. **アプリケーション (クライアント) ID** をメモ（= `BOT_ID`）

### 1.2 クライアントシークレットの作成

1. **証明書とシークレット** → **新しいクライアント シークレット**
2. **説明**: `Bot Secret`、**有効期限**: 推奨 24ヶ月
3. **追加** → 表示された **値** をメモ（= `BOT_PASSWORD`）

### 1.3 API アクセス許可の追加

1. **API のアクセス許可** → **アクセス許可の追加** → **Microsoft Graph**
2. **アプリケーションのアクセス許可** で以下を追加:
   - `OnlineMeetings.Read.All`
   - `OnlineMeetings.ReadWrite.All`
   - `Chat.ReadWrite.All`
   - `User.Read.All`
3. **管理者の同意を与える** をクリック

---

## 2. Azure Bot リソースの作成

1. [Azure Portal](https://portal.azure.com) → **リソースの作成** → `Azure Bot` を検索
2. 以下を設定:
   - **ボット ハンドル**: `ai-teammate-bot`
   - **サブスクリプション / リソースグループ**: 既存のものを選択
   - **価格レベル**: `F0`（無料）
   - **Microsoft App ID**: 手順1.1でメモした `BOT_ID`
   - **アプリの種類**: `マルチテナント`
3. 作成後、**チャネル** → **Microsoft Teams** を有効化

---

## 3. Dev Tunnel の設定

```bash
# Dev Tunnel CLI のインストール（未インストールの場合）
brew install --cask devtunnel

# ログイン
devtunnel user login

# トンネル作成
devtunnel create ai-teammate --allow-anonymous

# ポートの追加（ASP.NET Core デフォルトポート）
devtunnel port create ai-teammate -p 5000

# トンネル起動
devtunnel host ai-teammate
```

表示される URL（例: `https://xxxxxxxx.devtunnels.ms`）をメモ。

---

## 4. Bot メッセージングエンドポイントの設定

1. Azure Portal → 作成した **Azure Bot** リソース
2. **構成** → **メッセージング エンドポイント** に以下を入力:
   ```
   https://<dev-tunnel-url>/api/messages
   ```
3. **適用** をクリック

---

## 5. ローカル環境の設定

### 5.1 User Secrets の設定

```bash
cd TeamsAITeammate/src/TeamsAITeammate.Agent

# Bot 認証情報
dotnet user-secrets set "Agents:MicrosoftAppId" "<BOT_ID>"
dotnet user-secrets set "Agents:MicrosoftAppPassword" "<BOT_PASSWORD>"

# Azure OpenAI（既に設定済みの場合はスキップ）
dotnet user-secrets set "AzureOpenAI:Endpoint" "https://<your-openai>.openai.azure.com/"

# Cosmos DB（ローカルエミュレーター使用の場合はスキップ）
dotnet user-secrets set "CosmosDb:Endpoint" "https://<your-cosmos>.documents.azure.com:443/"
```

### 5.2 ローカル起動確認

```bash
cd TeamsAITeammate/src/TeamsAITeammate.Agent

# 起動
dotnet run

# 別ターミナルでヘルスチェック確認
curl http://localhost:5000/healthz
# 期待値: Healthy (HTTP 200)
```

---

## 6. Teams アプリパッケージの更新

### 6.1 manifest.json の更新

`TeamsAITeammate/appPackage/manifest.json` を開き、以下のプレースホルダーを実際の値に置換:

| プレースホルダー | 置換先 |
|---|---|
| `{{BOT_ID}}` | 手順1.1の アプリケーション ID |
| `{{BASE_URL}}` | Dev Tunnel URL または デプロイ先 URL |

### 6.2 Teams へのサイドロード

1. `appPackage` フォルダ内の `manifest.json`、`color.png`、`outline.png` を ZIP 圧縮
2. Teams → **アプリ** → **アプリの管理** → **カスタム アプリをアップロード**
3. ZIP ファイルを選択してインストール

---

## 7. 動作確認チェックリスト

### 基本動作

- [ ] `curl http://localhost:5000/healthz` が `200 OK` を返す
- [ ] Dev Tunnel 経由で `https://<tunnel-url>/healthz` にアクセスできる

### Teams Bot 動作

- [ ] Teams チャットで Bot にメッセージを送信し、応答が返る
- [ ] `@AI Teammate join` と送信するとエージェントが「✅ 会議に参加しました」と応答する
- [ ] `@AI Teammate status` で分析状況が表示される
- [ ] `@AI Teammate summarize` でサマリー応答が返る
- [ ] `@AI Teammate ask プロジェクトの進捗は？` で質問応答が返る
- [ ] `@AI Teammate pause` で一時停止される
- [ ] `@AI Teammate resume` で再開される
- [ ] `@AI Teammate settings` で設定情報が表示される
- [ ] `@AI Teammate leave` で退出する
- [ ] 日本語コマンド（「まとめて」「参加して」等）が正しく処理される

### 会議イベント

- [ ] 会議開始時に Bot がログを出力する
- [ ] 会議終了時に Bot がログを出力し、サマリーを生成する
- [ ] 参加者の入退出がログに記録される

---

## トラブルシューティング

| 症状 | 対処 |
|---|---|
| Bot が応答しない | Dev Tunnel が起動しているか確認。Azure Bot のメッセージングエンドポイント URL が正しいか確認 |
| 401 Unauthorized | `MicrosoftAppId` / `MicrosoftAppPassword` が正しいか確認 |
| Graph API エラー | Entra ID の API アクセス許可に管理者の同意が付与されているか確認 |
| Cosmos DB 接続エラー | ローカルエミュレーターが起動しているか、またはエンドポイント URL が正しいか確認 |
| Teams にアプリが表示されない | manifest.json の `botId` が正しいか確認。サイドロードが有効か確認 |
