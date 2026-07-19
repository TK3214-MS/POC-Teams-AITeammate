# Phase 1 — 手動セットアップ手順

Phase 1 のコード実装完了後に実施する手動手順をまとめます。

---

## 前提条件

- Azure サブスクリプションへのアクセス権（Contributor以上）
- Microsoft 365 テナントの管理者権限（Entra ID アプリ登録 + API権限の承認）
- GitHub リポジトリへのpush権限
- 以下のCLIがインストール済み:
  - Azure CLI (`az`)
  - Azure Developer CLI (`azd`)
  - Dev Tunnel CLI (`devtunnel`)
  - .NET SDK 10+
  - Node.js 22+

---

## 1. Entra ID アプリ登録

### 1.1 スクリプトでアプリ登録を作成

```bash
az login
cd TeamsAITeammate
chmod +x scripts/setup-entra-app.sh
./scripts/setup-entra-app.sh
```

出力される **Bot App ID** と **Bot App Password** を控えてください。

### 1.2 管理者によるAPI権限の承認

1. [Azure Portal](https://portal.azure.com) → **Microsoft Entra ID** → **アプリの登録** → 作成したアプリを選択
2. **APIのアクセス許可** → **管理者の同意を与える** をクリック
3. 以下の権限がすべて「付与済み」になっていることを確認:

| 権限 | 種類 |
| ------ | ------ |
| `OnlineMeetings.ReadWrite.All` | Application |
| `OnlineMeetingTranscript.Read.All` | Application |
| `Chat.ReadWrite.All` | Application |
| `User.Read.All` | Application |
| `CallRecords.Read.All` | Application |
| `OnlineMeetings.ReadWrite` | Delegated |
| `Chat.ReadWrite` | Delegated |

### 1.3 Bot Channel Registration

1. Azure Portal → **Azure Bot** リソースを作成
2. **Bot App ID** に手順1-1で取得した App ID を設定
3. **Messaging endpoint** に `https://<your-hostname>/api/messages` を設定
4. **チャネル** → **Microsoft Teams** を有効化

---

## 2. Azure リソースのプロビジョニング

### 2.1 azd 初期化

```bash
cd TeamsAITeammate
azd auth login
azd init
```

環境名を聞かれたら `dev` と入力。

### 2.2 パラメータの設定

```bash
azd env set BOT_ID "<手順1-1で取得したApp ID>"
azd env set BOT_PASSWORD "<手順1-1で取得したApp Password>"
```

### 2.3 プロビジョニング & デプロイ

```bash
azd up
```

- リージョン: `japaneast` を推奨
- 完了後、出力される **Container App の FQDN** を控える

### 2.4 デプロイ後の確認

```bash
# ヘルスチェック
curl https://<Container App FQDN>/healthz
# → "Healthy" が返ればOK
```

---

## 3. Teams アプリのサイドロード

### 3.1 manifest.json の更新

`appPackage/manifest.json` 内のプレースホルダーを実際の値に置換:

| プレースホルダー | 置換値 |
| --- | --- |
| `${{BOT_ID}}` | Entra ID App ID |
| `${{HOSTNAME}}` | Container App の FQDN |

### 3.2 アプリパッケージの作成

```bash
cd appPackage
# color.png / outline.png を実際のアイコン画像に差し替え（192x192 / 32x32）
zip -r ../TeamsAITeammate.zip manifest.json color.png outline.png
```

### 3.3 Teams Developer Portal からアップロード

1. [Teams Developer Portal](https://dev.teams.microsoft.com/) にアクセス
2. **アプリ** → **アプリのインポート** → `TeamsAITeammate.zip` をアップロード
3. **プレビュー in Teams** → **追加** でサイドロード

### 3.4 動作確認

- Teams でボットに話しかけ、ウェルカムメッセージが表示されることを確認
- `join` / `status` / `summarize` / `knowledge` コマンドが応答することを確認

---

## 4. GitHub Actions CI の設定

### 4.1 リポジトリシークレットの登録

GitHub → **Settings** → **Secrets and variables** → **Actions** で以下を登録:

| シークレット名 | 値 |
| --- | --- |
| `AZURE_CREDENTIALS` | `az ad sp create-for-rbac --role contributor --scopes /subscriptions/<SUB_ID> --json-auth` の出力 |
| `AZURE_CLIENT_ID` | サービスプリンシパルの Client ID |
| `AZURE_TENANT_ID` | テナント ID |
| `AZURE_SUBSCRIPTION_ID` | サブスクリプション ID |

### 4.2 CI の検証

```bash
git add .
git commit -m "Phase 1: Project foundation"
git push origin main
```

GitHub の **Actions** タブで CI ワークフローが成功することを確認。

---

## 5. ローカル開発環境での Bot 接続テスト

### 5.1 appsettings.Development.json の設定

```json
{
  "Agents": {
    "MicrosoftAppId": "<Bot App ID>",
    "MicrosoftAppPassword": "<Bot App Password>"
  }
}
```

### 5.2 Dev Tunnel の起動

```bash
devtunnel user login
devtunnel create --allow-anonymous
devtunnel port create -p 5000
devtunnel host
```

出力される **Tunnel URL** を控える。

### 5.3 Bot のメッセージングエンドポイント更新

Azure Portal → **Azure Bot** → **構成** → **メッセージングエンドポイント** を:

```text
https://<Tunnel URL>/api/messages
```

に変更。

### 5.4 ローカル起動 & テスト

```bash
cd TeamsAITeammate
dotnet run --project src/TeamsAITeammate.Agent
```

Teams でボットにメッセージを送り、応答があれば接続テスト完了。

---

## 完了チェックリスト

- [ ] Entra ID アプリ登録完了、API権限の管理者承認済み
- [ ] `azd up` で全Azureリソースがプロビジョニング済み
- [ ] Teams Developer Portal からアプリをサイドロードしてインストール済み
- [ ] GitHub Actions CI が PR トリガーで正常に動作
- [ ] ローカル dev tunnel 経由で Bot 接続テストが通る
