# AI Teammate エージェント アーキテクチャ

## 1. この資料の目的

本資料は、AI Teammate の現行実装を基準に、以下の観点を図示したものです。

- Teams 会議からナレッジ蓄積までのデータフロー
- バックエンド、フロントエンド、AI 処理で利用するフレームワークと SDK
- Azure 上の配置、サービス間接続、認証方式
- 会議参加者、管理者、Copilot 利用者のユーザーフロー

図中の「拡張」は、コード上に抽象化や実装が存在するものの、現行 Bicep だけでは有効化されない構成依存の経路です。

## 2. 論理アーキテクチャ

```mermaid
flowchart LR
    subgraph Users[利用者]
        Participant[会議参加者]
        Admin[テナント管理者]
        CopilotUser[Copilot 利用者]
    end

    subgraph M365[Microsoft 365 / Teams]
        Teams[Microsoft Teams]
        Meeting[Teams 会議・会議チャット]
        SidePanel[会議サイドパネル]
        AdminUI[管理画面]
        Copilot[Copilot Studio]
        Graph[Microsoft Graph]
        BotService[Azure Bot Service<br/>Teams Channel]
    end

    subgraph Runtime[Azure Container Apps: AI Teammate]
        Agent[TeammateAgent<br/>M365 Agents SDK]
        Transcript[Transcript Pipeline]
        Analysis[AI Analysis / RAG]
        Intervention[Intervention Orchestrator]
        Knowledge[Knowledge Ingestion]
        APIs[Admin / Copilot API]
        Hub[SignalR Hub]
    end

    subgraph AzureData[Azure AI・データサービス]
        OpenAI[Azure OpenAI<br/>GPT-5.5 / GPT-5.4-mini<br/>text-embedding-3-large]
        Cosmos[(Azure Cosmos DB<br/>会議・発話・ナレッジ)]
        Search[(Azure AI Search<br/>ハイブリッド・ベクトル検索)]
        Monitor[Application Insights<br/>Log Analytics / Monitor]
        KeyVault[Azure Key Vault]
    end

    subgraph Extensions[構成依存の拡張先]
        Blob[(Azure Blob Storage)]
        Dataverse[(Dataverse)]
        SharePoint[(SharePoint)]
        WorkIQ[Work IQ Transcript Provider]
    end

    Participant --> Teams --> Meeting
    Meeting --> BotService --> Agent
    Agent <--> Graph
    Agent --> Transcript --> Analysis
    Analysis <--> OpenAI
    Analysis <--> Search
    Analysis --> Intervention --> Agent
    Agent --> BotService --> Meeting
    Analysis --> Knowledge
    Knowledge --> Cosmos
    Knowledge --> Search
    Transcript --> Cosmos

    Participant --> SidePanel
    Agent --> Hub --> SidePanel
    Admin --> AdminUI --> APIs
    CopilotUser --> Copilot --> APIs
    APIs --> Cosmos
    APIs --> Search

    Runtime --> Monitor
    KeyVault --> Agent

    Transcript -. 構成時 .-> Blob
    Knowledge -. プロバイダー選択時 .-> Dataverse
    Knowledge -. プロバイダー選択時 .-> SharePoint
    WorkIQ -. 現行は利用不可 .-> Transcript
```

## 3. 会議データフロー

```mermaid
sequenceDiagram
    autonumber
    actor User as 会議参加者
    participant Teams as Microsoft Teams
    participant Bot as TeammateAgent
    participant Graph as Microsoft Graph
    participant Pipeline as Transcript Pipeline
    participant Scheduler as Analysis Scheduler
    participant Search as Azure AI Search
    participant AOAI as Azure OpenAI
    participant Ingest as Knowledge Ingestion
    participant Cosmos as Cosmos DB
    participant UI as Side Panel / SignalR

    User->>Teams: join コマンドまたは会議操作
    Teams->>Bot: Bot Activity
    Bot->>Cosmos: MeetingSession を作成・更新
    Bot->>Graph: 会議情報・トランスクリプトを要求

    loop 会議中
        Graph-->>Pipeline: TranscriptSegment
        Pipeline->>Pipeline: バッファリング・言語判定
        Pipeline->>Cosmos: 発話データを保存
        Pipeline->>Scheduler: 無音・話題変更・定期トリガー
        Scheduler->>Search: 関連ナレッジを検索
        Search-->>Scheduler: RAG コンテキスト
        Scheduler->>AOAI: 会話分析・質問生成・暗黙知抽出
        alt プライマリモデル成功
            AOAI-->>Scheduler: GPT-5.5 分析結果
        else 障害またはサーキットブレーカー作動
            AOAI-->>Scheduler: GPT-5.4-mini 分析結果
        end
        Scheduler->>Ingest: TacitKnowledgeCandidate
        Ingest->>AOAI: エンリッチ・Embedding 生成
        Ingest->>Cosmos: KnowledgeEntry を保存
        Ingest->>Search: 検索インデックスを更新
        Scheduler->>Bot: 介入候補
        Bot->>Bot: 頻度制御・メッセージ整形
        Bot-->>Teams: Adaptive Card / チャットメッセージ
        Scheduler-->>UI: SignalR リアルタイム更新
    end

    User->>Teams: 回答・確認・編集・却下
    Teams->>Bot: Adaptive Card action
    Bot->>Cosmos: ナレッジ状態と監査情報を更新
```

