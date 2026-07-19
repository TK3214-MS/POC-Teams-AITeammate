using TeamsAITeammate.Core.Models;

namespace TeamsAITeammate.Core.Interfaces;

public interface IAnalysisEngine
{
    Task<IReadOnlyList<KnowledgeEntry>> AnalyzeTranscriptAsync(
        IReadOnlyList<TranscriptEntry> entries,
        MeetingSession session,
        CancellationToken ct = default);

    Task<string> GenerateSummaryAsync(
        IReadOnlyList<TranscriptEntry> entries,
        CancellationToken ct = default);
}
