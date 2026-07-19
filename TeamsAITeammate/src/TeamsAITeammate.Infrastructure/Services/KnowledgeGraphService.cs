using Microsoft.Extensions.Logging;
using TeamsAITeammate.Core.Interfaces;
using TeamsAITeammate.Core.Models;

namespace TeamsAITeammate.Infrastructure.Services;

public class KnowledgeGraphService : IKnowledgeGraphService
{
    private readonly IKnowledgeStoreFactory _storeFactory;
    private readonly IEmbeddingService _embeddingService;
    private readonly ILogger<KnowledgeGraphService> _logger;

    // In-memory relation store (production would use Cosmos DB or a graph database)
    private readonly List<KnowledgeRelation> _relations = [];
    private readonly object _lock = new();

    public KnowledgeGraphService(
        IKnowledgeStoreFactory storeFactory,
        IEmbeddingService embeddingService,
        ILogger<KnowledgeGraphService> logger)
    {
        _storeFactory = storeFactory;
        _embeddingService = embeddingService;
        _logger = logger;
    }

    public Task AddRelationAsync(string sourceId, string targetId,
        RelationType type, CancellationToken ct)
    {
        lock (_lock)
        {
            // Prevent duplicate relations
            var exists = _relations.Any(r =>
                r.SourceId == sourceId && r.TargetId == targetId && r.Type == type);

            if (!exists)
            {
                _relations.Add(new KnowledgeRelation
                {
                    SourceId = sourceId,
                    TargetId = targetId,
                    Type = type
                });

                _logger.LogDebug(
                    "Added relation {Type} from {Source} to {Target}",
                    type, sourceId, targetId);
            }
        }

        return Task.CompletedTask;
    }

    public async Task<IReadOnlyList<KnowledgeEntry>> GetRelatedAsync(
        string knowledgeId, int depth, CancellationToken ct)
    {
        if (depth <= 0)
            return [];

        var store = _storeFactory.CreateStore("CosmosDB");
        var visited = new HashSet<string> { knowledgeId };
        var result = new List<KnowledgeEntry>();
        var frontier = new Queue<(string Id, int Depth)>();
        frontier.Enqueue((knowledgeId, 0));

        while (frontier.Count > 0)
        {
            var (currentId, currentDepth) = frontier.Dequeue();
            if (currentDepth >= depth)
                continue;

            IReadOnlyList<KnowledgeRelation> neighbors;
            lock (_lock)
            {
                neighbors = _relations
                    .Where(r => r.SourceId == currentId || r.TargetId == currentId)
                    .ToList();
            }

            foreach (var relation in neighbors)
            {
                var neighborId = relation.SourceId == currentId
                    ? relation.TargetId
                    : relation.SourceId;

                if (visited.Add(neighborId))
                {
                    var entry = await store.GetKnowledgeAsync(neighborId, ct);
                    if (entry is not null)
                    {
                        result.Add(entry);
                        frontier.Enqueue((neighborId, currentDepth + 1));
                    }
                }
            }
        }

        return result;
    }

    public async Task<IReadOnlyList<KnowledgeCluster>> DetectClustersAsync(
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

        // Simple topic-based clustering using RelatedTopics
        var topicMap = new Dictionary<string, List<KnowledgeEntry>>();
        foreach (var entry in entries)
        {
            foreach (var topic in entry.RelatedTopics)
            {
                if (!topicMap.TryGetValue(topic, out var list))
                {
                    list = [];
                    topicMap[topic] = list;
                }
                list.Add(entry);
            }
        }

        var clusters = topicMap
            .Where(kv => kv.Value.Count >= 2) // Only clusters with 2+ entries
            .Select(kv => new KnowledgeCluster
            {
                Topic = kv.Key,
                Entries = kv.Value,
                Cohesion = kv.Value.Count >= 3 ? 0.8f : 0.5f
            })
            .ToList();

        _logger.LogInformation(
            "Detected {Count} clusters for tenant {TenantId}",
            clusters.Count, tenantId);

        return clusters;
    }
}
