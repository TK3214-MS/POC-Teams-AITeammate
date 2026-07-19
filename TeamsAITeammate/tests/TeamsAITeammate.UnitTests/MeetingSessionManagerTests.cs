using Microsoft.Extensions.Logging;
using Moq;
using TeamsAITeammate.Core.Interfaces;
using TeamsAITeammate.Core.Models;
using TeamsAITeammate.Infrastructure.Services;

namespace TeamsAITeammate.UnitTests;

public class MeetingSessionManagerTests
{
    private readonly Mock<IMeetingSessionRepository> _repositoryMock = new();
    private readonly Mock<ILogger<MeetingSessionManager>> _loggerMock = new();
    private readonly MeetingSessionManager _manager;

    public MeetingSessionManagerTests()
    {
        _manager = new MeetingSessionManager(_repositoryMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task JoinMeetingAsync_NewMeeting_CreatesActiveSession()
    {
        _repositoryMock.Setup(r => r.GetByMeetingIdAsync("meeting-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync((MeetingSession?)null);

        var session = await _manager.JoinMeetingAsync("meeting-1", "tenant-1", "organizer-1");

        Assert.Equal("meeting-1", session.MeetingId);
        Assert.Equal("tenant-1", session.TenantId);
        Assert.Equal("organizer-1", session.OrganizerId);
        Assert.Equal(SessionState.Active, session.State);
        Assert.Equal(MeetingStatus.InProgress, session.Status);
        Assert.NotNull(session.JoinedAt);
        _repositoryMock.Verify(r => r.UpsertAsync(It.IsAny<MeetingSession>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task JoinMeetingAsync_AlreadyActive_ReturnsExistingSession()
    {
        var existing = new MeetingSession
        {
            MeetingId = "meeting-1",
            TenantId = "tenant-1",
            State = SessionState.Active,
        };

        _repositoryMock.Setup(r => r.GetByMeetingIdAsync("meeting-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var session = await _manager.JoinMeetingAsync("meeting-1", "tenant-1", "organizer-1");

        Assert.Same(existing, session);
        _repositoryMock.Verify(r => r.UpsertAsync(It.IsAny<MeetingSession>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task LeaveMeetingAsync_ActiveSession_CompletesSession()
    {
        var session = new MeetingSession
        {
            Id = "session-1",
            MeetingId = "meeting-1",
            State = SessionState.Active,
        };

        _repositoryMock.Setup(r => r.GetByIdAsync("session-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        await _manager.LeaveMeetingAsync("session-1");

        // Two upserts: Leaving then Completed
        _repositoryMock.Verify(r => r.UpsertAsync(
            It.IsAny<MeetingSession>(),
            It.IsAny<CancellationToken>()), Times.Exactly(2));

        // Final state should be Completed
        Assert.Equal(SessionState.Completed, session.State);
        Assert.Equal(MeetingStatus.Ended, session.Status);
        Assert.NotNull(session.EndedAt);
    }

    [Fact]
    public async Task LeaveMeetingAsync_NonExistentSession_DoesNotThrow()
    {
        _repositoryMock.Setup(r => r.GetByIdAsync("nonexistent", It.IsAny<CancellationToken>()))
            .ReturnsAsync((MeetingSession?)null);

        await _manager.LeaveMeetingAsync("nonexistent");

        _repositoryMock.Verify(r => r.UpsertAsync(It.IsAny<MeetingSession>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(SessionState.Active)]
    [InlineData(SessionState.Analyzing)]
    [InlineData(SessionState.Paused)]
    public async Task GetActiveSessionAsync_WithActiveState_ReturnsSession(SessionState state)
    {
        var session = new MeetingSession
        {
            MeetingId = "meeting-1",
            State = state,
        };

        _repositoryMock.Setup(r => r.GetByMeetingIdAsync("meeting-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var result = await _manager.GetActiveSessionAsync("meeting-1");

        Assert.NotNull(result);
        Assert.Equal(state, result.State);
    }

    [Theory]
    [InlineData(SessionState.Completed)]
    [InlineData(SessionState.Leaving)]
    [InlineData(SessionState.Joining)]
    public async Task GetActiveSessionAsync_WithInactiveState_ReturnsNull(SessionState state)
    {
        var session = new MeetingSession
        {
            MeetingId = "meeting-1",
            State = state,
        };

        _repositoryMock.Setup(r => r.GetByMeetingIdAsync("meeting-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var result = await _manager.GetActiveSessionAsync("meeting-1");

        Assert.Null(result);
    }

    [Fact]
    public async Task GetActiveSessionAsync_NoSession_ReturnsNull()
    {
        _repositoryMock.Setup(r => r.GetByMeetingIdAsync("meeting-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync((MeetingSession?)null);

        var result = await _manager.GetActiveSessionAsync("meeting-1");

        Assert.Null(result);
    }

    [Fact]
    public async Task UpdateSessionStateAsync_ExistingSession_UpdatesState()
    {
        var session = new MeetingSession
        {
            Id = "session-1",
            State = SessionState.Active,
        };

        _repositoryMock.Setup(r => r.GetByIdAsync("session-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        await _manager.UpdateSessionStateAsync("session-1", SessionState.Paused);

        _repositoryMock.Verify(r => r.UpsertAsync(
            It.Is<MeetingSession>(s => s.State == SessionState.Paused),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateSessionStateAsync_NonExistentSession_DoesNotThrow()
    {
        _repositoryMock.Setup(r => r.GetByIdAsync("nonexistent", It.IsAny<CancellationToken>()))
            .ReturnsAsync((MeetingSession?)null);

        await _manager.UpdateSessionStateAsync("nonexistent", SessionState.Paused);

        _repositoryMock.Verify(r => r.UpsertAsync(It.IsAny<MeetingSession>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetActiveSessionsAsync_ReturnsEmptyList()
    {
        var result = await _manager.GetActiveSessionsAsync();

        Assert.Empty(result);
    }
}
