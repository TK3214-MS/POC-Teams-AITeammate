using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TeamsAITeammate.Core.Interfaces;
using TeamsAITeammate.Core.Models;

namespace TeamsAITeammate.Infrastructure.Services;

/// <summary>エージェント設定のCosmos DB永続化</summary>
public class CosmosAgentSettingsRepository : IAgentSettingsRepository
{
    private readonly Container _container;
    private readonly ILogger<CosmosAgentSettingsRepository> _logger;

    public CosmosAgentSettingsRepository(CosmosClient cosmosClient, IConfiguration config,
        ILogger<CosmosAgentSettingsRepository> logger)
    {
        var dbName = config["CosmosDb:DatabaseName"] ?? "TeamsAITeammate";
        _container = cosmosClient.GetContainer(dbName, "settings");
        _logger = logger;
    }

    public async Task<AgentSettings?> GetAsync(string tenantId, CancellationToken ct = default)
    {
        try
        {
            var response = await _container.ReadItemAsync<AgentSettings>(
                tenantId, new PartitionKey(tenantId), cancellationToken: ct);
            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get agent settings for tenant {TenantId}", tenantId);
            throw;
        }
    }

    public async Task SaveAsync(AgentSettings settings, CancellationToken ct = default)
    {
        await _container.UpsertItemAsync(settings,
            new PartitionKey(settings.TenantId), cancellationToken: ct);
    }
}

/// <summary>監査ログのCosmos DB実装</summary>
public class CosmosAuditLogService : IAuditLogService
{
    private readonly Container _container;
    private readonly ILogger<CosmosAuditLogService> _logger;

    public CosmosAuditLogService(CosmosClient cosmosClient, IConfiguration config,
        ILogger<CosmosAuditLogService> logger)
    {
        var dbName = config["CosmosDb:DatabaseName"] ?? "TeamsAITeammate";
        _container = cosmosClient.GetContainer(dbName, "audit-logs");
        _logger = logger;
    }

    public async Task LogAsync(string tenantId, string userId, string action,
        string resourceType, string resourceId, string? details = null, CancellationToken ct = default)
    {
        var entry = new AuditLogEntry
        {
            TenantId = tenantId,
            UserId = userId,
            Action = action,
            ResourceType = resourceType,
            ResourceId = resourceId,
            Details = details
        };

        await _container.CreateItemAsync(entry, new PartitionKey(tenantId), cancellationToken: ct);
        _logger.LogInformation("Audit: {Action} on {ResourceType}/{ResourceId} by {UserId} in tenant {TenantId}",
            action, resourceType, resourceId, userId, tenantId);
    }

    public async Task<IReadOnlyList<AuditLogEntry>> GetLogsAsync(string tenantId,
        DateTimeOffset? from = null, DateTimeOffset? to = null, int maxResults = 100, CancellationToken ct = default)
    {
        var query = "SELECT * FROM c WHERE c.TenantId = @tenantId";
        if (from.HasValue) query += " AND c.Timestamp >= @from";
        if (to.HasValue) query += " AND c.Timestamp <= @to";
        query += " ORDER BY c.Timestamp DESC OFFSET 0 LIMIT @limit";

        var queryDef = new QueryDefinition(query)
            .WithParameter("@tenantId", tenantId)
            .WithParameter("@limit", maxResults);
        if (from.HasValue) queryDef = queryDef.WithParameter("@from", from.Value);
        if (to.HasValue) queryDef = queryDef.WithParameter("@to", to.Value);

        var results = new List<AuditLogEntry>();
        using var iterator = _container.GetItemQueryIterator<AuditLogEntry>(queryDef,
            requestOptions: new QueryRequestOptions { PartitionKey = new PartitionKey(tenantId) });

        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync(ct);
            results.AddRange(response);
        }

        return results;
    }
}

/// <summary>テナントユーザーのCosmos DB実装</summary>
public class CosmosTenantUserRepository : ITenantUserRepository
{
    private readonly Container _container;

    public CosmosTenantUserRepository(CosmosClient cosmosClient, IConfiguration config)
    {
        var dbName = config["CosmosDb:DatabaseName"] ?? "TeamsAITeammate";
        _container = cosmosClient.GetContainer(dbName, "users");
    }

    public async Task<IReadOnlyList<TenantUser>> GetUsersAsync(string tenantId, CancellationToken ct = default)
    {
        var query = new QueryDefinition("SELECT * FROM c WHERE c.TenantId = @tenantId")
            .WithParameter("@tenantId", tenantId);

        var results = new List<TenantUser>();
        using var iterator = _container.GetItemQueryIterator<TenantUser>(query,
            requestOptions: new QueryRequestOptions { PartitionKey = new PartitionKey(tenantId) });

        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync(ct);
            results.AddRange(response);
        }

        return results;
    }

    public async Task<TenantUser?> GetUserAsync(string tenantId, string userId, CancellationToken ct = default)
    {
        try
        {
            var response = await _container.ReadItemAsync<TenantUser>(
                userId, new PartitionKey(tenantId), cancellationToken: ct);
            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task SaveUserAsync(TenantUser user, CancellationToken ct = default)
    {
        await _container.UpsertItemAsync(user,
            new PartitionKey(user.TenantId), cancellationToken: ct);
    }
}

/// <summary>ダッシュボード統計サービス</summary>
public class DashboardService : IDashboardService
{
    private readonly IKnowledgeRepository _knowledgeRepo;
    private readonly IMeetingSessionRepository _sessionRepo;
    private readonly ILogger<DashboardService> _logger;

    public DashboardService(IKnowledgeRepository knowledgeRepo, IMeetingSessionRepository sessionRepo,
        ILogger<DashboardService> logger)
    {
        _knowledgeRepo = knowledgeRepo;
        _sessionRepo = sessionRepo;
        _logger = logger;
    }

    public async Task<DashboardStats> GetStatsAsync(string tenantId, CancellationToken ct = default)
    {
        // Get knowledge entries for stats
        var entries = await _knowledgeRepo.GetByTenantAsync(tenantId, ct);
        var sessions = await _sessionRepo.GetByTenantAsync(tenantId, ct: ct);

        var byCategory = entries
            .GroupBy(e => e.Category.ToString())
            .ToDictionary(g => g.Key, g => g.Count());

        var now = DateTimeOffset.UtcNow;

        return new DashboardStats
        {
            TenantId = tenantId,
            TotalKnowledgeEntries = entries.Count,
            TotalMeetingSessions = sessions.Count,
            TotalAnalysisExecutions = sessions.Count, // 1:1 with sessions
            ActiveUsers = sessions
                .Where(s => s.CreatedAt > now.AddDays(-30))
                .SelectMany(s => s.Participants ?? [])
                .Distinct()
                .Count(),
            KnowledgeByCategory = byCategory
        };
    }
}
