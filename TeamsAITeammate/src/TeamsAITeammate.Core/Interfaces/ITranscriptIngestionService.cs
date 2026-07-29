using TeamsAITeammate.Core.Models;

namespace TeamsAITeammate.Core.Interfaces;

public interface ITranscriptIngestionService
{
    Task AppendAsync(
        MeetingSession session,
        TranscriptSegment segment,
        CancellationToken ct = default);
}