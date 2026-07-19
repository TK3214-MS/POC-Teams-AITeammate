namespace TeamsAITeammate.Core.Models;

public record TranscriptSegment
{
    public string Id { get; init; } = Guid.NewGuid().ToString();
    public string MeetingId { get; init; } = string.Empty;
    public string SpeakerId { get; init; } = string.Empty;
    public string SpeakerName { get; init; } = string.Empty;
    public string Text { get; init; } = string.Empty;
    public string Language { get; init; } = string.Empty;
    public DateTimeOffset Timestamp { get; init; }
    public TimeSpan Duration { get; init; }
    public float Confidence { get; init; }
}

public record TranscriptStreamOptions
{
    public string PreferredLanguage { get; init; } = "auto";
    public bool IncludeSpeakerIdentification { get; init; } = true;
    public TimeSpan BufferInterval { get; init; } = TimeSpan.FromSeconds(3);
}

public record ConversationWindow
{
    public string SessionId { get; init; } = string.Empty;
    public IReadOnlyList<TranscriptSegment> Segments { get; init; } = [];
    public DateTimeOffset WindowStart { get; init; }
    public DateTimeOffset WindowEnd { get; init; }
    public TimeSpan TotalDuration { get; init; }
    public int UniqueSpeakerCount { get; init; }
    public string DetectedLanguage { get; init; } = string.Empty;

    public string ToFormattedTranscript()
    {
        if (Segments.Count == 0)
            return string.Empty;

        return string.Join('\n', Segments.Select(s =>
            $"[{s.Timestamp:HH:mm:ss}] {s.SpeakerName}: {s.Text}"));
    }
}

public record SpeakerStats
{
    public string SpeakerId { get; init; } = string.Empty;
    public string SpeakerName { get; init; } = string.Empty;
    public int SegmentCount { get; init; }
    public TimeSpan TotalSpeakingTime { get; init; }
    public DateTimeOffset LastSpokenAt { get; init; }
}

public record SilencePeriod
{
    public DateTimeOffset Start { get; init; }
    public DateTimeOffset End { get; init; }
    public TimeSpan Duration { get; init; }
}

public record LanguageDetectionResult
{
    public string PrimaryLanguage { get; init; } = string.Empty;
    public float Confidence { get; init; }
    public IReadOnlyDictionary<string, float> LanguageDistribution { get; init; } =
        new Dictionary<string, float>();
}
