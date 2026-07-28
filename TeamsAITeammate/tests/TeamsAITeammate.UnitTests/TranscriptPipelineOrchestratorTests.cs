using Microsoft.Extensions.Logging;
using Moq;
using TeamsAITeammate.Core.Interfaces;
using TeamsAITeammate.Core.Models;
using TeamsAITeammate.Infrastructure.Services;

namespace TeamsAITeammate.UnitTests;

public class TranscriptPipelineOrchestratorTests
{
    private readonly Mock<ITranscriptBuffer> _bufferMock = new();
    private readonly Mock<IInterventionTimer> _timerMock = new();
    private readonly Mock<ILanguageDetector> _detectorMock = new();
    private readonly Mock<ITranscriptPersistence> _persistenceMock = new();
    private readonly Mock<ITranscriptRepository> _transcriptsMock = new();
    private readonly Mock<IMeetingSessionManager> _sessionManagerMock = new();
    private readonly Mock<ILogger<TranscriptPipelineOrchestrator>> _loggerMock = new();

    private TranscriptPipelineOrchestrator CreateOrchestrator(
        params ITranscriptProvider[] providers)
    {
        return new TranscriptPipelineOrchestrator(
            providers,
            _bufferMock.Object,
            _timerMock.Object,
            _detectorMock.Object,
            _persistenceMock.Object,
            _transcriptsMock.Object,
            _sessionManagerMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task StartPipeline_PrefersWorkIQ_WhenAvailable()
    {
        var workIQ = new Mock<ITranscriptProvider>();
        workIQ.Setup(p => p.ProviderName).Returns("WorkIQ");
        workIQ.Setup(p => p.IsAvailableAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        workIQ.Setup(p => p.StreamTranscriptAsync(It.IsAny<string>(), It.IsAny<TranscriptStreamOptions>(), It.IsAny<CancellationToken>()))
            .Returns(EmptyStream());

        var graph = new Mock<ITranscriptProvider>();
        graph.Setup(p => p.ProviderName).Returns("GraphAPI");
        graph.Setup(p => p.IsAvailableAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var orchestrator = CreateOrchestrator(workIQ.Object, graph.Object);
        var session = new MeetingSession { MeetingId = "m1", TenantId = "t1" };

        await orchestrator.StartPipelineForSessionAsync(session, CancellationToken.None);

        // Give async task time to start
        await Task.Delay(100);

        workIQ.Verify(p => p.StreamTranscriptAsync(
            "m1", It.IsAny<TranscriptStreamOptions>(), It.IsAny<CancellationToken>()), Times.Once);
        graph.Verify(p => p.StreamTranscriptAsync(
            It.IsAny<string>(), It.IsAny<TranscriptStreamOptions>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task StartPipeline_FallsBackToGraph_WhenWorkIQUnavailable()
    {
        var workIQ = new Mock<ITranscriptProvider>();
        workIQ.Setup(p => p.ProviderName).Returns("WorkIQ");
        workIQ.Setup(p => p.IsAvailableAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var graph = new Mock<ITranscriptProvider>();
        graph.Setup(p => p.ProviderName).Returns("GraphAPI");
        graph.Setup(p => p.IsAvailableAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        graph.Setup(p => p.StreamTranscriptAsync(It.IsAny<string>(), It.IsAny<TranscriptStreamOptions>(), It.IsAny<CancellationToken>()))
            .Returns(EmptyStream());

        var orchestrator = CreateOrchestrator(workIQ.Object, graph.Object);
        var session = new MeetingSession { MeetingId = "m1", TenantId = "t1" };

        await orchestrator.StartPipelineForSessionAsync(session, CancellationToken.None);

        await Task.Delay(100);

        graph.Verify(p => p.StreamTranscriptAsync(
            "m1", It.IsAny<TranscriptStreamOptions>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task StartPipeline_NoProvider_LogsWarning()
    {
        var workIQ = new Mock<ITranscriptProvider>();
        workIQ.Setup(p => p.ProviderName).Returns("WorkIQ");
        workIQ.Setup(p => p.IsAvailableAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var graph = new Mock<ITranscriptProvider>();
        graph.Setup(p => p.ProviderName).Returns("GraphAPI");
        graph.Setup(p => p.IsAvailableAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var orchestrator = CreateOrchestrator(workIQ.Object, graph.Object);
        var session = new MeetingSession { MeetingId = "m1", TenantId = "t1" };

        await orchestrator.StartPipelineForSessionAsync(session, CancellationToken.None);

        // No provider streaming should have been called
        workIQ.Verify(p => p.StreamTranscriptAsync(
            It.IsAny<string>(), It.IsAny<TranscriptStreamOptions>(), It.IsAny<CancellationToken>()), Times.Never);
        graph.Verify(p => p.StreamTranscriptAsync(
            It.IsAny<string>(), It.IsAny<TranscriptStreamOptions>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Pipeline_BuffersAndPersistsSegments()
    {
        var segment = new TranscriptSegment
        {
            MeetingId = "m1",
            SpeakerId = "alice",
            SpeakerName = "Alice",
            Text = "Hello",
            Timestamp = DateTimeOffset.UtcNow,
            Duration = TimeSpan.FromSeconds(2),
        };

        var provider = new Mock<ITranscriptProvider>();
        provider.Setup(p => p.ProviderName).Returns("TestProvider");
        provider.Setup(p => p.IsAvailableAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        provider.Setup(p => p.StreamTranscriptAsync(It.IsAny<string>(), It.IsAny<TranscriptStreamOptions>(), It.IsAny<CancellationToken>()))
            .Returns(SingleSegmentStream(segment));

        _bufferMock.Setup(b => b.GetRecentWindowAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConversationWindow { Segments = [] });

        var orchestrator = CreateOrchestrator(provider.Object);
        var session = new MeetingSession { MeetingId = "m1", TenantId = "t1" };

        await orchestrator.StartPipelineForSessionAsync(session, CancellationToken.None);

        // Wait for async pipeline to process
        await Task.Delay(200);

        _bufferMock.Verify(b => b.AppendAsync(segment, It.IsAny<CancellationToken>()), Times.Once);
        _persistenceMock.Verify(p => p.AppendSegmentAsync(
            "t1", "m1", session.Id, segment, It.IsAny<CancellationToken>()), Times.Once);
        _transcriptsMock.Verify(p => p.AddAsync(
            It.Is<TranscriptEntry>(entry =>
                entry.SessionId == session.Id &&
                entry.SpeakerName == "Alice" &&
                entry.Text == "Hello"),
            It.IsAny<CancellationToken>()), Times.Once);
        _timerMock.Verify(t => t.ResetSilenceTimerAsync(session.Id, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task StopPipeline_FinalizesPersistence()
    {
        var orchestrator = CreateOrchestrator();

        await orchestrator.StopPipelineForSessionAsync("session-1", CancellationToken.None);

        // No active pipeline, so finalize is not called (graceful no-op)
        _persistenceMock.Verify(
            p => p.FinalizeAsync("session-1", It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private static async IAsyncEnumerable<TranscriptSegment> EmptyStream()
    {
        await Task.CompletedTask;
        yield break;
    }

    private static async IAsyncEnumerable<TranscriptSegment> SingleSegmentStream(TranscriptSegment segment)
    {
        await Task.CompletedTask;
        yield return segment;
    }
}
