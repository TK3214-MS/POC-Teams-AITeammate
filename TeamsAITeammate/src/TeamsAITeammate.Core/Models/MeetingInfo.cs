namespace TeamsAITeammate.Core.Models;

public record MeetingInfo
{
    public string Id { get; init; } = string.Empty;
    public string Subject { get; init; } = string.Empty;
    public string JoinUrl { get; init; } = string.Empty;
    public DateTimeOffset? StartDateTime { get; init; }
    public DateTimeOffset? EndDateTime { get; init; }
    public string ChatId { get; init; } = string.Empty;
    public MeetingParticipantInfo? Organizer { get; init; }
}

public record MeetingParticipantInfo
{
    public string UserId { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public ParticipantRole Role { get; init; }
}
