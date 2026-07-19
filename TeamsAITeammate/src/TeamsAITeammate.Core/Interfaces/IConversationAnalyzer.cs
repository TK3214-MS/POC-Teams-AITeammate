using TeamsAITeammate.Core.Models;

namespace TeamsAITeammate.Core.Interfaces;

public interface IConversationAnalyzer
{
    Task<ConversationAnalysis> AnalyzeAsync(
        ConversationWindow conversation,
        AnalysisContext context,
        CancellationToken ct = default);
}
