using System.Text.Json;
using Azure.Identity;
using Azure.Search.Documents;
using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Indexes.Models;
using Azure.Search.Documents.Models;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TeamsAITeammate.Core.Interfaces;
using TeamsAITeammate.Core.Models;

namespace TeamsAITeammate.Infrastructure.Services;

public class AzureAISearchKnowledgeStore : IKnowledgeStore
{
    private readonly SearchClient _searchClient;
    private readonly SearchIndexClient _indexClient;
    private readonly BlobContainerClient _blobContainer;
    private readonly string _indexName;
    private readonly ILogger<AzureAISearchKnowledgeStore> _logger;

    public string ProviderName => "AzureAISearch";

    public AzureAISearchKnowledgeStore(
        IConfiguration configuration,
        BlobServiceClient blobServiceClient,
        ILogger<AzureAISearchKnowledgeStore> logger)
    {
        _logger = logger;
        var searchEndpoint = configuration["AzureAISearch:Endpoint"]!;
        _indexName = configuration["AzureAISearch:IndexName"] ?? "knowledge-index";
        var credential = new DefaultAzureCredential();

        var endpoint = new Uri(searchEndpoint);
        _indexClient = new SearchIndexClient(endpoint, credential);
        _searchClient = new SearchClient(endpoint, _indexName, credential);

        var blobContainerName = configuration["BlobStorage:KnowledgeContainerName"] ?? "knowledge";
        _blobContainer = blobServiceClient.GetBlobContainerClient(blobContainerName);
    }

    internal AzureAISearchKnowledgeStore(
        SearchClient searchClient,
        SearchIndexClient indexClient,
        BlobContainerClient blobContainer,
        string indexName,
        ILogger<AzureAISearchKnowledgeStore> logger)
    {
        _searchClient = searchClient;
        _indexClient = indexClient;
        _blobContainer = blobContainer;
        _indexName = indexName;
        _logger = logger;
    }

    public async Task<string> SaveKnowledgeAsync(KnowledgeEntry entry, CancellationToken ct)
    {
        // Save original to Blob Storage
        var blobPath = $"{entry.TenantId}/{entry.Category}/{entry.Id}.json";
        var blobClient = _blobContainer.GetBlobClient(blobPath);
        var json = JsonSerializer.Serialize(entry);
        await blobClient.UploadAsync(BinaryData.FromString(json), overwrite: true, ct);

        // Index in AI Search
        var doc = MapToSearchDocument(entry);
        await _searchClient.MergeOrUploadDocumentsAsync(new[] { doc }, cancellationToken: ct);

        _logger.LogInformation("Saved knowledge entry {Id} to AI Search + Blob", entry.Id);
        return entry.Id;
    }

    public async Task UpdateKnowledgeAsync(string id, KnowledgeEntry entry, CancellationToken ct)
    {
        var updated = entry with { Id = id, UpdatedAt = DateTimeOffset.UtcNow };
        await SaveKnowledgeAsync(updated, ct);
        _logger.LogInformation("Updated knowledge entry {Id} in AI Search + Blob", id);
    }

