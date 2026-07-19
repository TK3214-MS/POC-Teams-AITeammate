using TeamsAITeammate.Core.Models;

namespace TeamsAITeammate.Core.Interfaces;

public interface IKnowledgeQualityService
{
    Task<IReadOnlyList<KnowledgeEntry>> DetectStaleKnowledgeAsync(
        string tenantId, TimeSpan staleThreshold, CancellationToken ct);

    Task<IReadOnlyList<KnowledgeConflict>> DetectConflictsAsync(
        KnowledgeEntry newEntry, CancellationToken ct);

    Task<IReadOnlyList<MergeSuggestion>> SuggestMergesAsync(
        string tenantId, CancellationToken ct);
}
