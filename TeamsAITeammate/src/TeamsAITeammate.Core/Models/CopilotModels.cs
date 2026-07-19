namespace TeamsAITeammate.Core.Models;

public record CopilotSearchRequest
{
    public string Query { get; init; } = string.Empty;
    public int MaxResults { get; init; } = 5;
    public string? Category { get; init; }
    public string? Language { get; init; }
    public DateTimeOffset? FromDate { get; init; }
    public DateTimeOffset? ToDate { get; init; }
}

public record CopilotSearchResponse
{
    public IReadOnlyList<CopilotSearchResult> Results { get; init; } = [];
    public int TotalCount { get; init; }
    public string Query { get; init; } = string.Empty;
}

public record CopilotSearchResult
{
    public string Id { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Summary { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public string MeetingSubject { get; init; } = string.Empty;
    public DateTimeOffset MeetingDate { get; init; }
    public string SourceSpeaker { get; init; } = string.Empty;
    public IReadOnlyList<string> Tags { get; init; } = [];
    public float RelevanceScore { get; init; }
    public string Highlight { get; init; } = string.Empty;
}
