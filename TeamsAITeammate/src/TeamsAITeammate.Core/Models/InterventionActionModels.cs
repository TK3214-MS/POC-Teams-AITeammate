namespace TeamsAITeammate.Core.Models;

public record InterventionAction
{
    public string Id { get; init; } = Guid.NewGuid().ToString();
    public InterventionType Type { get; init; }
    public InterventionTrigger Trigger { get; init; }
    public object Content { get; init; } = null!;
    public InterventionPriority Priority { get; init; }
    public DateTimeOffset ScheduledAt { get; init; } = DateTimeOffset.UtcNow;
    public string SessionId { get; init; } = string.Empty;
}

public enum InterventionType
{
    ChatMessage,
    AdaptiveCard,
    SidePanelUpdate,
    ProactiveNotification
}

public enum InterventionTrigger
{
    UserMention,
    SilenceDetected,
    TopicChange,
    PeriodicAnalysis,
    CriticalInsight,
    UserCommand
}

public enum InterventionPriority
{
    Critical,
    High,
    Medium,
    Low
}
