namespace TeamsAITeammate.Core.Models;

public record MeetingSession
{
    public string Id { get; init; } = Guid.NewGuid().ToString();
    public string TenantId { get; init; } = string.Empty;
    public string MeetingId { get; init; } = string.Empty;
    public string OrganizerId { get; init; } = string.Empty;
    public string Subject { get; init; } = string.Empty;
    public List<Participant> Participants { get; init; } = [];
    public MeetingStatus Status { get; set; } = MeetingStatus.Scheduled;
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? EndedAt { get; set; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}

public record Participant
{
    public string UserId { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public ParticipantRole Role { get; init; } = ParticipantRole.Attendee;
}

public enum MeetingStatus
{
    Scheduled,
    InProgress,
    Ended,
    Cancelled
}

public enum ParticipantRole
{
    Organizer,
    Presenter,
    Attendee
}
