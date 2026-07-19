using TeamsAITeammate.Core.Models;

namespace TeamsAITeammate.Core.Interfaces;

public interface ILanguageDetector
{
    Task<LanguageDetectionResult> DetectLanguageAsync(
        IReadOnlyList<TranscriptSegment> segments,
        CancellationToken ct = default);
}
