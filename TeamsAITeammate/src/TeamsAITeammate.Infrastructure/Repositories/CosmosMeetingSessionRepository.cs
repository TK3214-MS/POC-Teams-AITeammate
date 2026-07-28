using Azure.Identity;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using TeamsAITeammate.Core.Interfaces;
using TeamsAITeammate.Core.Models;

namespace TeamsAITeammate.Infrastructure.Repositories;

public class CosmosMeetingSessionRepository : IMeetingSessionRepository
{
    private readonly Container _container;

    public CosmosMeetingSessionRepository(IConfiguration configuration)
    {
        var endpoint = configuration["CosmosDb:Endpoint"]!;
        var databaseName = configuration["CosmosDb:DatabaseName"]!;
        var containerName = configuration["CosmosDb:SessionsContainer"]!;

        var client = new CosmosClient(endpoint, new DefaultAzureCredential());
        _container = client.GetContainer(databaseName, containerName);
    }

    public async Task<MeetingSession?> GetByIdAsync(string sessionId, CancellationToken ct = default)
    {
        try
        {
            var response = await _container.ReadItemAsync<MeetingSession>(sessionId, new PartitionKey(sessionId), cancellationToken: ct);
            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<MeetingSession?> GetByMeetingIdAsync(string meetingId, CancellationToken ct = default)
    {
        var query = new QueryDefinition("SELECT * FROM c WHERE c.MeetingId = @meetingId")
            .WithParameter("@meetingId", meetingId);

        using var iterator = _container.GetItemQueryIterator<MeetingSession>(query);
        MeetingSession? latestSession = null;
        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync(ct);
            foreach (var session in response)
            {
                if (latestSession is null || session.CreatedAt > latestSession.CreatedAt)
                    latestSession = session;
            }
        }
        return latestSession;
    }

    public async Task<IReadOnlyList<MeetingSession>> GetActiveAsync(CancellationToken ct = default)
    {
        var query = new QueryDefinition(
                "SELECT * FROM c WHERE (c.State = @active OR c.State = @analyzing OR c.State = @paused) " +
                "AND NOT CONTAINS(c.MeetingId, @legacyConversationMarker)")
            .WithParameter("@active", SessionState.Active)
            .WithParameter("@analyzing", SessionState.Analyzing)
            .WithParameter("@paused", SessionState.Paused)
            .WithParameter("@legacyConversationMarker", ";messageid=");

        var results = new List<MeetingSession>();
        using var iterator = _container.GetItemQueryIterator<MeetingSession>(query);
        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync(ct);
            results.AddRange(response);
        }
        return results;
    }

    public async Task<IReadOnlyList<MeetingSession>> GetByTenantAsync(string tenantId, int limit = 50, CancellationToken ct = default)
    {
        var query = new QueryDefinition("SELECT * FROM c WHERE c.TenantId = @tenantId ORDER BY c.CreatedAt DESC OFFSET 0 LIMIT @limit")
            .WithParameter("@tenantId", tenantId)
            .WithParameter("@limit", limit);

        var results = new List<MeetingSession>();
        using var iterator = _container.GetItemQueryIterator<MeetingSession>(query);
        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync(ct);
            results.AddRange(response);
        }
        return results;
    }

    public async Task UpsertAsync(MeetingSession session, CancellationToken ct = default)
    {
        await _container.UpsertItemAsync(session, new PartitionKey(session.Id), cancellationToken: ct);
    }

    public async Task DeleteAsync(string sessionId, CancellationToken ct = default)
    {
        await _container.DeleteItemAsync<MeetingSession>(sessionId, new PartitionKey(sessionId), cancellationToken: ct);
    }
}
