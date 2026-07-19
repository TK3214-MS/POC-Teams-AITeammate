# Phase 4: AI分析エンジン — 手動セットアップ手順

## 前提条件

- Phase 3 が完了していること
- Azure サブスクリプションへのアクセス権

---

## 1. Azure OpenAI リソースのセットアップ

### 1.1 モデルデプロイメント

Azure Portal または Azure CLI で以下のモデルをデプロイしてください。

```bash
# プライマリモデル（GPT-5.5）
az cognitiveservices account deployment create \
  --name <your-openai-resource> \
  --resource-group <your-rg> \
  --deployment-name gpt-55 \
  --model-name gpt-55 \
  --model-version "2025-05-01" \
  --model-format OpenAI \
  --sku-capacity 30 \
  --sku-name Standard

# フォールバックモデル（GPT-4.1）
az cognitiveservices account deployment create \
  --name <your-openai-resource> \
  --resource-group <your-rg> \
  --deployment-name gpt-41 \
  --model-name gpt-4.1 \
  --model-version "2025-04-14" \
  --model-format OpenAI \
  --sku-capacity 30 \
  --sku-name Standard

# Embedding モデル
az cognitiveservices account deployment create \
  --name <your-openai-resource> \
  --resource-group <your-rg> \
  --deployment-name text-embedding-3-large \
  --model-name text-embedding-3-large \
  --model-version "1" \
  --model-format OpenAI \
  --sku-capacity 30 \
  --sku-name Standard
```

> **注意:** GPT-5.5 がリージョンで利用できない場合は、`gpt-4.1` をプライマリとして設定し、`appsettings.json` の `DeploymentName` を `gpt-41` に変更してください。

### 1.2 RBAC 設定（Managed Identity 使用時）

```bash
# App Service / Container App の Managed Identity に OpenAI アクセス権を付与
az role assignment create \
  --assignee <managed-identity-principal-id> \
  --role "Cognitive Services OpenAI User" \
  --scope /subscriptions/<sub-id>/resourceGroups/<rg>/providers/Microsoft.CognitiveServices/accounts/<openai-resource>
```

---

## 2. アプリケーション設定

### 2.1 ローカル開発（User Secrets）

```bash
cd TeamsAITeammate/src/TeamsAITeammate.Agent

# Azure OpenAI エンドポイント
dotnet user-secrets set "AzureOpenAI:Endpoint" "https://<your-resource>.openai.azure.com/"

# プライマリモデル（デフォルト: gpt-55）
dotnet user-secrets set "AzureOpenAI:DeploymentName" "gpt-55"

# フォールバックモデル（デフォルト: gpt-41）
dotnet user-secrets set "AzureOpenAI:FallbackDeploymentName" "gpt-41"
```

### 2.2 Azure デプロイ（環境変数 / App Configuration）

| 設定キー | 値 | 説明 |
| --- | --- | --- |
| `AzureOpenAI__Endpoint` | `https://<name>.openai.azure.com/` | OpenAI エンドポイント |
| `AzureOpenAI__DeploymentName` | `gpt-55` | プライマリモデル |
| `AzureOpenAI__FallbackDeploymentName` | `gpt-41` | フォールバックモデル |

---

## 3. AI品質テストの実行

AI品質テストは実際の Azure OpenAI 接続が必要です。手動で実行してください。

### 3.1 接続設定

AI品質テストプロジェクトに User Secrets を設定:

```bash
cd TeamsAITeammate/tests/TeamsAITeammate.AIQualityTests

dotnet user-secrets init
dotnet user-secrets set "AzureOpenAI:Endpoint" "https://<your-resource>.openai.azure.com/"
dotnet user-secrets set "AzureOpenAI:DeploymentName" "gpt-55"
```

### 3.2 テスト実行

```bash
# Skip属性を外した後に実行
dotnet test tests/TeamsAITeammate.AIQualityTests --filter "Category=AIQuality"
```

> **注意:** `QuestionQualityTests.cs` の各テストメソッドから `Skip = "Requires Azure OpenAI connection"` を削除し、実際の AI クライアントを初期化するコードを追加してください。

---

## 4. 動作確認チェックリスト

### 4.1 基本動作

- [ ] アプリケーションが正常に起動すること (`dotnet run`)
- [ ] `/healthz` エンドポイントが `Healthy` を返すこと
- [ ] Azure OpenAI への接続が成功すること（ログで確認）

### 4.2 分析エンジン

- [ ] トランスクリプトからトピック検出が動作すること
- [ ] 暗黙知の抽出が正しいカテゴリで動作すること
- [ ] 深掘り質問の生成が会話文脈に適切であること
- [ ] 日本語・英語の両方で分析が動作すること

### 4.3 フォールバック

