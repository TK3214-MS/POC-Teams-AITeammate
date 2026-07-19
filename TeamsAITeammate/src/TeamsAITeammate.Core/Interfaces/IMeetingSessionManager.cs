using TeamsAITeammate.Core.Models;

namespace TeamsAITeammate.Core.Interfaces;

public interface IMeetingSessionManager
{
    Task<MeetingSession> JoinMeetingAsync(string meetingId, string tenantId, string organizerId, CancellationToken ct = default);
    Task LeaveMeetingAsync(string sessionId, CancellationToken ct = default);
    Task<MeetingSession?> GetActiveSessionAsync(string meetingId, CancellationToken ct = default);
    Task<IReadOnlyList<MeetingSession>> GetActiveSessionsAsync(CancellationToken ct = default);
    Task UpdateSessionStateAsync(string sessionId, SessionState state, CancellationToken ct = default);
}
