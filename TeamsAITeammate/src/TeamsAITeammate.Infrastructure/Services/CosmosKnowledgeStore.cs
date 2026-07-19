using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Azure.Identity;
using TeamsAITeammate.Core.Interfaces;
using TeamsAITeammate.Core.Models;

namespace TeamsAITeammate.Infrastructure.Services;

public class CosmosKnowledgeStore : IKnowledgeStore
{
    private readonly Container _container;
    private readonly ILogger<CosmosKnowledgeStore> _logger;

    public string ProviderName => "CosmosDB";

    public CosmosKnowledgeStore(IConfiguration configuration, ILogger<CosmosKnowledgeStore> logger)
    {
        _logger = logger;
        var endpoint = configuration["CosmosDb:Endpoint"]!;
        var databaseName = configuration["CosmosDb:DatabaseName"]!;
        var containerName = configuration["CosmosDb:KnowledgeContainer"]!;

        var client = new CosmosClient(endpoint, new DefaultAzureCredential());
        _container = client.GetContainer(databaseName, containerName);
    }

    internal CosmosKnowledgeStore(Container container, ILogger<CosmosKnowledgeStore> logger)
    {
        _container = container;
        _logger = logger;
    }

    public async Task<string> SaveKnowledgeAsync(KnowledgeEntry entry, CancellationToken ct)
    {
        var response = await _container.CreateItemAsync(entry, new PartitionKey(entry.TenantId), cancellationToken: ct);
        _logger.LogInformation("Saved knowledge entry {Id} to Cosmos DB", response.Resource.Id);
        return response.Resource.Id;
    }

    public async Task UpdateKnowledgeAsync(string id, KnowledgeEntry entry, CancellationToken ct)
    {
        var updated = entry with { Id = id, UpdatedAt = DateTimeOffset.UtcNow };
        await _container.UpsertItemAsync(updated, new PartitionKey(updated.TenantId), cancellationToken: ct);
        _logger.LogInformation("Updated knowledge entry {Id}", id);
    }

    public async Task<KnowledgeEntry?> GetKnowledgeAsync(string id, CancellationToken ct)
    {
        var query = new QueryDefinition("SELECT * FROM c WHERE c.id = @id")
            .WithParameter("@id", id);

        using var iterator = _container.GetItemQueryIterator<KnowledgeEntry>(query);
        if (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync(ct);
            return response.FirstOrDefault();
        }
        return null;
    }

    public async Task<IReadOnlyList<KnowledgeEntry>> SearchAsync(
        string query, KnowledgeSearchOptions options, CancellationToken ct)
    {
        var conditions = new List<string>();
        var queryDef = new QueryDefinition(string.Empty);

        if (!string.IsNullOrEmpty(options.TenantId))
        {
            conditions.Add("c.TenantId = @tenantId");
            queryDef = queryDef.WithParameter("@tenantId", options.TenantId);
        }

        if (options.Category.HasValue)
        {
            conditions.Add("c.Category = @category");
            queryDef = queryDef.WithParameter("@category", options.Category.Value.ToString());
        }

        if (options.Status.HasValue)
        {
            conditions.Add("c.Status = @status");
            queryDef = queryDef.WithParameter("@status", options.Status.Value.ToString());
        }

        if (options.FromDate.HasValue)
        {
            conditions.Add("c.MeetingDate >= @fromDate");
            queryDef = queryDef.WithParameter("@fromDate", options.FromDate.Value);
        }

        if (options.ToDate.HasValue)
        {
            conditions.Add("c.MeetingDate <= @toDate");
            queryDef = queryDef.WithParameter("@toDate", options.ToDate.Value);
        }

        if (!string.IsNullOrWhiteSpace(query))
        {
            conditions.Add("(CONTAINS(LOWER(c.Title), @query) OR CONTAINS(LOWER(c.Content), @query))");
            queryDef = queryDef.WithParameter("@query", query.ToLowerInvariant());
        }

        var whereClause = conditions.Count > 0
            ? "WHERE " + string.Join(" AND ", conditions)
            : string.Empty;

        var sql = $"SELECT * FROM c {whereClause} ORDER BY c.CreatedAt DESC OFFSET 0 LIMIT @limit";
        queryDef = new QueryDefinition(sql);

        // Re-add parameters since QueryDefinition is immutable per constructor
        if (!string.IsNullOrEmpty(options.TenantId))
            queryDef = queryDef.WithParameter("@tenantId", options.TenantId);
        if (options.Category.HasValue)
            queryDef = queryDef.WithParameter("@category", options.Category.Value.ToString());
        if (options.Status.HasValue)
            queryDef = queryDef.WithParameter("@status", options.Status.Value.ToString());
        if (options.FromDate.HasValue)
            queryDef = queryDef.WithParameter("@fromDate", options.FromDate.Value);
        if (options.ToDate.HasValue)
            queryDef = queryDef.WithParameter("@toDate", options.ToDate.Value);
        if (!string.IsNullOrWhiteSpace(query))
            queryDef = queryDef.WithParameter("@query", query.ToLowerInvariant());
        queryDef = queryDef.WithParameter("@limit", options.MaxResults);

        var results = new List<KnowledgeEntry>();
        using var iterator = _container.GetItemQueryIterator<KnowledgeEntry>(queryDef);
        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync(ct);
            results.AddRange(response);
        }
        return results;
    }

    public async Task DeleteKnowledgeAsync(string id, CancellationToken ct)
    {
        var entry = await GetKnowledgeAsync(id, ct);
        if (entry is not null)
        {
            await _container.DeleteItemAsync<KnowledgeEntry>(id, new PartitionKey(entry.TenantId), cancellationToken: ct);
            _logger.LogInformation("Deleted knowledge entry {Id}", id);
        }
    }

    public async Task<IReadOnlyList<KnowledgeEntry>> GetBySessionAsync(string sessionId, CancellationToken ct)
    {
        var query = new QueryDefinition("SELECT * FROM c WHERE c.SessionId = @sessionId ORDER BY c.CreatedAt DESC")
            .WithParameter("@sessionId", sessionId);

        var results = new List<KnowledgeEntry>();
        using var iterator = _container.GetItemQueryIterator<KnowledgeEntry>(query);
        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync(ct);
            results.AddRange(response);
        }
        return results;
    }

    public async Task<KnowledgeStoreStats> GetStatsAsync(string tenantId, CancellationToken ct)
    {
        var query = new QueryDefinition(
            "SELECT c.Status, COUNT(1) AS Count FROM c WHERE c.TenantId = @tenantId GROUP BY c.Status")
            .WithParameter("@tenantId", tenantId);

        var statusCounts = new Dictionary<string, int>();
        using var iterator = _container.GetItemQueryIterator<dynamic>(query);
        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync(ct);
            foreach (var item in response)
            {
                string status = item.Status?.ToString() ?? "Draft";
                int count = (int)(item.Count ?? 0);
                statusCounts[status] = count;
            }
        }

        return new KnowledgeStoreStats
        {
            TenantId = tenantId,
            TotalEntries = statusCounts.Values.Sum(),
            DraftCount = statusCounts.GetValueOrDefault("Draft"),
            ConfirmedCount = statusCounts.GetValueOrDefault("Confirmed"),
            RejectedCount = statusCounts.GetValueOrDefault("Rejected"),
            ArchivedCount = statusCounts.GetValueOrDefault("Archived"),
            EntriesByCategory = statusCounts
        };
    }
}
