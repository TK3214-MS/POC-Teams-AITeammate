namespace TeamsAITeammate.Core.Models;

public record TranscriptEntry
{
    public string Id { get; init; } = Guid.NewGuid().ToString();
    public string SessionId { get; init; } = string.Empty;
    public string SpeakerId { get; init; } = string.Empty;
    public string SpeakerName { get; init; } = string.Empty;
    public string Text { get; init; } = string.Empty;
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
    public double Confidence { get; init; }
    public string Language { get; init; } = "ja-JP";
}
