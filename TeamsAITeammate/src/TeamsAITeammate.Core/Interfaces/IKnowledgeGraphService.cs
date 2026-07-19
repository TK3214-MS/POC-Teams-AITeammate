using TeamsAITeammate.Core.Models;

namespace TeamsAITeammate.Core.Interfaces;

public interface IKnowledgeGraphService
{
    Task AddRelationAsync(string sourceId, string targetId,
        RelationType type, CancellationToken ct);

    Task<IReadOnlyList<KnowledgeEntry>> GetRelatedAsync(
        string knowledgeId, int depth, CancellationToken ct);

    Task<IReadOnlyList<KnowledgeCluster>> DetectClustersAsync(
        string tenantId, CancellationToken ct);
}
