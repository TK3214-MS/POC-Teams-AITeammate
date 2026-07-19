using TeamsAITeammate.Core.Models;

namespace TeamsAITeammate.Core.Interfaces;

/// <summary>エージェント設定の永続化</summary>
public interface IAgentSettingsRepository
{
    Task<AgentSettings?> GetAsync(string tenantId, CancellationToken ct = default);
    Task SaveAsync(AgentSettings settings, CancellationToken ct = default);
}

/// <summary>監査ログ</summary>
public interface IAuditLogService
{
    Task LogAsync(string tenantId, string userId, string action,
        string resourceType, string resourceId, string? details = null, CancellationToken ct = default);
    Task<IReadOnlyList<AuditLogEntry>> GetLogsAsync(string tenantId,
        DateTimeOffset? from = null, DateTimeOffset? to = null, int maxResults = 100, CancellationToken ct = default);
}

/// <summary>テナントユーザー管理</summary>
public interface ITenantUserRepository
{
    Task<IReadOnlyList<TenantUser>> GetUsersAsync(string tenantId, CancellationToken ct = default);
    Task<TenantUser?> GetUserAsync(string tenantId, string userId, CancellationToken ct = default);
    Task SaveUserAsync(TenantUser user, CancellationToken ct = default);
}

/// <summary>ダッシュボード統計</summary>
public interface IDashboardService
{
    Task<DashboardStats> GetStatsAsync(string tenantId, CancellationToken ct = default);
}
