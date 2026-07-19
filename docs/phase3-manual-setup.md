# Phase 3: 手動セットアップ手順

## 前提条件

- Phase 2 が完了していること
- Azure サブスクリプションがあること
- Teams 会議でトランスクリプション機能が有効化されていること

---

## 1. Graph API 権限の追加

### 1.1 トランスクリプト関連の権限

1. [Azure Portal](https://portal.azure.com) → **Microsoft Entra ID** → **アプリの登録** → Phase 2 で作成した `AI Teammate Bot`
2. **API のアクセス許可** → **アクセス許可の追加** → **Microsoft Graph**
3. **アプリケーションのアクセス許可** で以下を追加:
   - `OnlineMeetingTranscript.Read.All`
4. **管理者の同意を与える** をクリック

---

## 2. Teams テナント設定: トランスクリプション有効化

### 2.1 Teams 管理センター

1. [Teams 管理センター](https://admin.teams.microsoft.com) にアクセス
2. **会議** → **会議ポリシー** → 対象ポリシー（`Global` または カスタム）
3. 以下を有効化:
   - **トランスクリプト**: `オン`
   - **ライブ キャプション**: `オン（ただし翻訳を除く）` または `オン`
4. **保存** をクリック

> **注意**: ポリシー変更の反映には最大24時間かかる場合があります。

### 2.2 PowerShell での設定（代替手段）

```powershell
# Teams PowerShell モジュールのインストール
Install-Module MicrosoftTeams -Force

# 接続
Connect-MicrosoftTeams

# トランスクリプションの有効化
Set-CsTeamsMeetingPolicy -Identity Global -AllowTranscription $true

# 確認
Get-CsTeamsMeetingPolicy -Identity Global | Select-Object AllowTranscription
```

---

## 3. Azure Blob Storage の作成

### 3.1 ストレージアカウント

既存のストレージアカウントがない場合:

```bash
# リソースグループ（Phase 1 で作成済みのものを使用）
RESOURCE_GROUP="rg-teams-ai-teammate"
LOCATION="japaneast"
STORAGE_ACCOUNT="stteamsaiteammate"

az storage account create \
  --name $STORAGE_ACCOUNT \
  --resource-group $RESOURCE_GROUP \
  --location $LOCATION \
  --sku Standard_LRS \
  --kind StorageV2 \
  --min-tls-version TLS1_2
```

### 3.2 Blob コンテナの作成

```bash
# トランスクリプト用コンテナ
az storage container create \
  --account-name $STORAGE_ACCOUNT \
  --name transcripts \
  --auth-mode login

# ドキュメント用コンテナ（未作成の場合）
az storage container create \
  --account-name $STORAGE_ACCOUNT \
  --name documents \
  --auth-mode login
```

### 3.3 RBAC の設定

マネージド ID または開発時の DefaultAzureCredential で Blob にアクセスできるよう、ロールを割り当てます:

```bash
# 現在のユーザー（ローカル開発用）
USER_OBJECT_ID=$(az ad signed-in-user show --query id -o tsv)

az role assignment create \
  --assignee $USER_OBJECT_ID \
  --role "Storage Blob Data Contributor" \
  --scope "/subscriptions/<subscription-id>/resourceGroups/$RESOURCE_GROUP/providers/Microsoft.Storage/storageAccounts/$STORAGE_ACCOUNT"
```

---

## 4. ローカル環境の設定

### 4.1 User Secrets の追加

```bash
cd TeamsAITeammate/src/TeamsAITeammate.Agent

# Blob Storage 接続（DefaultAzureCredential で接続する場合）
dotnet user-secrets set "BlobStorage:Endpoint" "https://<your-storage-account>.blob.core.windows.net/"

# 接続文字列を使用する場合（代替）
# dotnet user-secrets set "BlobStorage:ConnectionString" "DefaultEndpointsProtocol=https;AccountName=..."
```

> **ローカル開発のみ**: Azurite（ストレージエミュレーター）を使用する場合は設定不要（`UseDevelopmentStorage=true` がデフォルト）。

### 4.2 Azurite の起動（ローカル開発）

```bash
# Azurite のインストール
npm install -g azurite

# 起動
azurite --silent --location $TMPDIR/azurite --debug $TMPDIR/azurite/debug.log
```

---

## 5. 動作確認チェックリスト

### ビルド・テスト

- [ ] `dotnet build` がエラー 0 で成功する
- [ ] `dotnet test tests/TeamsAITeammate.UnitTests` で 108 テストがすべてパスする

### Graph API トランスクリプト

- [ ] Teams 会議を開始しトランスクリプション（文字起こし）を有効にする
- [ ] 会議中に Graph API でトランスクリプトが取得可能であることを確認:
  ```bash
  # アクセストークンを取得（az cli の場合）
  TOKEN=$(az account get-access-token --resource https://graph.microsoft.com --query accessToken -o tsv)

  # 会議のトランスクリプト一覧を取得
  curl -H "Authorization: Bearer $TOKEN" \
    "https://graph.microsoft.com/v1.0/communications/onlineMeetings/<meeting-id>/transcripts"
  ```

### リアルタイムパイプライン

- [ ] アプリ起動後、ログに `Transcript pipeline orchestrator started` が出力される
- [ ] 会議参加後、トランスクリプトセグメントがバッファに蓄積される
- [ ] ログに `Detected language:` メッセージで言語が検出される

### Blob Storage 永続化

- [ ] Azurite または Azure Blob Storage の `transcripts` コンテナにファイルが作成される
- [ ] ファイルパスが `{tenantId}/{year}/{month}/{meetingId}/{sessionId}.jsonl` 形式である
- [ ] JSONL ファイルの各行が有効な JSON で、トランスクリプトセグメントを含む

### プロバイダーフォールバック

- [ ] WorkIQ API が利用不可の場合、ログに `WorkIQ API availability check: not available (stub)` が出力される
- [ ] 自動的に Graph API プロバイダーにフォールバックする

---

## 6. Phase 3 で追加されたファイル一覧

| ファイル | 説明 |
|---|---|
| `src/TeamsAITeammate.Core/Models/TranscriptModels.cs` | TranscriptSegment, ConversationWindow, SpeakerStats, SilencePeriod 等のモデル |
| `src/TeamsAITeammate.Core/Interfaces/ITranscriptProvider.cs` | トランスクリプトプロバイダー抽象化 |
| `src/TeamsAITeammate.Core/Interfaces/ITranscriptBuffer.cs` | トランスクリプトバッファ管理 |
| `src/TeamsAITeammate.Core/Interfaces/ILanguageDetector.cs` | 言語自動検出 |
| `src/TeamsAITeammate.Core/Interfaces/ITranscriptPersistence.cs` | Blob Storage 永続化 |
| `src/TeamsAITeammate.Infrastructure/Services/GraphTranscriptProvider.cs` | Graph API 差分ポーリング + VTT パーサー |
| `src/TeamsAITeammate.Infrastructure/Services/WorkIQTranscriptProvider.cs` | WorkIQ API スタブ（フォールバック用） |
| `src/TeamsAITeammate.Infrastructure/Services/TranscriptBuffer.cs` | インメモリバッファ（ウィンドウ取得・沈黙検出・話者統計） |
| `src/TeamsAITeammate.Infrastructure/Services/LanguageDetector.cs` | セグメントタグ優先 + ヒューリスティック言語検出 |
| `src/TeamsAITeammate.Infrastructure/Services/TranscriptPersistenceService.cs` | Azure Blob Storage JSONL 永続化（30秒フラッシュ） |
| `src/TeamsAITeammate.Infrastructure/Services/TranscriptPipelineOrchestrator.cs` | IHostedService パイプライン統括 |
| `tests/TeamsAITeammate.UnitTests/GraphTranscriptProviderTests.cs` | VTT パース テスト（6件） |
| `tests/TeamsAITeammate.UnitTests/TranscriptBufferTests.cs` | バッファ テスト（9件） |
| `tests/TeamsAITeammate.UnitTests/LanguageDetectorTests.cs` | 言語検出 テスト（7件） |
| `tests/TeamsAITeammate.UnitTests/TranscriptPipelineOrchestratorTests.cs` | パイプライン テスト（5件） |

---

## トラブルシューティング

### トランスクリプトが取得できない

1. Teams 管理センターで `AllowTranscription` が `True` になっているか確認
2. Graph API 権限 `OnlineMeetingTranscript.Read.All` が付与・同意済みか確認
3. 会議中にトランスクリプション（文字起こし）機能を手動で開始しているか確認

### Blob Storage への書き込みに失敗する

1. `Storage Blob Data Contributor` ロールが正しく割り当てられているか確認
2. Azurite 使用時は Azurite プロセスが起動しているか確認
3. ログで `Failed to flush transcript` エラーの詳細を確認

### 言語検出が不正確

- Phase 3 の言語検出はヒューリスティック（文字種判定）ベース
- 高精度が必要な場合は Phase 4 以降で Azure AI Language Service への置換を検討
