# AI Teammate データモデル定義

## Cosmos DB コンテナ

| コンテナ | パーティションキー | 説明 |
| --------- | ------------------- | ------ |
| sessions | /TenantId | 会議セッション |
| transcripts | /SessionId | トランスクリプト |
| knowledge | /TenantId | ナレッジエントリ |
| settings | /TenantId | エージェント設定 |
| users | /TenantId | テナントユーザー |
| audit-logs | /TenantId | 監査ログ |

## エンティティ定義

### MeetingSession

```text
MeetingSession
├── Id: string (GUID)
├── TenantId: string (partition key)
├── MeetingId: string
├── OrganizerId: string
├── Subject: string
├── Participants: Participant[]
│   ├── UserId: string
│   ├── DisplayName: string
│   ├── Email: string
│   └── Role: ParticipantRole (Organizer|Presenter|Attendee)
├── Status: MeetingStatus (Scheduled|InProgress|Ended|Cancelled)
├── State: SessionState (Joining|Active|Analyzing|Paused|Leaving|Completed)
├── StartedAt: DateTimeOffset
├── JoinedAt: DateTimeOffset?
├── EndedAt: DateTimeOffset?
├── CreatedAt: DateTimeOffset
└── Context: MeetingContext?
    ├── ChatId: string
    ├── ThreadId: string
    └── ServiceUrl: string
```

### KnowledgeEntry

```text
KnowledgeEntry
├── Id: string (GUID)
├── TenantId: string (partition key)
├── MeetingId: string
├── SessionId: string
├── Title: string
├── Content: string
├── Summary: string
├── Type: KnowledgeType
├── Category: TacitKnowledgeCategory
├── SourceSpeaker: string
├── SourceTranscriptSegmentId: string
├── SourceContext: string
├── MeetingSubject: string
├── MeetingDate: DateTimeOffset
├── Participants: string[]
├── Tags: string[]
├── RelatedTopics: string[]
├── Language: string
├── ConfidenceScore: double
├── Status: KnowledgeStatus (Draft|Confirmed|Edited|Rejected|Archived)
├── ValidatedBy: string?
├── ValidatedAt: DateTimeOffset?
├── Embedding: float[]?
├── CreatedAt: DateTimeOffset
└── UpdatedAt: DateTimeOffset?
```

### AgentSettings

```text
AgentSettings
├── TenantId: string (partition key / ID)
├── Intervention
│   ├── Frequency: string (low|medium|high)
│   ├── SilenceThresholdSeconds: int
│   ├── MaxInterventionsPerMeeting: int
│   ├── EnableProactiveIntervention: bool
│   └── CooldownSeconds: int
├── QuestionGeneration
│   ├── EnabledCategories: string[]
│   ├── MaxQuestionsPerIntervention: int
│   └── PriorityThreshold: string
├── DataStore
│   ├── PrimaryProvider: string
│   ├── EnableRAG: bool
│   └── RagMinRelevanceScore: double
├── Language
│   ├── AutoDetect: bool
│   ├── PreferredLanguage: string
│   └── SupportedLanguages: string[]
├── MeetingFilter
│   ├── IncludeAllMeetings: bool
│   ├── IncludedOrganizers: string[]
│   ├── ExcludedMeetingPatterns: string[]
│   └── MinimumParticipants: int
├── UpdatedAt: DateTimeOffset
└── UpdatedBy: string
```

### AuditLogEntry

```text
AuditLogEntry
├── Id: string (GUID)
├── TenantId: string (partition key)
├── UserId: string
├── Action: string
├── ResourceType: string
├── ResourceId: string
├── Details: string?
└── Timestamp: DateTimeOffset
```

## Azure AI Search インデックス

### knowledge-index

| フィールド | 型 | 検索可能 | フィルター | ソート |
| ----------- | ----- | --------- | ----------- | -------- |
| id | string | No | Yes | No |
| tenantId | string | No | Yes | No |
| title | string | Yes | No | No |
| content | string | Yes | No | No |
| summary | string | Yes | No | No |
| category | string | No | Yes | No |
| tags | Collection(string) | Yes | Yes | No |
| language | string | No | Yes | No |
| confidenceScore | double | No | Yes | Yes |
| createdAt | DateTimeOffset | No | Yes | Yes |
| embedding | Collection(single) | No | No | No |

ベクトル検索設定:

- アルゴリズム: HNSW
- 次元数: 3072 (text-embedding-3-large)
- メトリック: cosine