### 主要データと保存先

| データ | 生成元 | 主な処理 | 現行の保存先 |
| --- | --- | --- | --- |
| MeetingSession | `join`、会議イベント | 状態・参加者・開始終了管理 | Cosmos DB `sessions` |
| TranscriptSegment | Microsoft Graph | バッファリング、言語判定、会話ウィンドウ化 | Cosmos DB `transcripts` |
| AnalysisResult | Azure OpenAI | 話題、質問、サマリー、暗黙知候補の生成 | 会議処理内で利用、関連データを永続化 |
| KnowledgeEntry | Knowledge Ingestion | 重複判定、エンリッチ、Embedding、承認状態管理 | Cosmos DB `knowledge`、Azure AI Search |
| AgentSettings / AuditLog | 管理 API、カード操作 | テナント単位の設定・操作記録 | Cosmos DB 用リポジトリ |
| Telemetry | Agent、API、各サービス | トレース、メトリック、正常性監視 | Application Insights / Log Analytics |

## 4. フレームワーク・SDK構成

```mermaid
flowchart TB
    subgraph Frontend[フロントエンド]
        React[React 19 + TypeScript]
        Fluent[Fluent UI v9]
        TeamsJS[Microsoft Teams JavaScript SDK]
        SignalRClient[SignalR JavaScript Client]
        Router[React Router: Admin]
    end

    subgraph Hosting[ホスティング・エージェント]
        DotNet[.NET 10 / ASP.NET Core]
        Agents[Microsoft 365 Agents SDK]
        SignalR[ASP.NET Core SignalR]
        Health[ASP.NET Core Health Checks]
    end

    subgraph AI[AI オーケストレーション]
        SK[Microsoft Semantic Kernel]
        ExtAI[Microsoft.Extensions.AI]
        OpenAISDK[Azure.AI.OpenAI]
        Resilience[Polly / HTTP Resilience]
    end

    subgraph Integration[データ・連携 SDK]
        GraphSDK[Microsoft Graph SDK]
        CosmosSDK[Microsoft.Azure.Cosmos]
        SearchSDK[Azure.Search.Documents]
        BlobSDK[Azure.Storage.Blobs]
        Identity[Azure.Identity]
        AppInsightsSDK[Application Insights SDK]
    end

    React --> Fluent
    React --> TeamsJS
    React --> SignalRClient
    React --> Router
    SignalRClient <--> SignalR
    TeamsJS --> Agents
    Agents --> DotNet
    DotNet --> SK
    DotNet --> ExtAI
    SK --> OpenAISDK
    ExtAI --> OpenAISDK
    ExtAI --> Resilience
    DotNet --> GraphSDK
    DotNet --> CosmosSDK
    DotNet --> SearchSDK
    DotNet -. 構成依存 .-> BlobSDK
    OpenAISDK --> Identity
    GraphSDK --> Identity
    CosmosSDK --> Identity
    SearchSDK --> Identity
    DotNet --> AppInsightsSDK
    DotNet --> Health
```

