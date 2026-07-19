namespace TeamsAITeammate.Core.Models;

public record InterventionSettings
{
    public TimeSpan SilenceThreshold { get; init; } = TimeSpan.FromSeconds(30);
    public TimeSpan PeriodicInterval { get; init; } = TimeSpan.FromMinutes(5);
    public bool EnableProactiveIntervention { get; init; } = true;
    public int MaxInterventionsPerMeeting { get; init; } = 20;
}

public record SilenceDetectedEvent(string SessionId, TimeSpan Duration);

public record TopicChangeEvent(string SessionId, string PreviousTopic, string NewTopic);

public record PeriodicAnalysisEvent(string SessionId, DateTimeOffset Timestamp);
