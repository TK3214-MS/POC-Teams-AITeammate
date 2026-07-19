namespace TeamsAITeammate.Core.Models;

public record KnowledgeEntry
{
    public string Id { get; init; } = Guid.NewGuid().ToString();
    public string TenantId { get; init; } = string.Empty;
    public string MeetingId { get; init; } = string.Empty;
    public string SessionId { get; init; } = string.Empty;

    // ナレッジ内容
    public string Title { get; init; } = string.Empty;
    public string Content { get; init; } = string.Empty;
    public string Summary { get; init; } = string.Empty;
    public KnowledgeType Type { get; init; }
    public TacitKnowledgeCategory Category { get; init; }

    // メタデータ
    public string SourceSpeaker { get; init; } = string.Empty;
    public string SourceTranscriptSegmentId { get; init; } = string.Empty;
    public string SourceContext { get; init; } = string.Empty;
    public string MeetingSubject { get; init; } = string.Empty;
    public DateTimeOffset MeetingDate { get; init; }
    public IReadOnlyList<string> Participants { get; init; } = [];

    // 分類・タグ
    public List<string> Tags { get; init; } = [];
    public IReadOnlyList<string> RelatedTopics { get; init; } = [];
    public string Language { get; init; } = string.Empty;

    // 品質メタデータ
    public double ConfidenceScore { get; init; }
    public KnowledgeStatus Status { get; init; } = KnowledgeStatus.Draft;
    public string? ValidatedBy { get; init; }
    public DateTimeOffset? ValidatedAt { get; init; }

    // ベクトル埋め込み（AI Search用）
    public float[]? Embedding { get; init; }

    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UpdatedAt { get; set; }
}

public enum KnowledgeType
{
    TacitKnowledge,
    Decision,
    ActionItem,
    Insight,
    Question,
    Risk,
    BestPractice
}

public enum KnowledgeStatus
{
    Draft,
    Confirmed,
    Edited,
    Rejected,
    Archived
}
