using TeamsAITeammate.Core.Models;

namespace TeamsAITeammate.Core.Interfaces;

public interface ITranscriptBuffer
{
    Task AppendAsync(TranscriptSegment segment, CancellationToken ct = default);

    Task<ConversationWindow> GetRecentWindowAsync(
        string sessionId,
        TimeSpan window,
        CancellationToken ct = default);

    Task<ConversationWindow> GetFullConversationAsync(
        string sessionId,
        CancellationToken ct = default);

    Task<IReadOnlyDictionary<string, SpeakerStats>> GetSpeakerStatsAsync(
        string sessionId,
        CancellationToken ct = default);

    Task<IReadOnlyList<SilencePeriod>> DetectSilencePeriodsAsync(
        string sessionId,
        TimeSpan threshold,
        CancellationToken ct = default);
}
