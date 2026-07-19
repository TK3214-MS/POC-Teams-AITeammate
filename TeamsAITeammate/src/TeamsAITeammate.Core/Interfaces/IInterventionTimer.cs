using TeamsAITeammate.Core.Models;

namespace TeamsAITeammate.Core.Interfaces;

public interface IInterventionTimer
{
    event Func<SilenceDetectedEvent, Task>? OnSilenceDetected;
    event Func<TopicChangeEvent, Task>? OnTopicChanged;
    event Func<PeriodicAnalysisEvent, Task>? OnPeriodicAnalysis;

    Task StartAsync(string sessionId, InterventionSettings settings, CancellationToken ct = default);
    Task StopAsync(string sessionId, CancellationToken ct = default);
    Task ResetSilenceTimerAsync(string sessionId, CancellationToken ct = default);
}
