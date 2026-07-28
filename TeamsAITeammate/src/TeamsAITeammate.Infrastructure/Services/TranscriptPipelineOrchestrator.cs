using System.Threading.Channels;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TeamsAITeammate.Core.Interfaces;
using TeamsAITeammate.Core.Models;

namespace TeamsAITeammate.Infrastructure.Services;

public class TranscriptPipelineOrchestrator : IHostedService, IDisposable
{
    private readonly IEnumerable<ITranscriptProvider> _providers;
    private readonly ITranscriptBuffer _buffer;
    private readonly IInterventionTimer _interventionTimer;
    private readonly ILanguageDetector _languageDetector;
    private readonly ITranscriptPersistence _persistence;
    private readonly ITranscriptRepository _transcripts;
    private readonly IMeetingSessionManager _sessionManager;
    private readonly ILogger<TranscriptPipelineOrchestrator> _logger;

    private readonly Channel<TranscriptSegment> _analysisChannel;
    private readonly Dictionary<string, CancellationTokenSource> _activePipelines = new();
    private CancellationTokenSource? _monitorCts;
    private Task? _monitorTask;

    public ChannelReader<TranscriptSegment> AnalysisReader => _analysisChannel.Reader;

    public TranscriptPipelineOrchestrator(
        IEnumerable<ITranscriptProvider> providers,
        ITranscriptBuffer buffer,
        IInterventionTimer interventionTimer,
        ILanguageDetector languageDetector,
        ITranscriptPersistence persistence,
        ITranscriptRepository transcripts,
        IMeetingSessionManager sessionManager,
        ILogger<TranscriptPipelineOrchestrator> logger)
    {
        _providers = providers;
        _buffer = buffer;
        _interventionTimer = interventionTimer;
        _languageDetector = languageDetector;
        _persistence = persistence;
        _transcripts = transcripts;
        _sessionManager = sessionManager;
        _logger = logger;

        _analysisChannel = Channel.CreateBounded<TranscriptSegment>(
            new BoundedChannelOptions(1000)
            {
                FullMode = BoundedChannelFullMode.DropOldest,
            });
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _monitorCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _monitorTask = MonitorSessionsAsync(_monitorCts.Token);
        _logger.LogInformation("Transcript pipeline orchestrator started");
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping transcript pipeline orchestrator");

        if (_monitorCts is not null)
        {
            await _monitorCts.CancelAsync();
        }

        foreach (var (sessionId, cts) in _activePipelines)
        {
            await cts.CancelAsync();
            await _persistence.FinalizeAsync(sessionId, cancellationToken);
        }
        _activePipelines.Clear();

        if (_monitorTask is not null)
        {
            try { await _monitorTask; }
            catch (OperationCanceledException) { }
        }

        _analysisChannel.Writer.TryComplete();
        _logger.LogInformation("Transcript pipeline orchestrator stopped");
    }

    private async Task MonitorSessionsAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var sessions = await _sessionManager.GetActiveSessionsAsync(ct);

                foreach (var session in sessions)
                {
                    if (!_activePipelines.ContainsKey(session.Id))
                    {
                        await StartPipelineForSessionAsync(session, ct);
                    }
                }

                // Clean up pipelines for sessions no longer active
                var activeIds = sessions.Select(s => s.Id).ToHashSet();
                var staleIds = _activePipelines.Keys.Where(id => !activeIds.Contains(id)).ToList();
                foreach (var id in staleIds)
                {
                    await StopPipelineForSessionAsync(id, ct);
                }