| レイヤー | フレームワーク / SDK | 用途 |
| --- | --- | --- |
| Agent | Microsoft 365 Agents SDK | Teams Activity、コマンド、Adaptive Card action、Botライフサイクル |
| Web | .NET 10 / ASP.NET Core | Agentホスト、REST API、DI、Health Check、SignalR |
| AI | Semantic Kernel | 埋め込みプロンプトとAzure OpenAI連携 |
| AI抽象化 | Microsoft.Extensions.AI | `IChatClient`、プライマリ・フォールバックモデルの共通化 |
| 耐障害性 | Polly / Microsoft.Extensions.Http.Resilience | リトライ、サーキットブレーカー、フォールバック制御 |
| M365連携 | Microsoft Graph SDK | 会議情報、トランスクリプト、チャット、SharePoint拡張 |
| 検索 | Azure.Search.Documents | キーワード・セマンティック・ベクトルのハイブリッド検索 |
| 永続化 | Microsoft.Azure.Cosmos | 会議、発話、ナレッジ、設定、監査データへのアクセス |
| 認証 | Azure.Identity | `DefaultAzureCredential` とユーザー割り当てマネージドID |
| UI | React 19 / Fluent UI v9 | 会議サイドパネルと管理画面 |
| Teams UI連携 | Teams JavaScript SDK | Teamsコンテキスト取得、アプリ初期化 |
| リアルタイムUI | SignalR | 分析結果と会議状態のプッシュ通知 |

## 5. Azure 配置・認証アーキテクチャ

```mermaid
flowchart TB
    Internet[Microsoft Teams / HTTPS]
    BotService[Azure Bot Service<br/>SingleTenant]

    subgraph ResourceGroup[Azure Resource Group]
        subgraph Compute[実行・配布]
            ACA[Azure Container App<br/>0.5 vCPU / 1 GiB<br/>1-10 replicas]
            ACAEnv[Container Apps Environment]
            ACR[Azure Container Registry: Basic]
            UAMI[User-assigned Managed Identity]
        end

        subgraph AIData[AI・データ]
            AOAI[Azure OpenAI: S0]
            Cosmos[Cosmos DB: Serverless]
            Search[Azure AI Search: Basic]
        end

        subgraph SecurityOps[セキュリティ・運用]
            KV[Azure Key Vault]
            AppInsights[Application Insights]
            Logs[Log Analytics Workspace]
            Workbook[Azure Monitor Workbook]
            Alerts[Azure Monitor Alerts]
        end
    end

    Internet --> BotService -->|HTTPS /api/messages| ACA
    ACR -->|コンテナイメージ| ACA
    UAMI --> ACA
    UAMI -->|AcrPull| ACR
    KV -->|Key Vault secret reference<br/>Bot App Password| ACA
    ACA -->|DefaultAzureCredential| AOAI
    ACA -->|DefaultAzureCredential| Cosmos
    ACA -->|DefaultAzureCredential| Search
    ACA -->|Telemetry| AppInsights
    ACAEnv --> Logs
    AppInsights --> Workbook
    AppInsights --> Alerts
```

### Azure サービス一覧

| Azure サービス | 役割 | 現行構成 |
| --- | --- | --- |
| Azure Container Apps | Agent、API、SignalR、Health Checkの実行 | 外部HTTPS ingress、HTTPスケール、最小1・最大10レプリカ |
| Azure Container Registry | Agentコンテナイメージの保管 | Basic、管理者アカウント無効、Managed IdentityでPull |
| Azure Bot Service | Teams ChannelとAgent endpointの中継 | SingleTenant、`/api/messages`へ配送 |
| Microsoft Entra ID | BotアプリID、テナント境界 | `AzureADMyOrg`、Agent設定もSingleTenant |
| User-assigned Managed Identity | Azure SDKの資格情報 | `AZURE_CLIENT_ID` と `DefaultAzureCredential` で利用 |
| Azure Key Vault | Botクライアントシークレット | Container AppsのKey Vault参照として注入 |
| Azure OpenAI | 会話分析、質問・サマリー生成、Embedding | GPT-5.5、GPT-5.4-mini、text-embedding-3-large |
| Azure Cosmos DB | トランザクションデータの主保存先 | Serverless、Session consistency |
| Azure AI Search | RAG検索とナレッジ索引 | Basic、Semantic Search有効 |
| Application Insights | アプリケーションテレメトリ | ASP.NET Core SDKから送信 |
| Log Analytics | Container Appsログ | 30日保持 |
| Azure Monitor | Workbookとアラート | Application Insightsを監視対象として利用 |

## 6. ユーザーフロー

