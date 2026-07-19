using Microsoft.Extensions.Logging;
using Moq;
using TeamsAITeammate.AI.Services;
using TeamsAITeammate.Core.Interfaces;
using TeamsAITeammate.Core.Models;

namespace TeamsAITeammate.UnitTests;

public class AnalysisSchedulerTests
{
    private readonly Mock<IConversationAnalyzer> _mockAnalyzer;
    private readonly Mock<IQuestionGenerator> _mockQuestionGenerator;
    private readonly Mock<ITacitKnowledgeExtractor> _mockTacitExtractor;
    private readonly Mock<ITranscriptBuffer> _mockBuffer;
    private readonly Mock<IInterventionTimer> _mockTimer;
    private readonly AnalysisScheduler _scheduler;

    public AnalysisSchedulerTests()
    {
        _mockAnalyzer = new Mock<IConversationAnalyzer>();
        _mockQuestionGenerator = new Mock<IQuestionGenerator>();
        _mockTacitExtractor = new Mock<ITacitKnowledgeExtractor>();
        _mockBuffer = new Mock<ITranscriptBuffer>();
        _mockTimer = new Mock<IInterventionTimer>();

        _scheduler = new AnalysisScheduler(
            _mockAnalyzer.Object,
            _mockQuestionGenerator.Object,
            _mockTacitExtractor.Object,
            _mockBuffer.Object,
            _mockTimer.Object,
            new Mock<ILogger<AnalysisScheduler>>().Object);
    }

    [Fact]
    public async Task StartAsync_InitializesSession()
    {
        await _scheduler.StartAsync("session-1");
        // Should not throw; session is registered
    }

