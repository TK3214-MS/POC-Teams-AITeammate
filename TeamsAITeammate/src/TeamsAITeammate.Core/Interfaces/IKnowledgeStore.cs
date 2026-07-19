using TeamsAITeammate.Core.Models;

namespace TeamsAITeammate.Core.Interfaces;

public interface IKnowledgeStore
{
    string ProviderName { get; }

    Task<string> SaveKnowledgeAsync(KnowledgeEntry entry, CancellationToken ct);

    Task UpdateKnowledgeAsync(string id, KnowledgeEntry entry, CancellationToken ct);

    Task<KnowledgeEntry?> GetKnowledgeAsync(string id, CancellationToken ct);

    Task<IReadOnlyList<KnowledgeEntry>> SearchAsync(
        string query,
        KnowledgeSearchOptions options,
        CancellationToken ct);

    Task DeleteKnowledgeAsync(string id, CancellationToken ct);

    Task<IReadOnlyList<KnowledgeEntry>> GetBySessionAsync(
        string sessionId,
        CancellationToken ct);

    Task<KnowledgeStoreStats> GetStatsAsync(string tenantId, CancellationToken ct);
}
