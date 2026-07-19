using Azure.Identity;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using TeamsAITeammate.Core.Interfaces;
using TeamsAITeammate.Core.Models;

namespace TeamsAITeammate.Infrastructure.Repositories;

public class CosmosTranscriptRepository : ITranscriptRepository
{
    private readonly Container _container;

    public CosmosTranscriptRepository(IConfiguration configuration)
    {
        var endpoint = configuration["CosmosDb:Endpoint"]!;
        var databaseName = configuration["CosmosDb:DatabaseName"]!;
        var containerName = configuration["CosmosDb:TranscriptsContainer"]!;

        var client = new CosmosClient(endpoint, new DefaultAzureCredential());
        _container = client.GetContainer(databaseName, containerName);
    }

    public async Task AddAsync(TranscriptEntry entry, CancellationToken ct = default)
    {
        await _container.CreateItemAsync(entry, new PartitionKey(entry.SessionId), cancellationToken: ct);
    }

    public async Task<IReadOnlyList<TranscriptEntry>> GetBySessionAsync(string sessionId, CancellationToken ct = default)
    {
        var query = new QueryDefinition("SELECT * FROM c WHERE c.SessionId = @sessionId ORDER BY c.Timestamp ASC")
            .WithParameter("@sessionId", sessionId);

        var results = new List<TranscriptEntry>();
        using var iterator = _container.GetItemQueryIterator<TranscriptEntry>(query);
        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync(ct);
            results.AddRange(response);
        }
        return results;
    }
}