    [Fact]
    public async Task StopAsync_RemovesSession()
    {
        await _scheduler.StartAsync("session-1");
        await _scheduler.StopAsync("session-1");
        // Subsequent analysis requests should be ignored
        await _scheduler.RequestAnalysisAsync("session-1", "mention");
        _mockAnalyzer.Verify(a => a.AnalyzeAsync(
            It.IsAny<ConversationWindow>(),
            It.IsAny<AnalysisContext>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RequestAnalysis_Mention_RunsFullAnalysis()
    {
        SetupBufferReturns(CreateSampleWindow());
        SetupAnalyzerReturns(new ConversationAnalysis());
        SetupQuestionGeneratorReturns([]);
        SetupTacitExtractorReturns([]);

        await _scheduler.StartAsync("session-1");
        await _scheduler.RequestAnalysisAsync("session-1", "mention");

        _mockAnalyzer.Verify(a => a.AnalyzeAsync(
            It.IsAny<ConversationWindow>(),
            It.IsAny<AnalysisContext>(),
            It.IsAny<CancellationToken>()), Times.Once);

        _mockQuestionGenerator.Verify(q => q.GenerateQuestionsAsync(
            It.IsAny<ConversationWindow>(),
            It.IsAny<AnalysisContext>(),
            It.IsAny<QuestionGenerationOptions>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RequestAnalysis_Silence_RunsFullAnalysis()
    {
        SetupBufferReturns(CreateSampleWindow());
        SetupAnalyzerReturns(new ConversationAnalysis());
        SetupQuestionGeneratorReturns([]);
        SetupTacitExtractorReturns([]);

        await _scheduler.StartAsync("session-1");
        await _scheduler.RequestAnalysisAsync("session-1", "silence");

        _mockAnalyzer.Verify(a => a.AnalyzeAsync(
            It.IsAny<ConversationWindow>(),
            It.IsAny<AnalysisContext>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RequestAnalysis_TopicChange_ExtractsTacitKnowledge()
    {
        SetupRecentBufferReturns(CreateSampleWindow());
        SetupTacitExtractorReturns(
        [
            new TacitKnowledgeCandidate
            {
                Content = "Test knowledge",
                Category = TacitKnowledgeCategory.ExpertKnowledge,
                Confidence = 0.8f
            }
        ]);
        SetupQuestionGeneratorReturns([]);

        await _scheduler.StartAsync("session-1");
        await _scheduler.RequestAnalysisAsync("session-1", "topic_change");

        _mockTacitExtractor.Verify(t => t.ExtractAsync(
            It.IsAny<ConversationWindow>(),
            It.IsAny<AnalysisContext>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RequestAnalysis_EmptyWindow_DoesNotCallAnalyzer()
    {
        var emptyWindow = new ConversationWindow { Segments = [] };
        SetupBufferReturns(emptyWindow);

        await _scheduler.StartAsync("session-1");
        await _scheduler.RequestAnalysisAsync("session-1", "mention");

        _mockAnalyzer.Verify(a => a.AnalyzeAsync(
            It.IsAny<ConversationWindow>(),
            It.IsAny<AnalysisContext>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RequestAnalysis_UnknownSession_IsIgnored()
    {
        await _scheduler.RequestAnalysisAsync("unknown-session", "mention");

        _mockAnalyzer.Verify(a => a.AnalyzeAsync(
            It.IsAny<ConversationWindow>(),
            It.IsAny<AnalysisContext>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RequestAnalysis_Periodic_RunsIncrementalAnalysis()
    {
        SetupRecentBufferReturns(CreateSampleWindow());
        SetupAnalyzerReturns(new ConversationAnalysis());

        await _scheduler.StartAsync("session-1");
        await _scheduler.RequestAnalysisAsync("session-1", "periodic");

        _mockAnalyzer.Verify(a => a.AnalyzeAsync(
            It.IsAny<ConversationWindow>(),
            It.IsAny<AnalysisContext>(),
            It.IsAny<CancellationToken>()), Times.Once);

        // Question generator should NOT be called for incremental analysis
        _mockQuestionGenerator.Verify(q => q.GenerateQuestionsAsync(
            It.IsAny<ConversationWindow>(),
            It.IsAny<AnalysisContext>(),
            It.IsAny<QuestionGenerationOptions>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task OnAnalysisCompleted_IsFiredAfterAnalysis()
    {
        SetupBufferReturns(CreateSampleWindow());
        var analysis = new ConversationAnalysis
        {
            Topics = [new DetectedTopic { Title = "Test" }]
        };
        SetupAnalyzerReturns(analysis);
        SetupQuestionGeneratorReturns([]);
        SetupTacitExtractorReturns([]);

        ConversationAnalysis? received = null;
        _scheduler.OnAnalysisCompleted += (sessionId, a) =>
        {
            received = a;
            return Task.CompletedTask;
        };

        await _scheduler.StartAsync("session-1");
        await _scheduler.RequestAnalysisAsync("session-1", "mention");

        Assert.NotNull(received);
        Assert.Single(received.Topics);
    }

    private void SetupBufferReturns(ConversationWindow window)
    {
        _mockBuffer.Setup(b => b.GetFullConversationAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(window);
        _mockBuffer.Setup(b => b.GetRecentWindowAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(window);
    }

    private void SetupRecentBufferReturns(ConversationWindow window)
    {
        _mockBuffer.Setup(b => b.GetRecentWindowAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(window);
    }

    private void SetupAnalyzerReturns(ConversationAnalysis analysis)
    {
        _mockAnalyzer.Setup(a => a.AnalyzeAsync(
            It.IsAny<ConversationWindow>(),
            It.IsAny<AnalysisContext>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(analysis);
    }

    private void SetupQuestionGeneratorReturns(IReadOnlyList<GeneratedQuestion> questions)
    {
        _mockQuestionGenerator.Setup(q => q.GenerateQuestionsAsync(
            It.IsAny<ConversationWindow>(),
            It.IsAny<AnalysisContext>(),
            It.IsAny<QuestionGenerationOptions>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(questions);
    }

    private void SetupTacitExtractorReturns(IReadOnlyList<TacitKnowledgeCandidate> candidates)
    {
        _mockTacitExtractor.Setup(t => t.ExtractAsync(
            It.IsAny<ConversationWindow>(),
            It.IsAny<AnalysisContext>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(candidates);
    }

    private static ConversationWindow CreateSampleWindow() => new()
    {
        SessionId = "session-1",
        Segments =
        [
            new TranscriptSegment { SpeakerName = "田中", Text = "議論しましょう", Timestamp = DateTimeOffset.UtcNow.AddMinutes(-5) },
            new TranscriptSegment { SpeakerName = "佐藤", Text = "了解です", Timestamp = DateTimeOffset.UtcNow.AddMinutes(-4) }
        ],
        WindowStart = DateTimeOffset.UtcNow.AddMinutes(-5),
        WindowEnd = DateTimeOffset.UtcNow
    };
}
