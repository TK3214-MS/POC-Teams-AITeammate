# Teams 実機テストランブック

## 目的と制約

この手順は、AI Teammate を既存の Dev 環境へ更新デプロイし、Teams 会議内で次の経路を確認するためのものです。

```text
会議 SidePanel
  -> Teams dialog
  -> テスト実行者のマイク
  -> Azure AI Speech
  -> 認証済み transcript API
  -> Cosmos DB / Blob Storage
  -> Azure OpenAI 分析
  -> SignalR
  -> SidePanel
```

この方式で取得するのは、dialog を開いたユーザーの端末マイク音声だけです。Teams 会議全体の音声や全参加者の分離音声を直接取得する方式ではありません。最初のテストでは 1 人だけが音声分析を開始してください。

## 現在の Dev 環境

- Azure subscription: `E7-Dev`
- Resource group: `rg-aiteammate-dev`
- Region: `japaneast`
- Container App と Azure Bot は既に存在
- Azure AI Speech と Blob Storage は未作成

Azure 拡張と `az` / `azd` CLI のサインイン状態は別管理です。以下を実行するターミナルでは、必ず対象テナントとサブスクリプションを確認してください。

## 1. デプロイ前に完了するコード修正

現状のまま `azd provision` する前に、次を Bicep へ追加します。

1. Storage Account と `transcripts` / `knowledge` Blob container
2. Container App への `BlobStorage__Endpoint` 設定
3. Managed Identity への `Storage Blob Data Contributor`
4. Managed Identity への Azure OpenAI 推論用データプレーンロール
5. Managed Identity への Azure AI Search 用データプレーンロール

Speech resource と Key Vault の Speech key 連携は既に Bicep に追加済みです。上記修正後、`azure-prepare -> azure-validate -> azure-deploy` の順で進めます。検証済みの `.azure/deployment-plan.md` がない状態で本番デプロイを開始しないでください。

## 2. Bot シークレットをローテーション

ローカルの azd 環境には既存 Bot シークレットが保存されています。値をターミナル出力、チャット、チケットへ貼り付けず、新しい資格情報へローテーションします。

1. Entra 管理センターで既存 AI Teammate アプリを開く
2. **Certificates & secrets** で新しい client secret を追加
3. 新しい値を azd 環境へ設定

作成直後に一度だけ表示される **Value** を使用します。**Secret ID** はBot認証には使用できません。

```bash
cd TeamsAITeammate
azd env select dev
read -s BOT_APP_PASSWORD
azd env set BOT_APP_PASSWORD "$BOT_APP_PASSWORD"
unset BOT_APP_PASSWORD
```

新しい資格情報でデプロイと Bot 応答を確認した後、旧 secret を削除します。

## 3. CLI コンテキストを確認

```bash
az login --tenant <tenant-id>
az account set --subscription <subscription-id>
az account show --query '{subscription:name, tenant:tenantId, user:user.name}' -o table

azd auth login
azd env select dev
azd env get-value AZURE_RESOURCE_GROUP
azd env get-value BOT_APP_ID
```

`azd env get-values` 全体は Bot secret を含むため表示しません。

## 4. 既存 Entra アプリへ Teams SSO を設定

`scripts/setup-entra-app.sh` は新規アプリ作成用なので、既存環境では再実行しません。既存の Bot App ID を持つアプリ登録を Entra 管理センターで更新します。

### Expose an API

- Application ID URI: `api://<BOT_APP_ID>`
- Delegated scope: `access_as_user`
- Consent: Admins and users

### Authorized client applications

`access_as_user` scope を次の Teams クライアントへ事前承認します。

| Client | Application ID |
| --- | --- |
| Teams desktop/mobile | `1fec8e78-bce4-4aaf-ab1b-5451cc387264` |
| Teams web | `5e3ce6c0-2b1f-4285-8d4b-75ee78787346` |

既存の Microsoft Graph application permissions について管理者同意が付与済みであることも確認します。

## 5. ローカル検証と Azure preflight

