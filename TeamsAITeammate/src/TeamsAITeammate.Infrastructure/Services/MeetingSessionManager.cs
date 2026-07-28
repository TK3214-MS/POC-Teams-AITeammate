using Microsoft.Extensions.Logging;
using TeamsAITeammate.Core.Interfaces;
using TeamsAITeammate.Core.Models;

namespace TeamsAITeammate.Infrastructure.Services;

public class MeetingSessionManager : IMeetingSessionManager
{
    private readonly IMeetingSessionRepository _repository;
    private readonly ILogger<MeetingSessionManager> _logger;

    public MeetingSessionManager(IMeetingSessionRepository repository, ILogger<MeetingSessionManager> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<MeetingSession> JoinMeetingAsync(string meetingId, string tenantId, string organizerId, CancellationToken ct = default)
    {
        var existing = await _repository.GetByMeetingIdAsync(meetingId, ct);
        if (existing is not null && existing.State is SessionState.Active or SessionState.Analyzing)
        {
            _logger.LogWarning("Already joined meeting {MeetingId}, returning existing session", meetingId);
            return existing;
        }

        var session = new MeetingSession
        {
            MeetingId = meetingId,
            TenantId = tenantId,
            OrganizerId = organizerId,
            Status = MeetingStatus.InProgress,
            State = SessionState.Joining,
            StartedAt = DateTimeOffset.UtcNow,
            JoinedAt = DateTimeOffset.UtcNow,
        };

        await _repository.UpsertAsync(session, ct);
        _logger.LogInformation("Joining meeting {MeetingId} with session {SessionId}", meetingId, session.Id);

        session.State = SessionState.Active;
        await _repository.UpsertAsync(session, ct);
        _logger.LogInformation("Session {SessionId} is now active", session.Id);

        return session;
    }

    public async Task LeaveMeetingAsync(string sessionId, CancellationToken ct = default)
    {
        var session = await _repository.GetByIdAsync(sessionId, ct);
        if (session is null)
        {
            _logger.LogWarning("Session {SessionId} not found for leave", sessionId);
            return;
        }

        session.State = SessionState.Leaving;
        await _repository.UpsertAsync(session, ct);

        session.State = SessionState.Completed;
        session.Status = MeetingStatus.Ended;
        session.EndedAt = DateTimeOffset.UtcNow;
        await _repository.UpsertAsync(session, ct);

        _logger.LogInformation("Left meeting session {SessionId}", sessionId);
    }

    public async Task<MeetingSession?> GetActiveSessionAsync(string meetingId, CancellationToken ct = default)
    {
        var session = await _repository.GetByMeetingIdAsync(meetingId, ct);
        if (session?.State is SessionState.Active or SessionState.Analyzing or SessionState.Paused)
            return session;
        return null;
    }

    public async Task<IReadOnlyList<MeetingSession>> GetActiveSessionsAsync(CancellationToken ct = default)
    {
        return await _repository.GetActiveAsync(ct);
    }

    public async Task UpdateSessionStateAsync(string sessionId, SessionState state, CancellationToken ct = default)
    {
        var session = await _repository.GetByIdAsync(sessionId, ct);
        if (session is null)
        {
            _logger.LogWarning("Session {SessionId} not found for state update", sessionId);
            return;
        }

        session.State = state;
        await _repository.UpsertAsync(session, ct);
        _logger.LogInformation("Updated session {SessionId} state to {State}", sessionId, state);
    }
}
