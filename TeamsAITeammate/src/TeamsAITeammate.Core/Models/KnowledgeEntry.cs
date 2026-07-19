namespace TeamsAITeammate.Core.Models;

public record KnowledgeEntry
{
    public string Id { get; init; } = Guid.NewGuid().ToString();
    public string TenantId { get; init; } = string.Empty;
    public string SessionId { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Content { get; init; } = string.Empty;
    public KnowledgeType Type { get; init; }
    public List<string> Tags { get; init; } = [];
    public string SourceContext { get; init; } = string.Empty;
    public double ConfidenceScore { get; init; }
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
