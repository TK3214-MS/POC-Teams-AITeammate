using TeamsAITeammate.Core.Models;

namespace TeamsAITeammate.Core.Interfaces;

public interface INotificationThrottler
{
    Task<bool> CanSendAsync(string sessionId, InterventionType type, CancellationToken ct);
    Task RecordSentAsync(string sessionId, InterventionType type, CancellationToken ct);
    Task ResetAsync(string sessionId, CancellationToken ct);
}