    public async Task<KnowledgeEntry?> GetKnowledgeAsync(string id, CancellationToken ct)
    {
        try
        {
            var response = await _searchClient.GetDocumentAsync<SearchDocument>(id, cancellationToken: ct);
            return MapFromSearchDocument(response.Value);
        }
        catch (Azure.RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    public async Task<IReadOnlyList<KnowledgeEntry>> SearchAsync(
        string query, KnowledgeSearchOptions options, CancellationToken ct)
    {
        var searchOptions = new SearchOptions
        {
            Size = options.MaxResults,
            OrderBy = { "MeetingDate desc" },
            IncludeTotalCount = true
        };

        if (!string.IsNullOrEmpty(options.TenantId))
            searchOptions.Filter = $"TenantId eq '{EscapeSearchFilter(options.TenantId)}'";

        var filters = new List<string>();
        if (!string.IsNullOrEmpty(options.TenantId))
            filters.Add($"TenantId eq '{EscapeSearchFilter(options.TenantId)}'");
        if (options.Category.HasValue)
            filters.Add($"Category eq '{options.Category.Value}'");
        if (options.Status.HasValue)
            filters.Add($"Status eq '{options.Status.Value}'");
        if (options.FromDate.HasValue)
            filters.Add($"MeetingDate ge {options.FromDate.Value:O}");
        if (options.ToDate.HasValue)
            filters.Add($"MeetingDate le {options.ToDate.Value:O}");

        if (filters.Count > 0)
            searchOptions.Filter = string.Join(" and ", filters);

        // Use vector search if embedding is provided
        if (options.UseVectorSearch && options.QueryVector is not null)
        {
            searchOptions.VectorSearch = new VectorSearchOptions
            {
                Queries =
                {
                    new VectorizedQuery(options.QueryVector)
                    {
                        KNearestNeighborsCount = options.MaxResults,
                        Fields = { "ContentVector" }
                    }
                }
            };
        }

        var searchText = string.IsNullOrWhiteSpace(query) ? "*" : query;
        var response = await _searchClient.SearchAsync<SearchDocument>(searchText, searchOptions, ct);

        var results = new List<KnowledgeEntry>();
        await foreach (var result in response.Value.GetResultsAsync())
        {
            if (result.Score >= options.MinRelevanceScore)
            {
                results.Add(MapFromSearchDocument(result.Document));
            }
        }
        return results;
    }

    public async Task DeleteKnowledgeAsync(string id, CancellationToken ct)
    {
        // Get the entry first to find the blob path
        var entry = await GetKnowledgeAsync(id, ct);
        if (entry is not null)
        {
            var blobPath = $"{entry.TenantId}/{entry.Category}/{id}.json";
            var blobClient = _blobContainer.GetBlobClient(blobPath);
            await blobClient.DeleteIfExistsAsync(cancellationToken: ct);
        }

        var batch = IndexDocumentsBatch.Delete("Id", new[] { id });
        await _searchClient.IndexDocumentsAsync(batch, cancellationToken: ct);
        _logger.LogInformation("Deleted knowledge entry {Id} from AI Search + Blob", id);
    }

    public async Task<IReadOnlyList<KnowledgeEntry>> GetBySessionAsync(string sessionId, CancellationToken ct)
    {
        var searchOptions = new SearchOptions
        {
            Filter = $"SessionId eq '{EscapeSearchFilter(sessionId)}'",
            Size = 100,
            OrderBy = { "MeetingDate desc" }
        };

        var response = await _searchClient.SearchAsync<SearchDocument>("*", searchOptions, ct);

        var results = new List<KnowledgeEntry>();
        await foreach (var result in response.Value.GetResultsAsync())
        {
            results.Add(MapFromSearchDocument(result.Document));
        }
        return results;
    }

    public Task<KnowledgeStoreStats> GetStatsAsync(string tenantId, CancellationToken ct)
    {
        // AI Search doesn't support aggregation natively — return a basic stat
        return Task.FromResult(new KnowledgeStoreStats
        {
            TenantId = tenantId
        });
    }

    private static SearchDocument MapToSearchDocument(KnowledgeEntry entry)
    {
        var doc = new SearchDocument
        {
            ["Id"] = entry.Id,
            ["TenantId"] = entry.TenantId,
            ["MeetingId"] = entry.MeetingId,
            ["SessionId"] = entry.SessionId,
            ["Title"] = entry.Title,
            ["Content"] = entry.Content,
            ["Summary"] = entry.Summary,
            ["Category"] = entry.Category.ToString(),
            ["Status"] = entry.Status.ToString(),
            ["SourceSpeaker"] = entry.SourceSpeaker,
            ["MeetingSubject"] = entry.MeetingSubject,
            ["MeetingDate"] = entry.MeetingDate,
            ["Language"] = entry.Language,
            ["Tags"] = entry.Tags.ToArray(),
            ["Confidence"] = entry.ConfidenceScore,
            ["CreatedAt"] = entry.CreatedAt,
            ["UpdatedAt"] = entry.UpdatedAt
        };

        if (entry.Embedding is not null)
            doc["ContentVector"] = entry.Embedding;

        return doc;
    }

    private static KnowledgeEntry MapFromSearchDocument(SearchDocument doc)
    {
        return new KnowledgeEntry
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
    }

    private static string EscapeSearchFilter(string value) => value.Replace("'", "''");
}
