namespace TeamsAITeammate.Core.Models;

public record KnowledgeSearchOptions
{
    public int MaxResults { get; init; } = 10;
    public string? TenantId { get; init; }
    public string? SessionId { get; init; }
    public TacitKnowledgeCategory? Category { get; init; }
    public KnowledgeStatus? Status { get; init; }
    public string? Language { get; init; }
    public DateTimeOffset? FromDate { get; init; }
    public DateTimeOffset? ToDate { get; init; }
    public bool UseVectorSearch { get; init; }
    public float[]? QueryVector { get; init; }
    public float MinRelevanceScore { get; init; } = 0.0f;
}

public record KnowledgeStoreStats
{
    public string TenantId { get; init; } = string.Empty;
    public int TotalEntries { get; init; }
    public int DraftCount { get; init; }
    public int ConfirmedCount { get; init; }
    public int RejectedCount { get; init; }
    public int ArchivedCount { get; init; }
    public IReadOnlyDictionary<string, int> EntriesByCategory { get; init; } =
        new Dictionary<string, int>();
    public DateTimeOffset? LastUpdatedAt { get; init; }
}

public record IngestionContext
{
    public string TenantId { get; init; } = string.Empty;
    public string SessionId { get; init; } = string.Empty;
    public string MeetingId { get; init; } = string.Empty;
    public string MeetingSubject { get; init; } = string.Empty;
    public DateTimeOffset MeetingDate { get; init; }
    public IReadOnlyList<string> Participants { get; init; } = [];
    public string Language { get; init; } = string.Empty;
    public string DataStoreProvider { get; init; } = "CosmosDB";
}