```bash
cd TeamsAITeammate

dotnet test tests/TeamsAITeammate.UnitTests/TeamsAITeammate.UnitTests.csproj

(
  cd src/TeamsAITeammate.SidePanel
  npm ci
  npm run build
)

az bicep build --file infra/main.bicep
azd provision --preview
```

確認項目:

- 単体テストが全件成功
- SidePanel の production build が成功
- Bicep diagnostics が 0 件
- preview に Speech、Storage、Blob RBAC、OpenAI/Search RBAC が含まれる
- 既存 Cosmos DB や Container App の意図しない削除・置換がない
- `japaneast` で Speech と Azure OpenAI model quota が利用可能

## 6. Azure へ更新デプロイ

preflight の証跡を `.azure/deployment-plan.md` に記録し、状態が `Validated` になった後に実行します。

```bash
cd TeamsAITeammate
azd provision
azd deploy
```

デプロイ後に値を取得します。

```bash
RESOURCE_GROUP=$(azd env get-value AZURE_RESOURCE_GROUP)
BOT_APP_ID=$(azd env get-value BOT_APP_ID)
APP_FQDN=$(azd env get-value containerAppFqdn)
CONTAINER_APP_NAME=$(az containerapp list \
  --resource-group "$RESOURCE_GROUP" \
  --query '[0].name' -o tsv)
```

Azure Bot の endpoint を既存リソースへ反映します。

```bash
az bot update \
  --resource-group "$RESOURCE_GROUP" \
  --name aiteammate-bot-dev \
  --endpoint "https://$APP_FQDN/api/messages"
```

## 7. デプロイ直後の確認

```bash
curl --fail --silent --show-error "https://$APP_FQDN/healthz"

az containerapp revision list \
  --name "$CONTAINER_APP_NAME" \
  --resource-group "$RESOURCE_GROUP" \
  --query '[].{name:name, active:properties.active, health:properties.healthState, running:properties.runningState}' \
  -o table
```

Container App の設定で次を確認します。値そのものや secret は表示しません。

- `Speech__Endpoint`
- `Speech__Region`
- Key Vault 参照の `Speech__Key`
- `BlobStorage__Endpoint`
- `TeamsTabAuth__Audience=api://<BOT_APP_ID>`

また、Managed Identity に次のアクセスがあることを確認します。

- Cosmos DB Built-in Data Contributor
- Storage Blob Data Contributor
- Azure OpenAI 推論用ロール
- Azure AI Search の必要なデータプレーンロール
- ACR Pull
- Key Vault secret get/list

## 8. Teams アプリパッケージを生成

既存 manifest を直接書き換えず、一時ディレクトリへ環境別パッケージを生成します。再アップロード時は manifest version を以前より大きくします。

```bash
PACKAGE_DIR=$(mktemp -d)
APP_VERSION="1.0.4"

jq \
  --arg botId "$BOT_APP_ID" \
  --arg fqdn "$APP_FQDN" \
  --arg version "$APP_VERSION" \
  '.version = $version
   | .id = $botId
   | .bots[].botId = $botId
   | .configurableTabs[].configurationUrl = ("https://" + $fqdn + "/configure")
   | .validDomains = [$fqdn]
   | .webApplicationInfo.id = $botId
   | .webApplicationInfo.resource = ("api://" + $botId)' \
  appPackage/manifest.json > "$PACKAGE_DIR/manifest.json"

cp appPackage/color.png appPackage/outline.png "$PACKAGE_DIR/"

(
  cd "$PACKAGE_DIR"
  zip -r "$OLDPWD/ai-teammate-app.zip" manifest.json color.png outline.png
)
```

パッケージには次が含まれる必要があります。

- `manifest.json`
- `color.png`
- `outline.png`

## 9. Teams へアップロード

前提として、Teams 管理センターでカスタムアプリのアップロードがテストユーザーに許可されている必要があります。

1. Teams の **Apps** -> **Manage your apps** を開く
2. **Upload an app** -> **Upload a custom app** を選択
3. `ai-teammate-app.zip` をアップロード
4. 既存アプリがある場合は更新として反映
5. 対象会議からversion 1.0.2以前のAI Teammateを削除
6. Teamsを完全終了して再起動

