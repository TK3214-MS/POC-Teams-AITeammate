using TeamsAITeammate.Core.Models;

namespace TeamsAITeammate.Core.Interfaces;

public interface IKnowledgeIngestionPipeline
{
    Task<KnowledgeEntry> IngestAsync(
        TacitKnowledgeCandidate candidate,
        IngestionContext context,
        CancellationToken ct);

    Task<KnowledgeEntry> UpdateStatusAsync(
        string id,
        KnowledgeStatus status,
        string? validatedBy,
        string? correctedContent,
        CancellationToken ct);
}