- [ ] GPT-5.5 が正常時はプライマリモデルが使用されること
- [ ] GPT-5.5 が 429/503 を返した時にフォールバックが動作すること
- [ ] サーキットブレーカーが3回連続失敗後に開くこと
- [ ] 1分後にサーキットブレーカーがhalf-open状態に回復すること

### 4.4 スケジューラー

- [ ] 新規セグメント受信時に10秒デバウンスで増分分析が実行されること
- [ ] 沈黙検知時にフル分析 + 質問生成が実行されること
- [ ] 議題切替時に暗黙知抽出が実行されること
- [ ] 5分ごとの定期分析が実行されること

---

## 5. Phase 4 で作成されたファイル一覧

### Core（モデル・インターフェース）

| ファイル | 説明 |
| --- | --- |
| `src/TeamsAITeammate.Core/Models/AnalysisModels.cs` | 分析結果モデル（ConversationAnalysis, DetectedTopic, TacitKnowledgeCandidate, GeneratedQuestion 等） |
| `src/TeamsAITeammate.Core/Interfaces/IConversationAnalyzer.cs` | 会話分析インターフェース |
| `src/TeamsAITeammate.Core/Interfaces/IQuestionGenerator.cs` | 質問生成インターフェース |
| `src/TeamsAITeammate.Core/Interfaces/ITacitKnowledgeExtractor.cs` | 暗黙知抽出インターフェース |
| `src/TeamsAITeammate.Core/Interfaces/IAnalysisScheduler.cs` | 分析スケジューラーインターフェース |

### AI サービス

| ファイル | 説明 |
| --- | --- |
| `src/TeamsAITeammate.AI/Services/ConversationAnalyzer.cs` | トピック・決定事項・アクションアイテム分析 |
| `src/TeamsAITeammate.AI/Services/QuestionGenerator.cs` | 10種類の深掘り質問を4段階優先度で生成 |
| `src/TeamsAITeammate.AI/Services/TacitKnowledgeExtractor.cs` | 10カテゴリの暗黙知抽出（confidence < 0.5 フィルター） |
| `src/TeamsAITeammate.AI/Services/AnalysisScheduler.cs` | InterventionTimer連携の分析オーケストレーター |
| `src/TeamsAITeammate.AI/Services/ResilientChatClient.cs` | GPT-5.5→GPT-4.1 フォールバック + サーキットブレーカー |

### プロンプトテンプレート（Semantic Kernel）

| ディレクトリ | 説明 |
| --- | --- |
| `src/TeamsAITeammate.AI/Prompts/AnalyzeConversation/` | 会話分析プロンプト |
| `src/TeamsAITeammate.AI/Prompts/GenerateQuestions/` | 質問生成プロンプト |
| `src/TeamsAITeammate.AI/Prompts/ExtractTacitKnowledge/` | 暗黙知抽出プロンプト |
| `src/TeamsAITeammate.AI/Prompts/SuggestAgenda/` | 追加議題提案プロンプト |

### テスト

| ファイル | テスト数 |
| --- | --- |
| `tests/TeamsAITeammate.UnitTests/ConversationAnalyzerTests.cs` | 10 |
| `tests/TeamsAITeammate.UnitTests/QuestionGeneratorTests.cs` | 10 |
| `tests/TeamsAITeammate.UnitTests/TacitKnowledgeExtractorTests.cs` | 12 |
| `tests/TeamsAITeammate.UnitTests/AnalysisSchedulerTests.cs` | 8 |
| `tests/TeamsAITeammate.UnitTests/ResilientChatClientTests.cs` | 16 |
| `tests/TeamsAITeammate.AIQualityTests/QuestionQualityTests.cs` | 4（要接続） |

### 変更されたファイル

| ファイル | 変更内容 |
| --- | --- |
| `src/TeamsAITeammate.Agent/Program.cs` | Semantic Kernel DI、IChatClient、Phase 4 サービス登録追加 |
| `src/TeamsAITeammate.Agent/TeamsAITeammate.Agent.csproj` | `SKEXP0010` NoWarn 追加 |
| `src/TeamsAITeammate.AI/TeamsAITeammate.AI.csproj` | Polly、InternalsVisibleTo、EmbeddedResource 追加 |
| `tests/TeamsAITeammate.AIQualityTests/TeamsAITeammate.AIQualityTests.csproj` | Core プロジェクト参照追加 |
| `tests/TeamsAITeammate.AIQualityTests/AnalysisQualityTests.cs` | コメント更新 |

---

## 6. 次のフェーズへの準備

Phase 5（エージェント介入UI）に進む前に以下を確認してください:

1. Azure OpenAI のデプロイメントが完了していること
2. `dotnet run` でアプリケーションが起動すること
3. 164件の単体テストがすべてパスすること