## 10. テスト会議を準備

1. 同一テナント内でテスト会議を予約
2. 可能ならテスト実行者と確認者の 2 名で参加
3. 会議詳細画面の **Apps** から **AI Teammate** を追加
4. 追加後、会議チャットで `@AI Teammate` が候補に表示されることを確認
5. 必要に応じてAI Teammateタブを追加し、構成画面で **Save** を押す
6. 会議へ参加する。この時点では音声分析を開始しない

デスクトップ版 Teams を最初の検証対象にします。マイク権限を拒否済みの場合は、macOS の **System Settings** -> **Privacy & Security** -> **Microphone** で Teams の許可を確認します。

## 11. 会議内 E2E テスト

### セッション開始

1. 会議チャットで `@AI Teammate join` を送る
2. Bot からセッション開始応答が返ることを確認
3. 会議画面の **Apps** から AI Teammate SidePanel を開く
4. SidePanel の接続表示が `Connected` になることを確認

`join` より先に音声分析を開始すると transcript API は `404` を返します。

### 音声分析

1. SidePanel のマイクボタンを押す
2. 開いた dialog で **分析開始** を押す
3. Teams と macOS のマイク許可を承認
4. `docs/sample-transcripts/car-manufacturing.md` から 30〜60 秒読み上げる
5. dialog に確定した認識テキストが表示されることを確認
6. 10〜30 秒待ち、SidePanel に分析結果が表示されることを確認
7. **停止**を押して dialog を閉じる

### Bot とデータの確認

1. 会議チャットで `@AI Teammate status` を送信
2. transcript 件数が 1 件以上であることを確認
3. `@AI Teammate summarize` を送信し、認識済み内容の要約が返ることを確認
4. Cosmos DB の `sessions` と `transcripts` container に対象データがあることを確認
5. Storage Account の `transcripts` container に JSONL が作成されることを確認
6. Application Insights で Speech token、transcript API、OpenAI、SignalR の失敗がないことを確認

### 終了

1. `@AI Teammate leave` を送る、または会議を終了
2. scheduler と timer が停止することをログで確認
3. raw audio が Blob Storage や Cosmos DB に保存されていないことを確認

## 12. 最低限の異常系テスト

| 操作 | 期待結果 |
| --- | --- |
| `join` 前に分析開始 | transcript API が 404、保存されない |
| `pause` 後に発話 | transcript API が 409、保存されない |
| `resume` 後に発話 | 受付が再開する |
| 別テナント token で送信 | 403 |
| Speech token なしで API 呼び出し | 401 |
| 10 分以上連続動作 | Speech token が更新され、認識が継続する |
| dialog を閉じる | マイク利用が停止する |

## 13. 問題発生時の確認順

### SidePanel が開かない

- manifest の `configurationUrl` と `validDomains`
- Container App の `/configure` と `/sidepanel` が HTTPS で応答するか
- Teams のカスタムアプリポリシー

### `Speech authorization failed: 401`

- Teams SSO の `api://<BOT_APP_ID>` audience
- `access_as_user` scope
- Teams client preauthorization
- Container App の tenant ID

### `Speech authorization failed: 500`

- Speech resource と region
- Key Vault の `SpeechServiceKey`
- Container App identity の Key Vault access

### `Transcript submission failed: 404`

- 同じ会議チャットで先に `join` を実行したか
- Bot と SidePanel が同じ Teams meeting ID を参照しているか

### `Transcript submission failed: 409`

- セッションが paused / ended になっていないか

### 認識テキストは出るが分析されない

- Cosmos DB への transcript write
- Azure OpenAI の Managed Identity RBAC
- model deployment と quota
- AnalysisScheduler と SignalR のログ

### ログ確認

```bash
az containerapp logs show \
  --name "$CONTAINER_APP_NAME" \
  --resource-group "$RESOURCE_GROUP" \
  --type console \
  --tail 200
```

ログや問い合わせへ Bot secret、Speech key、SSO token を貼り付けないでください。
