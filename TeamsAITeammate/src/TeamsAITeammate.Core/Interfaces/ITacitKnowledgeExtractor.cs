using TeamsAITeammate.Core.Models;

namespace TeamsAITeammate.Core.Interfaces;

public interface ITacitKnowledgeExtractor
{
    Task<IReadOnlyList<TacitKnowledgeCandidate>> ExtractAsync(
        ConversationWindow conversation,
        AnalysisContext context,
        CancellationToken ct = default);
}
