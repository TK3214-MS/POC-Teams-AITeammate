using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using TeamsAITeammate.Core.Interfaces;
using TeamsAITeammate.Core.Models;

namespace TeamsAITeammate.Infrastructure.Services;

public class InterventionTimer : IInterventionTimer, IDisposable
{
    private readonly ILogger<InterventionTimer> _logger;
    private readonly ConcurrentDictionary<string, TimerState> _timers = new();

    public event Func<SilenceDetectedEvent, Task>? OnSilenceDetected;
    public event Func<TopicChangeEvent, Task>? OnTopicChanged;
    public event Func<PeriodicAnalysisEvent, Task>? OnPeriodicAnalysis;

    public InterventionTimer(ILogger<InterventionTimer> logger)
    {
        _logger = logger;
    }

    public Task StartAsync(string sessionId, InterventionSettings settings, CancellationToken ct = default)
    {
        if (_timers.ContainsKey(sessionId))
        {
            _logger.LogWarning("Timer already running for session {SessionId}", sessionId);
            return Task.CompletedTask;
        }

        var state = new TimerState(settings);

        if (settings.SilenceThreshold > TimeSpan.Zero)
        {
            state.SilenceTimer = new Timer(
                _ => FireSilenceDetected(sessionId, settings.SilenceThreshold),
                null,
                settings.SilenceThreshold,
                Timeout.InfiniteTimeSpan);
        }

        if (settings.PeriodicInterval > TimeSpan.Zero)
        {
            state.PeriodicTimer = new Timer(
                _ => FirePeriodicAnalysis(sessionId),
                null,
                settings.PeriodicInterval,
                settings.PeriodicInterval);
        }

        _timers[sessionId] = state;
        _logger.LogInformation("Started intervention timer for session {SessionId}", sessionId);
        return Task.CompletedTask;
    }

    public Task StopAsync(string sessionId, CancellationToken ct = default)
    {
        if (_timers.TryRemove(sessionId, out var state))
        {
            state.Dispose();
            _logger.LogInformation("Stopped intervention timer for session {SessionId}", sessionId);
        }
        return Task.CompletedTask;
    }

    public Task ResetSilenceTimerAsync(string sessionId, CancellationToken ct = default)
    {
        if (_timers.TryGetValue(sessionId, out var state) && state.SilenceTimer is not null)
        {
            state.SilenceTimer.Change(state.Settings.SilenceThreshold, Timeout.InfiniteTimeSpan);
            _logger.LogDebug("Reset silence timer for session {SessionId}", sessionId);
        }
        return Task.CompletedTask;
    }

    private void FireSilenceDetected(string sessionId, TimeSpan duration)
    {
        if (OnSilenceDetected is null) return;

        _ = Task.Run(async () =>
        {
            try
            {
                await OnSilenceDetected.Invoke(new SilenceDetectedEvent(sessionId, duration));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in silence detected handler for session {SessionId}", sessionId);
            }
        });
    }

    private void FirePeriodicAnalysis(string sessionId)
    {
        if (OnPeriodicAnalysis is null) return;

        _ = Task.Run(async () =>
        {
            try
            {
                await OnPeriodicAnalysis.Invoke(new PeriodicAnalysisEvent(sessionId, DateTimeOffset.UtcNow));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in periodic analysis handler for session {SessionId}", sessionId);
            }
        });
    }

    public void Dispose()
    {
        foreach (var state in _timers.Values)
        {
            state.Dispose();
        }
        _timers.Clear();
        GC.SuppressFinalize(this);
    }

    private sealed class TimerState : IDisposable
    {
        public InterventionSettings Settings { get; }
        public Timer? SilenceTimer { get; set; }
        public Timer? PeriodicTimer { get; set; }

        public TimerState(InterventionSettings settings)
        {
            Settings = settings;
        }

        public void Dispose()
        {
            SilenceTimer?.Dispose();
            PeriodicTimer?.Dispose();
        }
    }
}