```mermaid
flowchart TD
    Start([Teams会議を開始]) --> Join[AI Teammateを会議に参加させる]
    Join --> Capture[トランスクリプト収集・分析開始]
    Capture --> Observe{介入トリガーを検出}
    Observe -->|無音・話題変更・定期分析| Analyze[RAG付き会話分析]
    Observe -->|未検出| Capture
    Analyze --> Candidate{介入価値と頻度制限を満たすか}
    Candidate -->|いいえ| Capture
    Candidate -->|はい| Card[質問・議題・知識候補を表示]
    Card --> UserAction{参加者の操作}
    UserAction -->|回答| Learn[回答を分析へ反映]
    UserAction -->|確認| Confirm[ナレッジをConfirmedで保存]
    UserAction -->|編集| Edit[修正内容をEditedで保存]
    UserAction -->|却下| Reject[Rejectedとして記録]
    Learn --> Capture
    Confirm --> Capture
    Edit --> Capture
    Reject --> Capture
    Capture --> End{会議終了か}
    End -->|いいえ| Observe
    End -->|はい| Summary[最終サマリー・会議状態を確定]
    Summary --> Search[蓄積ナレッジをTeams / Copilotから検索]
```

### 利用者別の入口と成果

| 利用者 | 主な入口 | 主な操作 | 得られる成果 |
| --- | --- | --- | --- |
| 会議参加者 | Teams会議チャット、Adaptive Card、サイドパネル | `join`、`status`、`summarize`、`knowledge`、回答・確認・編集・却下 | 会議中の補助質問、リアルタイム分析、会議サマリー、再利用可能なナレッジ |
| テナント管理者 | Admin UI / `/api/admin` | ダッシュボード確認、介入設定、ナレッジ・ユーザー管理 | 利用状況、品質、設定、監査証跡の管理 |
| Copilot利用者 | Copilot Studio / `/api/copilot` | テナント内ナレッジ検索、詳細取得、統計参照 | RAGによる組織ナレッジの再利用 |

## 7. 実装境界と拡張ポイント

| 項目 | 現在の位置づけ | 図上の扱い |
| --- | --- | --- |
| GraphTranscriptProvider | 現行の利用可能なトランスクリプト経路 | 主経路 |
| WorkIQTranscriptProvider | `IsAvailableAsync` が利用不可を返すスタブ | 拡張経路 |
| CosmosKnowledgeStore | 既定のナレッジ保存先 | 主経路 |
| AzureAISearchKnowledgeStore / Retriever | RAG索引・検索用 | 主経路。ただし索引初期化とRBACが必要 |
| DataverseKnowledgeStore | 環境URL設定時に利用する代替プロバイダー | 拡張経路 |
| SharePointKnowledgeStore | Site ID設定時に利用する代替プロバイダー | 拡張経路 |
| Azure Blob Storage | SDKと永続化サービスは存在するが、現行BicepにStorage Accountはない | 拡張経路 |
| Side Panel / Admin UI | Reactアプリは存在するが、現行Container App Bicepに静的配信の明示構成はない | UI論理構成として表示 |
| Cosmos settings / audit-logs | リポジトリ実装は存在するが、現行Cosmos Bicepの明示コンテナは3個 | 配備時に追加初期化が必要 |

## 8. 実装参照

- Agent起動・DI: [Program.cs](../../TeamsAITeammate/src/TeamsAITeammate.Agent/Program.cs)
- Bot処理: [TeammateAgent.cs](../../TeamsAITeammate/src/TeamsAITeammate.Agent/TeammateAgent.cs)
- トランスクリプト処理: [TranscriptPipelineOrchestrator.cs](../../TeamsAITeammate/src/TeamsAITeammate.Infrastructure/Services/TranscriptPipelineOrchestrator.cs)
- AI分析: [ConversationAnalyzer.cs](../../TeamsAITeammate/src/TeamsAITeammate.AI/Services/ConversationAnalyzer.cs)
- RAG分析: [RagEnhancedConversationAnalyzer.cs](../../TeamsAITeammate/src/TeamsAITeammate.AI/Services/RagEnhancedConversationAnalyzer.cs)
- ナレッジ取り込み: [KnowledgeIngestionPipeline.cs](../../TeamsAITeammate/src/TeamsAITeammate.AI/Services/KnowledgeIngestionPipeline.cs)
- 介入制御: [InterventionOrchestrator.cs](../../TeamsAITeammate/src/TeamsAITeammate.Infrastructure/Services/InterventionOrchestrator.cs)
- RAG検索: [AzureAISearchRetriever.cs](../../TeamsAITeammate/src/TeamsAITeammate.Infrastructure/Services/AzureAISearchRetriever.cs)
- Azure構成: [main.bicep](../../TeamsAITeammate/infra/main.bicep)
- Teamsアプリ定義: [manifest.json](../../TeamsAITeammate/appPackage/manifest.json)
