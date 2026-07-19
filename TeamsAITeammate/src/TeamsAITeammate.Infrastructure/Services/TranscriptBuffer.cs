using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using TeamsAITeammate.Core.Interfaces;
using TeamsAITeammate.Core.Models;

namespace TeamsAITeammate.Infrastructure.Services;

public class TranscriptBuffer : ITranscriptBuffer
{
    private readonly ConcurrentDictionary<string, List<TranscriptSegment>> _buffers = new();
    private readonly ILogger<TranscriptBuffer> _logger;

    public TranscriptBuffer(ILogger<TranscriptBuffer> logger)
    {
        _logger = logger;
    }

    public Task AppendAsync(TranscriptSegment segment, CancellationToken ct = default)
    {
        var list = _buffers.GetOrAdd(segment.MeetingId, _ => []);
        lock (list)
        {
            list.Add(segment);
        }

        _logger.LogDebug("Buffered segment for meeting {MeetingId}, speaker {Speaker}",
            segment.MeetingId, segment.SpeakerName);

        return Task.CompletedTask;
    }

    public Task<ConversationWindow> GetRecentWindowAsync(
        string sessionId, TimeSpan window, CancellationToken ct = default)
    {
        var segments = GetSegments(sessionId);
        var cutoff = DateTimeOffset.UtcNow - window;
        var recent = segments.Where(s => s.Timestamp >= cutoff).ToList();

        return Task.FromResult(BuildWindow(sessionId, recent));
    }

    public Task<ConversationWindow> GetFullConversationAsync(
        string sessionId, CancellationToken ct = default)
    {
        var segments = GetSegments(sessionId);
        return Task.FromResult(BuildWindow(sessionId, segments));
    }

    public Task<IReadOnlyDictionary<string, SpeakerStats>> GetSpeakerStatsAsync(
        string sessionId, CancellationToken ct = default)
    {
        var segments = GetSegments(sessionId);

        var stats = segments
            .GroupBy(s => s.SpeakerId)
            .ToDictionary(
                g => g.Key,
                g => new SpeakerStats
                {
                    SpeakerId = g.Key,
                    SpeakerName = g.First().SpeakerName,
                    SegmentCount = g.Count(),
                    TotalSpeakingTime = TimeSpan.FromTicks(g.Sum(s => s.Duration.Ticks)),
                    LastSpokenAt = g.Max(s => s.Timestamp),
                });

        return Task.FromResult<IReadOnlyDictionary<string, SpeakerStats>>(stats);
    }

    public Task<IReadOnlyList<SilencePeriod>> DetectSilencePeriodsAsync(
        string sessionId, TimeSpan threshold, CancellationToken ct = default)
    {
        var segments = GetSegments(sessionId);
        var silences = new List<SilencePeriod>();

        if (segments.Count < 2)
            return Task.FromResult<IReadOnlyList<SilencePeriod>>(silences);

        var ordered = segments.OrderBy(s => s.Timestamp).ToList();

        for (var i = 1; i < ordered.Count; i++)
        {
            var prevEnd = ordered[i - 1].Timestamp + ordered[i - 1].Duration;
            var currentStart = ordered[i].Timestamp;
            var gap = currentStart - prevEnd;

            if (gap >= threshold)
            {
                silences.Add(new SilencePeriod
                {
                    Start = prevEnd,
                    End = currentStart,
                    Duration = gap,
                });
            }
        }

        return Task.FromResult<IReadOnlyList<SilencePeriod>>(silences);
    }

    private IReadOnlyList<TranscriptSegment> GetSegments(string sessionId)
    {
        if (!_buffers.TryGetValue(sessionId, out var list))
            return [];

        lock (list)
        {
            return list.ToList();
        }
    }

    private static ConversationWindow BuildWindow(string sessionId, IReadOnlyList<TranscriptSegment> segments)
    {
        if (segments.Count == 0)
        {
            return new ConversationWindow
            {
                SessionId = sessionId,
                Segments = [],
                WindowStart = DateTimeOffset.UtcNow,
                WindowEnd = DateTimeOffset.UtcNow,
            };
        }

        var ordered = segments.OrderBy(s => s.Timestamp).ToList();

        return new ConversationWindow
        {
            SessionId = sessionId,
            Segments = ordered,
            WindowStart = ordered[0].Timestamp,
            WindowEnd = ordered[^1].Timestamp + ordered[^1].Duration,
            TotalDuration = (ordered[^1].Timestamp + ordered[^1].Duration) - ordered[0].Timestamp,
            UniqueSpeakerCount = ordered.Select(s => s.SpeakerId).Distinct().Count(),
            DetectedLanguage = ordered
                .Where(s => !string.IsNullOrEmpty(s.Language))
                .GroupBy(s => s.Language)
                .MaxBy(g => g.Count())?.Key ?? string.Empty,
        };
    }
}
