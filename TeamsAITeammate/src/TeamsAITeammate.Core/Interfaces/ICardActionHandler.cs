using TeamsAITeammate.Core.Models;

namespace TeamsAITeammate.Core.Interfaces;

public interface ICardActionHandler
{
    Task<CardActionResult> HandleActionAsync(string actionVerb, IDictionary<string, object> data, string sessionId, CancellationToken ct);
}

public record CardActionResult
{
    public bool Success { get; init; }
    public string? Message { get; init; }
    public string? UpdatedCardJson { get; init; }
}
