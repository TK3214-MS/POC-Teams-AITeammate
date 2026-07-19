using Azure.Search.Documents;
using Azure.Search.Documents.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TeamsAITeammate.Core.Interfaces;
using TeamsAITeammate.Core.Models;

namespace TeamsAITeammate.Infrastructure.Services;

public class AzureAISearchRetriever : IKnowledgeRetriever
{
    private readonly SearchClient _searchClient;
    private readonly IEmbeddingService _embeddingService;
    private readonly ILogger<AzureAISearchRetriever> _logger;

    public AzureAISearchRetriever(
        IConfiguration configuration,
        IEmbeddingService embeddingService,
        ILogger<AzureAISearchRetriever> logger)
    {
        _embeddingService = embeddingService;
        _logger = logger;

        var searchEndpoint = configuration["AzureAISearch:Endpoint"]!;
        var indexName = configuration["AzureAISearch:IndexName"] ?? "knowledge-index";
        var credential = new Azure.Identity.DefaultAzureCredential();
        _searchClient = new SearchClient(new Uri(searchEndpoint), indexName, credential);
    }

    internal AzureAISearchRetriever(
        SearchClient searchClient,
        IEmbeddingService embeddingService,
        ILogger<AzureAISearchRetriever> logger)
    {
        _searchClient = searchClient;
        _embeddingService = embeddingService;
        _logger = logger;
    }

    public async Task<IReadOnlyList<RetrievalResult>> RetrieveAsync(
        RetrievalQuery query, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(query.QueryText))
            return [];

        var searchOptions = BuildSearchOptions(query);
        var searchText = query.Strategy == RetrievalStrategy.VectorOnly ? "*" : query.QueryText;

        if (query.Strategy is RetrievalStrategy.HybridSearch or RetrievalStrategy.VectorOnly)
        {
            var queryVector = await _embeddingService.GenerateEmbeddingAsync(query.QueryText, ct);
            searchOptions.VectorSearch = new VectorSearchOptions
            {
                Queries =
                {
                    new VectorizedQuery(queryVector)
                    {
                        KNearestNeighborsCount = query.MaxResults,
                        Fields = { "ContentVector" }
                    }
                }
            };
        }

        if (query.Strategy == RetrievalStrategy.SemanticRanking)
        {
            searchOptions.QueryType = SearchQueryType.Semantic;
            searchOptions.SemanticSearch = new SemanticSearchOptions
            {
                SemanticConfigurationName = "knowledge-semantic-config"
            };
        }

        var response = await _searchClient.SearchAsync<SearchDocument>(searchText, searchOptions, ct);

        var results = new List<RetrievalResult>();
        await foreach (var result in response.Value.GetResultsAsync())
        {
            var score = (float)(result.Score ?? 0);
            if (score < query.MinRelevanceScore)
                continue;

            var entry = MapFromSearchDocument(result.Document);
            var highlight = ExtractHighlight(result);
            var source = DetermineSource(query.Strategy);

            results.Add(new RetrievalResult
            {
                Entry = entry,
                RelevanceScore = score,
                MatchHighlight = highlight,
                Source = source
            });
        }

        _logger.LogInformation(
            "Retrieved {Count} results for query '{Query}' with strategy {Strategy}",
            results.Count, query.QueryText, query.Strategy);

        return results;
    }

    private static SearchOptions BuildSearchOptions(RetrievalQuery query)
    {
        var options = new SearchOptions
        {
            Size = query.MaxResults,
            IncludeTotalCount = true,
            HighlightFields = { "Content", "Title", "Summary" }
        };

        var filters = new List<string>();

        if (!string.IsNullOrEmpty(query.TenantId))
            filters.Add($"TenantId eq '{EscapeFilter(query.TenantId)}'");

        if (query.CategoryFilter is { Count: > 0 })
        {
            var categoryConditions = query.CategoryFilter
                .Select(c => $"Category eq '{c}'");
            filters.Add($"({string.Join(" or ", categoryConditions)})");
        }

        if (query.DateFrom.HasValue)
            filters.Add($"MeetingDate ge {query.DateFrom.Value:O}");

        if (query.DateTo.HasValue)
            filters.Add($"MeetingDate le {query.DateTo.Value:O}");

        if (query.TagFilter is { Count: > 0 })
        {
            var tagConditions = query.TagFilter
                .Select(t => $"Tags/any(tag: tag eq '{EscapeFilter(t)}')");
            filters.Add($"({string.Join(" or ", tagConditions)})");
        }

        // Exclude rejected/archived entries
        filters.Add($"Status ne '{KnowledgeStatus.Rejected}'");
        filters.Add($"Status ne '{KnowledgeStatus.Archived}'");

        if (filters.Count > 0)
            options.Filter = string.Join(" and ", filters);

        return options;
    }

    private static string ExtractHighlight(SearchResult<SearchDocument> result)
    {
        if (result.Highlights is null || result.Highlights.Count == 0)
            return string.Empty;

        foreach (var field in new[] { "Content", "Title", "Summary" })
        {
            if (result.Highlights.TryGetValue(field, out var highlights) && highlights.Count > 0)
                return string.Join(" ... ", highlights);
        }

        return string.Empty;
    }

    private static RetrievalSource DetermineSource(RetrievalStrategy strategy) => strategy switch
    {
        RetrievalStrategy.HybridSearch => RetrievalSource.HybridSearch,
        RetrievalStrategy.VectorOnly => RetrievalSource.VectorSearch,
        RetrievalStrategy.KeywordOnly => RetrievalSource.KeywordSearch,
        RetrievalStrategy.SemanticRanking => RetrievalSource.SemanticRanking,
        _ => RetrievalSource.HybridSearch
    };

    private static KnowledgeEntry MapFromSearchDocument(SearchDocument doc) => new()
    {
        Id = doc.GetString("Id") ?? string.Empty,
        TenantId = doc.GetString("TenantId") ?? string.Empty,
        MeetingId = doc.GetString("MeetingId") ?? string.Empty,
        SessionId = doc.GetString("SessionId") ?? string.Empty,
        Title = doc.GetString("Title") ?? string.Empty,
        Content = doc.GetString("Content") ?? string.Empty,
        Summary = doc.GetString("Summary") ?? string.Empty,
        Category = Enum.TryParse<TacitKnowledgeCategory>(
            doc.GetString("Category"), out var cat) ? cat : default,
        Status = Enum.TryParse<KnowledgeStatus>(
            doc.GetString("Status"), out var status) ? status : KnowledgeStatus.Draft,
        SourceSpeaker = doc.GetString("SourceSpeaker") ?? string.Empty,
        MeetingSubject = doc.GetString("MeetingSubject") ?? string.Empty,
        MeetingDate = doc.TryGetValue("MeetingDate", out var md) && md is DateTimeOffset mdo
            ? mdo : default,
        Language = doc.GetString("Language") ?? string.Empty,
        Tags = doc.TryGetValue("Tags", out var tags) && tags is IEnumerable<string> tagList
            ? tagList.ToList() : [],
        ConfidenceScore = doc.TryGetValue("Confidence", out var conf) && conf is double c
            ? c : 0
    };

    private static string EscapeFilter(string value) => value.Replace("'", "''");
}
