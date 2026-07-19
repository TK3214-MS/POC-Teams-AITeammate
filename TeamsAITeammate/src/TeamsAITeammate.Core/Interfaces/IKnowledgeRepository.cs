using TeamsAITeammate.Core.Models;

namespace TeamsAITeammate.Core.Interfaces;

public interface IKnowledgeRepository
{
    Task UpsertAsync(KnowledgeEntry entry, CancellationToken ct = default);
    Task<KnowledgeEntry?> GetByIdAsync(string id, string tenantId, CancellationToken ct = default);
    Task<IReadOnlyList<KnowledgeEntry>> GetByTenantAsync(string tenantId, CancellationToken ct = default);
    Task<IReadOnlyList<KnowledgeEntry>> SearchAsync(string tenantId, string query, int limit = 10, CancellationToken ct = default);
    Task<IReadOnlyList<KnowledgeEntry>> GetBySessionAsync(string sessionId, CancellationToken ct = default);
    Task DeleteAsync(string id, string tenantId, CancellationToken ct = default);
}
