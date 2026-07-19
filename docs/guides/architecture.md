# AI Teammate アーキテクチャ概要

## システムアーキテクチャ

```mermaid
graph TB
    subgraph "Microsoft Teams"
        TC[Teams Client]
        MC[Meeting Chat]
    end

    subgraph "Azure Container Apps"
        AG[TeammateAgent<br/>M365 Agents SDK]
        API[Admin API<br/>ASP.NET Core]
        SH[SignalR Hub]
    end

    subgraph "AI Services"
        AOAI[Azure OpenAI<br/>GPT-5.5 / GPT-4.1]
        EMB[Embedding Service<br/>text-embedding-3-large]
    end

    subgraph "Data Layer"
        COSMOS[(Cosmos DB<br/>Sessions, Knowledge,<br/>Settings, Audit)]
        SEARCH[(Azure AI Search<br/>Vector Index)]
        BLOB[(Blob Storage<br/>Transcripts)]
    end

    subgraph "Monitoring"
        AI[Application Insights]
        MON[Azure Monitor<br/>Workbooks]
        ALERT[Alert Rules]
    end

    subgraph "Frontend"
        SP[Side Panel<br/>React + Fluent UI]
        ADMIN[Admin Panel<br/>React + Fluent UI]
    end

    TC -->|Bot Framework| AG
    MC -->|Transcript| AG
    AG --> AOAI
    AG --> EMB
    AG --> COSMOS
    AG --> SEARCH
    AG --> BLOB
    AG --> SH
    SH --> SP
    API --> COSMOS
    ADMIN --> API
    AG --> AI
    AI --> MON
    AI --> ALERT
```

## コンポーネント構成

```mermaid
graph LR
    subgraph "TeamsAITeammate.Agent"
        A1[TeammateAgent]
        A2[AdminController]
        A3[CopilotController]
        A4[MeetingAnalysisHub]
    end

    subgraph "TeamsAITeammate.AI"
        B1[ConversationAnalyzer]
        B2[QuestionGenerator]
        B3[TacitKnowledgeExtractor]
        B4[RagEnhancedAnalyzer]
        B5[EmbeddingService]
    end

    subgraph "TeamsAITeammate.Infrastructure"
        C1[InterventionOrchestrator]
        C2[NotificationThrottler]
        C3[CardActionHandler]
        C4[KnowledgeStoreFactory]
        C5[GraphTranscriptProvider]
        C6[AITeammateTelemetry]
        C7[HealthChecks]
    end

    subgraph "TeamsAITeammate.Core"
        D1[Interfaces]
        D2[Models]
    end

    A1 --> B1 & B2 & B3
    A1 --> C1 & C2 & C3
    B4 --> B1
    C4 --> C1
    A1 --> D1
    B1 --> D2
```

## データフロー

```mermaid
sequenceDiagram
    participant T as Teams Meeting
    participant A as TeammateAgent
    participant TP as TranscriptPipeline
    participant AI as AI Analysis
    participant KS as KnowledgeStore
    participant SP as SidePanel

    T->>A: Meeting Started
    A->>TP: Start Transcript Collection
    loop Every 5 seconds
        TP->>TP: Buffer Segments
    end
    TP->>AI: Conversation Window
    AI->>AI: Analyze Topics & Generate Questions
    AI->>A: Analysis Result
    A->>T: Adaptive Card (Question)
    T->>A: User Response
    AI->>KS: Extract & Store Knowledge
    A->>SP: Real-time Update (SignalR)
```

## テクノロジースタック

| レイヤー | 技術 |
| --------- | ------ |
| Runtime | .NET 10 / ASP.NET Core |
| Bot Framework | Microsoft 365 Agents SDK |
| AI | Azure OpenAI (GPT-5.5, GPT-4.1) |
| Vector Search | Azure AI Search + text-embedding-3-large |
| Database | Azure Cosmos DB |
| Frontend | React 19 + Fluent UI v9 + TypeScript |
| Hosting | Azure Container Apps |
| CI/CD | GitHub Actions |
| Monitoring | Application Insights + Azure Monitor |
| IaC | Bicep |
