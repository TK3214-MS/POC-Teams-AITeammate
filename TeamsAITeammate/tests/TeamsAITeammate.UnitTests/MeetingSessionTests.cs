using TeamsAITeammate.Core.Models;

namespace TeamsAITeammate.UnitTests;

public class MeetingSessionTests
{
    [Fact]
    public void NewSession_ShouldHaveDefaultValues()
    {
        var session = new MeetingSession();

        Assert.NotNull(session.Id);
        Assert.NotEmpty(session.Id);
        Assert.Equal(MeetingStatus.Scheduled, session.Status);
        Assert.Equal(SessionState.Joining, session.State);
        Assert.Empty(session.Participants);
        Assert.Null(session.JoinedAt);
        Assert.Null(session.Context);
    }

    [Fact]
    public void NewSession_WithProperties_ShouldRetainValues()
    {
        var session = new MeetingSession
        {
            TenantId = "tenant-1",
            MeetingId = "meeting-1",
            Subject = "Sprint Review",
            Status = MeetingStatus.InProgress,
            State = SessionState.Active,
            JoinedAt = DateTimeOffset.UtcNow,
            Context = new MeetingContext { ChatId = "chat-1", ThreadId = "thread-1" },
        };

        Assert.Equal("tenant-1", session.TenantId);
        Assert.Equal("meeting-1", session.MeetingId);
        Assert.Equal("Sprint Review", session.Subject);
        Assert.Equal(MeetingStatus.InProgress, session.Status);
        Assert.Equal(SessionState.Active, session.State);
        Assert.NotNull(session.JoinedAt);
        Assert.Equal("chat-1", session.Context!.ChatId);
    }

    [Fact]
    public void SessionState_ShouldContainExpectedValues()
    {
        var states = Enum.GetValues<SessionState>();

        Assert.Contains(SessionState.Joining, states);
        Assert.Contains(SessionState.Active, states);
        Assert.Contains(SessionState.Analyzing, states);
        Assert.Contains(SessionState.Paused, states);
        Assert.Contains(SessionState.Leaving, states);
        Assert.Contains(SessionState.Completed, states);
    }
}

public class KnowledgeEntryTests
{
    [Fact]
    public void NewKnowledge_ShouldHaveDefaultValues()
    {
        var entry = new KnowledgeEntry();

        Assert.NotNull(entry.Id);
        Assert.Empty(entry.Tags);
        Assert.Equal(0, entry.ConfidenceScore);
        Assert.Null(entry.UpdatedAt);
    }

    [Fact]
    public void KnowledgeType_ShouldContainExpectedValues()
    {
        var types = Enum.GetValues<KnowledgeType>();

        Assert.Contains(KnowledgeType.TacitKnowledge, types);
        Assert.Contains(KnowledgeType.Decision, types);
        Assert.Contains(KnowledgeType.ActionItem, types);
        Assert.Contains(KnowledgeType.Insight, types);
    }
}

public class TranscriptEntryTests
{
    [Fact]
    public void NewTranscript_ShouldHaveDefaultLanguage()
    {
        var entry = new TranscriptEntry();

        Assert.Equal("ja-JP", entry.Language);
    }
}
