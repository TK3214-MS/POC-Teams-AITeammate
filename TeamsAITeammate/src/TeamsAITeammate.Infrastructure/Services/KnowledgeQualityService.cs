using Microsoft.Extensions.Logging;
using TeamsAITeammate.Core.Interfaces;
using TeamsAITeammate.Core.Models;

namespace TeamsAITeammate.Infrastructure.Services;

public class KnowledgeQualityService : IKnowledgeQualityService
{
    private readonly IKnowledgeStoreFactory _storeFactory;
    private readonly IEmbeddingService _embeddingService;
    private readonly ILogger<KnowledgeQualityService> _logger;

    public KnowledgeQualityService(
        IKnowledgeStoreFactory storeFactory,
        IEmbeddingService embeddingService,
        ILogger<KnowledgeQualityService> logger)
    {
        _storeFactory = storeFactory;
        _embeddingService = embeddingService;
        _logger = logger;
    }

    public async Task<IReadOnlyList<KnowledgeEntry>> DetectStaleKnowledgeAsync(
        string tenantId, TimeSpan staleThreshold, CancellationToken ct)
    {
        var store = _storeFactory.CreateStore("CosmosDB");
        var allEntries = await store.SearchAsync(
            string.Empty,
            new KnowledgeSearchOptions
            {
                TenantId = tenantId,
                MaxResults = 1000,
                Status = KnowledgeStatus.Confirmed
            },
            ct);

        var cutoff = DateTimeOffset.UtcNow - staleThreshold;
        var stale = allEntries
            .Where(e => (e.UpdatedAt ?? e.CreatedAt) < cutoff)
            .ToList();

        _logger.LogInformation(
            "Detected {Count} stale knowledge entries for tenant {TenantId}",
            stale.Count, tenantId);

        return stale;
    }

    public async Task<IReadOnlyList<KnowledgeConflict>> DetectConflictsAsync(
        KnowledgeEntry newEntry, CancellationToken ct)
    {
        var store = _storeFactory.CreateStore("AzureAISearch");

        // Search for semantically similar entries
        float[]? queryVector = null;
        if (!string.IsNullOrEmpty(newEntry.Content))
        {
            var text = $"{newEntry.Title} {newEntry.Content}";
            queryVector = await _embeddingService.GenerateEmbeddingAsync(text, ct);
        }

        var similar = await store.SearchAsync(
            newEntry.Title,
            new KnowledgeSearchOptions
            {
                TenantId = newEntry.TenantId,
                MaxResults = 10,
                UseVectorSearch = queryVector is not null,
                QueryVector = queryVector,
                MinRelevanceScore = 0.8f,
                Status = KnowledgeStatus.Confirmed
            },
            ct);

        var conflicts = new List<KnowledgeConflict>();
        foreach (var existing in similar)
        {
            if (existing.Id == newEntry.Id)
                continue;

            // Same category but potentially conflicting content
            if (existing.Category == newEntry.Category)
            {
                var similarity = queryVector is not null && existing.Embedding is not null
                    ? CosineSimilarity(queryVector, existing.Embedding)
                    : 0f;

                if (similarity > 0.85f)
                {
                    conflicts.Add(new KnowledgeConflict
                    {
                        Existing = existing,
                        New = newEntry,
                        ConflictDescription =
                            $"High similarity ({similarity:P0}) between existing '{existing.Title}' and new '{newEntry.Title}'",
                        SimilarityScore = similarity
                    });
                }
            }
        }

        _logger.LogInformation(
            "Detected {Count} potential conflicts for entry '{Title}'",
            conflicts.Count, newEntry.Title);

        return conflicts;
    }

    public async Task<IReadOnlyList<MergeSuggestion>> SuggestMergesAsync(
        string tenantId, CancellationToken ct)
    {
        var store = _storeFactory.CreateStore("CosmosDB");
        var entries = await store.SearchAsync(
            string.Empty,
            new KnowledgeSearchOptions
            {
                TenantId = tenantId,
                MaxResults = 500,
                Status = KnowledgeStatus.Confirmed
            },
            ct);

        var suggestions = new List<MergeSuggestion>();

        // Group by category, then check for similarity within each group
        var grouped = entries.GroupBy(e => e.Category);
        foreach (var group in grouped)
        {
            var items = group.ToList();
            for (var i = 0; i < items.Count; i++)
            {
                for (var j = i + 1; j < items.Count; j++)
                {
                    if (items[i].Embedding is not null && items[j].Embedding is not null)
                    {
                        var similarity = CosineSimilarity(items[i].Embedding!, items[j].Embedding!);
                        if (similarity > 0.9f)
                        {
                            suggestions.Add(new MergeSuggestion
                            {
                                Source = items[i],
                                Target = items[j],
                                MergeRationale =
                                    $"High content similarity ({similarity:P0}) within category {group.Key}",
                                SimilarityScore = similarity
                            });
                        }
                    }
                }
            }
        }

        _logger.LogInformation(
            "Found {Count} merge suggestions for tenant {TenantId}",
            suggestions.Count, tenantId);

        return suggestions;
    }

    internal static float CosineSimilarity(float[] a, float[] b)
    {
        if (a.Length != b.Length || a.Length == 0)
            return 0f;

        float dotProduct = 0, normA = 0, normB = 0;
        for (var i = 0; i < a.Length; i++)
        {
            dotProduct += a[i] * b[i];
            normA += a[i] * a[i];
            normB += b[i] * b[i];
        }

        var denominator = MathF.Sqrt(normA) * MathF.Sqrt(normB);
        return denominator == 0 ? 0f : dotProduct / denominator;
    }
}
