using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Graph;
using TeamsAITeammate.Core.Interfaces;
using TeamsAITeammate.Core.Models;

namespace TeamsAITeammate.Infrastructure.Services;

public partial class GraphTranscriptProvider : ITranscriptProvider
{
    private readonly GraphServiceClient _graphClient;
    private readonly ILogger<GraphTranscriptProvider> _logger;
    private readonly TimeSpan _pollingInterval;

    public string ProviderName => "GraphAPI";

    public GraphTranscriptProvider(
        GraphClientService graphClientService,
        ILogger<GraphTranscriptProvider> logger,
        TimeSpan? pollingInterval = null)
    {
        _graphClient = graphClientService.Client;
        _logger = logger;
        _pollingInterval = pollingInterval ?? TimeSpan.FromSeconds(5);
    }

    public async Task<bool> IsAvailableAsync(string meetingId, CancellationToken ct = default)
    {
        try
        {
            await _graphClient.Communications.OnlineMeetings[meetingId]
                .GetAsync(cancellationToken: ct);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Graph API not available for meeting {MeetingId}", meetingId);
            return false;
        }
    }

    public async IAsyncEnumerable<TranscriptSegment> StreamTranscriptAsync(
        string meetingId,
        TranscriptStreamOptions options,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var lastProcessedTimestamp = DateTimeOffset.MinValue;

        _logger.LogInformation("Starting Graph API transcript polling for meeting {MeetingId}", meetingId);

        while (!ct.IsCancellationRequested)
        {
            IReadOnlyList<TranscriptSegment> newSegments;
            try
            {
                newSegments = await GetTranscriptSegmentsSinceAsync(
                    meetingId, lastProcessedTimestamp, ct);
            }
            catch (OperationCanceledException)
            {
                yield break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error polling transcript for meeting {MeetingId}", meetingId);
                await Task.Delay(_pollingInterval, ct);
                continue;
            }

            foreach (var segment in newSegments)
            {
                if (segment.Timestamp > lastProcessedTimestamp)
                    lastProcessedTimestamp = segment.Timestamp;

                yield return segment;
            }

            try
            {
                await Task.Delay(_pollingInterval, ct);
            }
            catch (OperationCanceledException)
            {
                yield break;
            }
        }
    }

    public async Task<IReadOnlyList<TranscriptSegment>> GetFullTranscriptAsync(
        string meetingId,
        CancellationToken ct = default)
    {
        return await GetTranscriptSegmentsSinceAsync(meetingId, DateTimeOffset.MinValue, ct);
    }

    private async Task<IReadOnlyList<TranscriptSegment>> GetTranscriptSegmentsSinceAsync(
        string meetingId,
        DateTimeOffset since,
        CancellationToken ct)
    {
        var transcripts = await _graphClient.Communications
            .OnlineMeetings[meetingId]
            .Transcripts
            .GetAsync(cancellationToken: ct);

        if (transcripts?.Value is null || transcripts.Value.Count == 0)
            return [];

        var segments = new List<TranscriptSegment>();

        foreach (var transcript in transcripts.Value)
        {
            if (transcript.Id is null) continue;

            try
            {
                var contentStream = await _graphClient.Communications
                    .OnlineMeetings[meetingId]
                    .Transcripts[transcript.Id]
                    .Content
                    .GetAsync(cancellationToken: ct);

                if (contentStream is null) continue;

                using var reader = new StreamReader(contentStream);
                var vttContent = await reader.ReadToEndAsync(ct);
                var parsed = ParseVtt(vttContent, meetingId);

                segments.AddRange(parsed.Where(s => s.Timestamp > since));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get transcript content {TranscriptId}", transcript.Id);
            }
        }

        return segments.OrderBy(s => s.Timestamp).ToList();
    }

    internal static IReadOnlyList<TranscriptSegment> ParseVtt(string vttContent, string meetingId)
    {
        var segments = new List<TranscriptSegment>();
        if (string.IsNullOrWhiteSpace(vttContent))
            return segments;

        var lines = vttContent.Split('\n', StringSplitOptions.None);
        var i = 0;

        // Skip WEBVTT header
        while (i < lines.Length && !lines[i].TrimEnd().Contains("-->"))
            i++;

        while (i < lines.Length)
        {
            var line = lines[i].TrimEnd();

            // Look for timestamp line: "00:00:01.000 --> 00:00:03.500"
            var match = TimestampPattern().Match(line);
            if (!match.Success)
            {
                i++;
                continue;
            }

            var start = ParseVttTimestamp(match.Groups[1].Value);
            var end = ParseVttTimestamp(match.Groups[2].Value);
            i++;

            // Collect text lines until blank line or end
            var speakerName = string.Empty;
            var speakerId = string.Empty;
            var textParts = new List<string>();

            while (i < lines.Length && !string.IsNullOrWhiteSpace(lines[i]))
            {
                var textLine = lines[i].TrimEnd();

                // Check for speaker tag: <v SpeakerName>text</v>
                var speakerMatch = SpeakerPattern().Match(textLine);
                if (speakerMatch.Success)
                {
                    speakerName = speakerMatch.Groups[1].Value;
                    speakerId = speakerName;
                    textParts.Add(speakerMatch.Groups[2].Value);
                }
                else
                {
                    // Strip any remaining HTML-like tags
                    textParts.Add(HtmlTagPattern().Replace(textLine, string.Empty));
                }

                i++;
            }

            var text = string.Join(' ', textParts).Trim();
            if (!string.IsNullOrEmpty(text))
            {
                segments.Add(new TranscriptSegment
                {
                    MeetingId = meetingId,
                    SpeakerId = speakerId,
                    SpeakerName = speakerName,
                    Text = text,
                    Timestamp = start,
                    Duration = end - start,
                    Confidence = 1.0f,
                });
            }

            i++;
        }

        return segments;
    }

    private static DateTimeOffset ParseVttTimestamp(string ts)
    {
        // Format: HH:mm:ss.fff or mm:ss.fff
        var parts = ts.Split(':');
        int hours = 0, minutes, seconds, millis;

        if (parts.Length == 3)
        {
            hours = int.Parse(parts[0], CultureInfo.InvariantCulture);
            minutes = int.Parse(parts[1], CultureInfo.InvariantCulture);
            var secParts = parts[2].Split('.');
            seconds = int.Parse(secParts[0], CultureInfo.InvariantCulture);
            millis = secParts.Length > 1 ? int.Parse(secParts[1].PadRight(3, '0')[..3], CultureInfo.InvariantCulture) : 0;
        }
        else
        {
            minutes = int.Parse(parts[0], CultureInfo.InvariantCulture);
            var secParts = parts[1].Split('.');
            seconds = int.Parse(secParts[0], CultureInfo.InvariantCulture);
            millis = secParts.Length > 1 ? int.Parse(secParts[1].PadRight(3, '0')[..3], CultureInfo.InvariantCulture) : 0;
        }

        return DateTimeOffset.UnixEpoch.Add(new TimeSpan(0, hours, minutes, seconds, millis));
    }

    [GeneratedRegex(@"(\d{1,2}:\d{2}:\d{2}[\.,]\d{3})\s*-->\s*(\d{1,2}:\d{2}:\d{2}[\.,]\d{3})")]
    private static partial Regex TimestampPattern();

    [GeneratedRegex(@"<v\s+([^>]+)>(.+?)(?:</v>)?$")]
    private static partial Regex SpeakerPattern();

    [GeneratedRegex(@"<[^>]+>")]
    private static partial Regex HtmlTagPattern();
}
