using TeamsAITeammate.Core.Models;

namespace TeamsAITeammate.Core.Interfaces;

public interface ITranscriptRepository
{
    Task AddAsync(TranscriptEntry entry, CancellationToken ct = default);
    Task<IReadOnlyList<TranscriptEntry>> GetBySessionAsync(string sessionId, CancellationToken ct = default);
}
