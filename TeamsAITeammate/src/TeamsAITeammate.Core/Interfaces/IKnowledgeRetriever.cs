using TeamsAITeammate.Core.Models;

namespace TeamsAITeammate.Core.Interfaces;

public interface IKnowledgeRetriever
{
    Task<IReadOnlyList<RetrievalResult>> RetrieveAsync(
        RetrievalQuery query,
        CancellationToken ct);
}
