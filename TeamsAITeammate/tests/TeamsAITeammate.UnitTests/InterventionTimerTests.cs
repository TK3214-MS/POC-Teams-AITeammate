using Microsoft.Extensions.Logging;
using Moq;
using TeamsAITeammate.Core.Models;
using TeamsAITeammate.Infrastructure.Services;

namespace TeamsAITeammate.UnitTests;

public class InterventionTimerTests
{
    private readonly Mock<ILogger<InterventionTimer>> _loggerMock = new();

    [Fact]
    public async Task StartAsync_NewSession_StartsSuccessfully()
    {
        using var timer = new InterventionTimer(_loggerMock.Object);
        var settings = new InterventionSettings
        {
            SilenceThreshold = TimeSpan.FromSeconds(10),
            PeriodicInterval = TimeSpan.FromMinutes(1),
        };

        await timer.StartAsync("session-1", settings);

        // Should not throw — timer is running
    }

    [Fact]
    public async Task StartAsync_DuplicateSession_DoesNotThrow()
    {
        using var timer = new InterventionTimer(_loggerMock.Object);
        var settings = new InterventionSettings();

        await timer.StartAsync("session-1", settings);
        await timer.StartAsync("session-1", settings); // Should not throw
    }

    [Fact]
    public async Task StopAsync_RunningSession_StopsSuccessfully()
    {
        using var timer = new InterventionTimer(_loggerMock.Object);
        await timer.StartAsync("session-1", new InterventionSettings());

        await timer.StopAsync("session-1");

        // Starting again after stop should work
        await timer.StartAsync("session-1", new InterventionSettings());
    }

    [Fact]
    public async Task StopAsync_NonExistentSession_DoesNotThrow()
    {
        using var timer = new InterventionTimer(_loggerMock.Object);

        await timer.StopAsync("nonexistent"); // Should not throw
    }

    [Fact]
    public async Task ResetSilenceTimerAsync_RunningSession_ResetsSuccessfully()
    {
        using var timer = new InterventionTimer(_loggerMock.Object);
        await timer.StartAsync("session-1", new InterventionSettings
        {
            SilenceThreshold = TimeSpan.FromSeconds(30),
        });

        await timer.ResetSilenceTimerAsync("session-1"); // Should not throw
    }

    [Fact]
    public async Task ResetSilenceTimerAsync_NonExistentSession_DoesNotThrow()
    {
        using var timer = new InterventionTimer(_loggerMock.Object);

        await timer.ResetSilenceTimerAsync("nonexistent"); // Should not throw
    }

    [Fact]
    public async Task OnSilenceDetected_ShortThreshold_FiresEvent()
    {
        using var timer = new InterventionTimer(_loggerMock.Object);
        var eventFired = new TaskCompletionSource<SilenceDetectedEvent>();

        timer.OnSilenceDetected += e =>
        {
            eventFired.TrySetResult(e);
            return Task.CompletedTask;
        };

        await timer.StartAsync("session-1", new InterventionSettings
        {
            SilenceThreshold = TimeSpan.FromMilliseconds(50),
            PeriodicInterval = TimeSpan.Zero,
        });

        var result = await Task.WhenAny(eventFired.Task, Task.Delay(TimeSpan.FromSeconds(5)));

        Assert.Same(eventFired.Task, result);
        var evt = await eventFired.Task;
        Assert.Equal("session-1", evt.SessionId);
    }

    [Fact]
    public async Task OnPeriodicAnalysis_ShortInterval_FiresEvent()
    {
        using var timer = new InterventionTimer(_loggerMock.Object);
        var eventFired = new TaskCompletionSource<PeriodicAnalysisEvent>();

        timer.OnPeriodicAnalysis += e =>
        {
            eventFired.TrySetResult(e);
            return Task.CompletedTask;
        };

        await timer.StartAsync("session-1", new InterventionSettings
        {
            SilenceThreshold = TimeSpan.Zero,
            PeriodicInterval = TimeSpan.FromMilliseconds(50),
        });

        var result = await Task.WhenAny(eventFired.Task, Task.Delay(TimeSpan.FromSeconds(5)));

        Assert.Same(eventFired.Task, result);
        var evt = await eventFired.Task;
        Assert.Equal("session-1", evt.SessionId);
    }

    [Fact]
    public async Task StopAsync_PreventsSubsequentEvents()
    {
        using var timer = new InterventionTimer(_loggerMock.Object);
        var eventCount = 0;

        timer.OnSilenceDetected += _ =>
        {
            Interlocked.Increment(ref eventCount);
            return Task.CompletedTask;
        };

        await timer.StartAsync("session-1", new InterventionSettings
        {
            SilenceThreshold = TimeSpan.FromMilliseconds(200),
            PeriodicInterval = TimeSpan.Zero,
        });

        await timer.StopAsync("session-1");

        // Wait enough time for the timer to have fired if it was still running
        await Task.Delay(500);

        Assert.Equal(0, eventCount);
    }

    [Fact]
    public async Task Dispose_CleansUpTimers()
    {
        var timer = new InterventionTimer(_loggerMock.Object);
        await timer.StartAsync("session-1", new InterventionSettings());
        await timer.StartAsync("session-2", new InterventionSettings());

        timer.Dispose(); // Should not throw
    }

    [Fact]
    public async Task MultipleSessions_IndependentTimers()
    {
        using var timer = new InterventionTimer(_loggerMock.Object);

        await timer.StartAsync("session-1", new InterventionSettings());
        await timer.StartAsync("session-2", new InterventionSettings());

        await timer.StopAsync("session-1");

        // session-2 should still be running — reset should not throw
        await timer.ResetSilenceTimerAsync("session-2");
    }
}
