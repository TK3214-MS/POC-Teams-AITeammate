using TeamsAITeammate.Core.Models;

namespace TeamsAITeammate.Core.Interfaces;

public interface IMeetingSessionRepository
{
    Task<MeetingSession?> GetByIdAsync(string sessionId, CancellationToken ct = default);
    Task<MeetingSession?> GetByMeetingIdAsync(string meetingId, CancellationToken ct = default);
    Task<IReadOnlyList<MeetingSession>> GetActiveAsync(CancellationToken ct = default);
    Task<IReadOnlyList<MeetingSession>> GetByTenantAsync(string tenantId, int limit = 50, CancellationToken ct = default);
    Task UpsertAsync(MeetingSession session, CancellationToken ct = default);
    Task DeleteAsync(string sessionId, CancellationToken ct = default);
}
