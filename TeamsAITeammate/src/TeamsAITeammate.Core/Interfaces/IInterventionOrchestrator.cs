using TeamsAITeammate.Core.Models;

namespace TeamsAITeammate.Core.Interfaces;

public interface IInterventionOrchestrator
{
    Task<InterventionAction?> EvaluateAsync(
        string sessionId,
        InterventionTrigger trigger,
        ConversationAnalysis? analysis,
        CancellationToken ct);

    Task ExecuteAsync(InterventionAction action, CancellationToken ct);
    Task PauseAsync(string sessionId, CancellationToken ct);
    Task ResumeAsync(string sessionId, CancellationToken ct);
    bool IsPaused(string sessionId);
}
