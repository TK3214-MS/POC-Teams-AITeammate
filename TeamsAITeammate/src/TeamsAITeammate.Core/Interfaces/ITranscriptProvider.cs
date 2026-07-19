using TeamsAITeammate.Core.Models;

namespace TeamsAITeammate.Core.Interfaces;

public interface ITranscriptProvider
{
    string ProviderName { get; }

    IAsyncEnumerable<TranscriptSegment> StreamTranscriptAsync(
        string meetingId,
        TranscriptStreamOptions options,
        CancellationToken ct = default);

    Task<bool> IsAvailableAsync(string meetingId, CancellationToken ct = default);

    Task<IReadOnlyList<TranscriptSegment>> GetFullTranscriptAsync(
        string meetingId,
        CancellationToken ct = default);
}