                await Task.Delay(TimeSpan.FromSeconds(5), ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error monitoring sessions");
                await Task.Delay(TimeSpan.FromSeconds(10), ct);
            }
        }
    }

    internal async Task StartPipelineForSessionAsync(MeetingSession session, CancellationToken ct)
    {
        var provider = await SelectProviderAsync(session.MeetingId, ct);
        if (provider is null)
        {
            _logger.LogWarning("No transcript provider available for meeting {MeetingId}", session.MeetingId);
            return;
        }

        _logger.LogInformation("Starting transcript pipeline for session {SessionId} using {Provider}",
            session.Id, provider.ProviderName);

        var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _activePipelines[session.Id] = cts;

        _ = RunPipelineAsync(session, provider, cts.Token);
    }

    private async Task RunPipelineAsync(
        MeetingSession session, ITranscriptProvider provider, CancellationToken ct)
    {
        var options = new TranscriptStreamOptions();
        var segmentCount = 0;

        try
        {
            await foreach (var segment in provider.StreamTranscriptAsync(
                session.MeetingId, options, ct))
            {
                await _buffer.AppendAsync(segment, ct);

                await _persistence.AppendSegmentAsync(
                    session.TenantId, session.MeetingId, session.Id, segment, ct);

                await _transcripts.AddAsync(new TranscriptEntry
                {
                    SessionId = session.Id,
                    SpeakerId = segment.SpeakerId,
                    SpeakerName = segment.SpeakerName,
                    Text = segment.Text,
                    Timestamp = segment.Timestamp,
                    Confidence = segment.Confidence,
                    Language = segment.Language,
                }, ct);

                await _interventionTimer.ResetSilenceTimerAsync(session.Id, ct);

                await _analysisChannel.Writer.WriteAsync(segment, ct);

                segmentCount++;

                if (segmentCount % 10 == 0)
                {
                    await DetectAndLogLanguageAsync(session.Id, ct);
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Pipeline cancelled for session {SessionId}", session.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Pipeline error for session {SessionId}, attempting fallback", session.Id);
            await AttemptFallbackAsync(session, provider.ProviderName, ct);
        }
        finally
        {
            await _persistence.FlushAsync(session.Id, ct);
        }
    }

    private async Task AttemptFallbackAsync(
        MeetingSession session, string failedProviderName, CancellationToken ct)
    {
        var fallback = _providers.FirstOrDefault(p =>
            p.ProviderName != failedProviderName);

        if (fallback is null)
        {
            _logger.LogError("No fallback provider available for session {SessionId}", session.Id);
            return;
        }

        if (!await fallback.IsAvailableAsync(session.MeetingId, ct))
        {
            _logger.LogError("Fallback provider {Provider} not available", fallback.ProviderName);
            return;
        }

        _logger.LogInformation("Falling back to {Provider} for session {SessionId}",
            fallback.ProviderName, session.Id);

        await RunPipelineAsync(session, fallback, ct);
    }

    private async Task<ITranscriptProvider?> SelectProviderAsync(string meetingId, CancellationToken ct)
    {
        // Prefer WorkIQ, fallback to Graph
        var ordered = _providers.OrderByDescending(p => p.ProviderName == "WorkIQ");

        foreach (var provider in ordered)
        {
            if (await provider.IsAvailableAsync(meetingId, ct))
                return provider;
        }

        return null;
    }

    private async Task DetectAndLogLanguageAsync(string sessionId, CancellationToken ct)
    {
        try
        {
            var window = await _buffer.GetRecentWindowAsync(sessionId, TimeSpan.FromMinutes(2), ct);
            if (window.Segments.Count == 0) return;

            var result = await _languageDetector.DetectLanguageAsync(window.Segments, ct);
            _logger.LogInformation("Session {SessionId} detected language: {Language} ({Confidence:P0})",
                sessionId, result.PrimaryLanguage, result.Confidence);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Language detection failed for session {SessionId}", sessionId);
        }
    }

    internal async Task StopPipelineForSessionAsync(string sessionId, CancellationToken ct)
    {
        if (_activePipelines.TryGetValue(sessionId, out var cts))
        {
            await cts.CancelAsync();
            _activePipelines.Remove(sessionId);
            await _persistence.FinalizeAsync(sessionId, ct);
            _logger.LogInformation("Stopped pipeline for session {SessionId}", sessionId);
        }
    }

    public void Dispose()
    {
        _monitorCts?.Dispose();
        foreach (var cts in _activePipelines.Values)
            cts.Dispose();
        GC.SuppressFinalize(this);
    }
}
