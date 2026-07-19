using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using TeamsAITeammate.Core.Interfaces;
using TeamsAITeammate.Core.Models;

namespace TeamsAITeammate.Infrastructure.Services;

public class WorkIQTranscriptProvider : ITranscriptProvider
{
    private readonly ILogger<WorkIQTranscriptProvider> _logger;

    public string ProviderName => "WorkIQ";

    public WorkIQTranscriptProvider(ILogger<WorkIQTranscriptProvider> logger)
    {
        _logger = logger;
    }

    public Task<bool> IsAvailableAsync(string meetingId, CancellationToken ct = default)
    {
        // WorkIQ API is not yet available — always fall back to Graph API
        _logger.LogDebug("WorkIQ API availability check: not available (stub)");
        return Task.FromResult(false);
    }

    public async IAsyncEnumerable<TranscriptSegment> StreamTranscriptAsync(
        string meetingId,
        TranscriptStreamOptions options,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        _logger.LogWarning("WorkIQ streaming not implemented; use Graph API fallback");
        await Task.CompletedTask;
        yield break;
    }

    public Task<IReadOnlyList<TranscriptSegment>> GetFullTranscriptAsync(
        string meetingId,
        CancellationToken ct = default)
    {
        _logger.LogWarning("WorkIQ full transcript not implemented; use Graph API fallback");
        return Task.FromResult<IReadOnlyList<TranscriptSegment>>([]);
    }
}
