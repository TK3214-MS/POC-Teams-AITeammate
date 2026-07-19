using TeamsAITeammate.Core.Models;

namespace TeamsAITeammate.Core.Interfaces;

public interface IKnowledgeRepository
{
    Task UpsertAsync(KnowledgeEntry entry, CancellationToken ct = default);
    Task<IReadOnlyList<KnowledgeEntry>> SearchAsync(string tenantId, string query, int limit = 10, CancellationToken ct = default);
    Task<IReadOnlyList<KnowledgeEntry>> GetBySessionAsync(string sessionId, CancellationToken ct = default);
}
