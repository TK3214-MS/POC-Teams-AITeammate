using TeamsAITeammate.Core.Models;

namespace TeamsAITeammate.Core.Interfaces;

public interface ITranscriptPersistence
{
    Task AppendSegmentAsync(string tenantId, string meetingId, string sessionId,
        TranscriptSegment segment, CancellationToken ct = default);

    Task FlushAsync(string sessionId, CancellationToken ct = default);

    Task FinalizeAsync(string sessionId, CancellationToken ct = default);
}
