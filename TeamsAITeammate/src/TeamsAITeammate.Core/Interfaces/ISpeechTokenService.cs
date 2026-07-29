namespace TeamsAITeammate.Core.Interfaces;

public interface ISpeechTokenService
{
    Task<SpeechAuthorization> GetAuthorizationAsync(CancellationToken ct = default);
}

public record SpeechAuthorization(
    string Token,
    string Region,
    DateTimeOffset ExpiresAt);