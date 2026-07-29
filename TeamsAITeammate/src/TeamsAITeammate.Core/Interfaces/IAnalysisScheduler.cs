using TeamsAITeammate.Core.Models;

namespace TeamsAITeammate.Core.Interfaces;

public interface IAnalysisScheduler
{
    event Func<string, ConversationAnalysis, Task>? OnAnalysisCompleted;

    Task StartAsync(string sessionId, CancellationToken ct = default);
    Task StopAsync(string sessionId, CancellationToken ct = default);
    Task RequestAnalysisAsync(string sessionId, string trigger, CancellationToken ct = default);
}
