namespace TeamsAITeammate.Core.Models;

public record CommandResult
{
    public string Command { get; init; } = string.Empty;
    public string? Argument { get; init; }
    public bool IsRecognized { get; init; }
    public string OriginalText { get; init; } = string.Empty;
}
