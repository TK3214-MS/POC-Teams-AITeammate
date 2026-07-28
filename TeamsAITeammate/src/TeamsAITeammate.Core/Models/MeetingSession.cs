namespace TeamsAITeammate.Core.Models;

public record MeetingSession
{
    public string Id { get; init; } = Guid.NewGuid().ToString();
    public string id => Id;
    public string TenantId { get; init; } = string.Empty;
    public string MeetingId { get; init; } = string.Empty;
    public string OrganizerId { get; init; } = string.Empty;
    public string Subject { get; init; } = string.Empty;
    public List<Participant> Participants { get; init; } = [];
    public MeetingStatus Status { get; set; } = MeetingStatus.Scheduled;
    public SessionState State { get; set; } = SessionState.Joining;
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? JoinedAt { get; set; }
    public DateTimeOffset? EndedAt { get; set; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public MeetingContext? Context { get; set; }
}

public record Participant
{
    public string UserId { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public ParticipantRole Role { get; init; } = ParticipantRole.Attendee;
}

public record MeetingContext
{
    public string ChatId { get; init; } = string.Empty;
    public string ThreadId { get; init; } = string.Empty;
    public string ServiceUrl { get; init; } = string.Empty;
}

public enum MeetingStatus
{
    Scheduled,
    InProgress,
    Ended,
    Cancelled
}

public enum SessionState
{
    Joining,
    Active,
    Analyzing,
    Paused,
    Leaving,
    Completed
}

public enum ParticipantRole
{
    Organizer,
    Presenter,
    Attendee
}
