using Azure.Identity;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using TeamsAITeammate.Core.Interfaces;
using TeamsAITeammate.Core.Models;

namespace TeamsAITeammate.Infrastructure.Repositories;

public class CosmosKnowledgeRepository : IKnowledgeRepository
{
    private readonly Container _container;

    public CosmosKnowledgeRepository(IConfiguration configuration)
    {
        var endpoint = configuration["CosmosDb:Endpoint"]!;
        var databaseName = configuration["CosmosDb:DatabaseName"]!;
        var containerName = configuration["CosmosDb:KnowledgeContainer"]!;

        var client = new CosmosClient(endpoint, new DefaultAzureCredential());
        _container = client.GetContainer(databaseName, containerName);
    }

    public async Task UpsertAsync(KnowledgeEntry entry, CancellationToken ct = default)
    {
        await _container.UpsertItemAsync(entry, new PartitionKey(entry.TenantId), cancellationToken: ct);
    }

    public async Task<IReadOnlyList<KnowledgeEntry>> SearchAsync(string tenantId, string query, int limit = 10, CancellationToken ct = default)
    {
        var cosmosQuery = new QueryDefinition("SELECT * FROM c WHERE c.TenantId = @tenantId ORDER BY c.CreatedAt DESC OFFSET 0 LIMIT @limit")
            .WithParameter("@tenantId", tenantId)
            .WithParameter("@limit", limit);

        var results = new List<KnowledgeEntry>();
        using var iterator = _container.GetItemQueryIterator<KnowledgeEntry>(cosmosQuery);
        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync(ct);
            results.AddRange(response);
        }
        return results;
    }

    public async Task<IReadOnlyList<KnowledgeEntry>> GetBySessionAsync(string sessionId, CancellationToken ct = default)
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
}
