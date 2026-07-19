namespace TeamsAITeammate.Core.Models;

public record RetrievalQuery
{
    public string QueryText { get; init; } = string.Empty;
    public string TenantId { get; init; } = string.Empty;
    public RetrievalStrategy Strategy { get; init; } = RetrievalStrategy.HybridSearch;
    public int MaxResults { get; init; } = 10;
    public float MinRelevanceScore { get; init; } = 0.7f;
    public IReadOnlyList<TacitKnowledgeCategory>? CategoryFilter { get; init; }
    public DateTimeOffset? DateFrom { get; init; }
    public DateTimeOffset? DateTo { get; init; }
    public IReadOnlyList<string>? TagFilter { get; init; }
}

public enum RetrievalStrategy
{
    HybridSearch,
    VectorOnly,
    KeywordOnly,
    SemanticRanking
}

public record RetrievalResult
{
    public KnowledgeEntry Entry { get; init; } = new();
    public float RelevanceScore { get; init; }
    public string MatchHighlight { get; init; } = string.Empty;
    public RetrievalSource Source { get; init; }
}

public enum RetrievalSource
{
    VectorSearch,
    KeywordSearch,
    HybridSearch,
    SemanticRanking
}

public record KnowledgeConflict
{
    public KnowledgeEntry Existing { get; init; } = new();
    public KnowledgeEntry New { get; init; } = new();
    public string ConflictDescription { get; init; } = string.Empty;
    public float SimilarityScore { get; init; }
}

public record MergeSuggestion
{
    public KnowledgeEntry Source { get; init; } = new();
    public KnowledgeEntry Target { get; init; } = new();
    public string MergeRationale { get; init; } = string.Empty;
    public float SimilarityScore { get; init; }
}

public record KnowledgeCluster
{
    public string ClusterId { get; init; } = Guid.NewGuid().ToString();
    public string Topic { get; init; } = string.Empty;
    public IReadOnlyList<KnowledgeEntry> Entries { get; init; } = [];
    public float Cohesion { get; init; }
}

public enum RelationType
{
    RelatedTo,
    DerivedFrom,
    Contradicts,
    Supersedes,
    Supports,
    DependsOn
}

public record KnowledgeRelation
{
    public string SourceId { get; init; } = string.Empty;
    public string TargetId { get; init; } = string.Empty;
    public RelationType Type { get; init; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}
