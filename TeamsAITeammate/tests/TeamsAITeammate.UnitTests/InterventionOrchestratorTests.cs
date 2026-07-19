using Microsoft.Extensions.Logging;
using Moq;
using TeamsAITeammate.Core.Interfaces;
using TeamsAITeammate.Core.Models;
using TeamsAITeammate.Infrastructure.Services;

namespace TeamsAITeammate.UnitTests;

public class InterventionOrchestratorTests
{
    private readonly Mock<INotificationThrottler> _throttler = new();
    private readonly Mock<IMessageFormatter> _formatter = new();
    private readonly Mock<IGraphMeetingClient> _graphClient = new();
    private readonly Mock<IMeetingSessionManager> _sessionManager = new();
    private readonly InterventionOrchestrator _orchestrator;

    public InterventionOrchestratorTests()
    {
        _orchestrator = new InterventionOrchestrator(
            _throttler.Object,
            _formatter.Object,
            _graphClient.Object,
            _sessionManager.Object,
            Mock.Of<ILogger<InterventionOrchestrator>>());
    }

    [Fact]
    public async Task EvaluateAsync_WhenPaused_ReturnsNull()
    {
        await _orchestrator.PauseAsync("session1", CancellationToken.None);

        var result = await _orchestrator.EvaluateAsync(
            "session1", InterventionTrigger.SilenceDetected, null, CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task EvaluateAsync_AfterResume_ReturnsAction()
    {
        var analysis = CreateAnalysisWithHighPriorityQuestion();

        await _orchestrator.PauseAsync("session1", CancellationToken.None);
        await _orchestrator.ResumeAsync("session1", CancellationToken.None);

        var result = await _orchestrator.EvaluateAsync(
            "session1", InterventionTrigger.SilenceDetected, analysis, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(InterventionTrigger.SilenceDetected, result.Trigger);
    }

    [Fact]
    public async Task EvaluateAsync_SilenceDetected_WithHighPriorityQuestion_ReturnsAdaptiveCard()
    {
        var analysis = CreateAnalysisWithHighPriorityQuestion();

        var result = await _orchestrator.EvaluateAsync(
            "session1", InterventionTrigger.SilenceDetected, analysis, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(InterventionType.AdaptiveCard, result.Type);
        Assert.Equal(InterventionTrigger.SilenceDetected, result.Trigger);
        Assert.Equal(InterventionPriority.High, result.Priority);
    }

    [Fact]
    public async Task EvaluateAsync_SilenceDetected_WithSuggestedAgenda_ReturnsAgendaCard()
    {
        var analysis = new ConversationAnalysis
        {
            Questions = [],
            SuggestedAgenda = new[]
            {
                new SuggestedAgendaItem { Title = "Review timeline", Rationale = "Not discussed yet" }
            }
        };

        var result = await _orchestrator.EvaluateAsync(
            "session1", InterventionTrigger.SilenceDetected, analysis, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(InterventionType.AdaptiveCard, result.Type);
        Assert.Equal(InterventionPriority.Medium, result.Priority);
    }

    [Fact]
    public async Task EvaluateAsync_SilenceDetected_NoQuestions_ReturnsNull()
    {
        var analysis = new ConversationAnalysis
        {
            Questions = [],
            SuggestedAgenda = []
        };

        var result = await _orchestrator.EvaluateAsync(
            "session1", InterventionTrigger.SilenceDetected, analysis, CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task EvaluateAsync_TopicChange_WithKnowledge_ReturnsTacitKnowledgeCard()
    {
        var analysis = new ConversationAnalysis
        {
            TacitKnowledgeCandidates = new[]
            {
                new TacitKnowledgeCandidate
                {
                    Content = "There is a known workaround",
                    Category = TacitKnowledgeCategory.ExpertKnowledge,
                    SourceSpeaker = "Alice",
                    Confidence = 0.85f
                }
            }
        };

        var result = await _orchestrator.EvaluateAsync(
            "session1", InterventionTrigger.TopicChange, analysis, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(InterventionType.AdaptiveCard, result.Type);
        Assert.Equal(InterventionTrigger.TopicChange, result.Trigger);
    }

    [Fact]
    public async Task EvaluateAsync_PeriodicAnalysis_WithQuestions_ReturnsSummaryCard()
    {
        var analysis = CreateAnalysisWithHighPriorityQuestion();

        var result = await _orchestrator.EvaluateAsync(
            "session1", InterventionTrigger.PeriodicAnalysis, analysis, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(InterventionType.AdaptiveCard, result.Type);
        Assert.Equal(InterventionTrigger.PeriodicAnalysis, result.Trigger);
        Assert.Equal(InterventionPriority.Low, result.Priority);
    }

    [Fact]
    public async Task EvaluateAsync_CriticalInsight_HighConfidence_ReturnsCard()
    {
        var analysis = new ConversationAnalysis
        {
            TacitKnowledgeCandidates = new[]
            {
                new TacitKnowledgeCandidate
                {
                    Content = "Critical compliance requirement",
                    Category = TacitKnowledgeCategory.LessonsLearned,
                    SourceSpeaker = "Bob",
                    Confidence = 0.95f
                }
            }
        };

        var result = await _orchestrator.EvaluateAsync(
            "session1", InterventionTrigger.CriticalInsight, analysis, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(InterventionPriority.High, result.Priority);
    }

    [Fact]
    public async Task EvaluateAsync_CriticalInsight_LowConfidence_ReturnsNull()
    {
        var analysis = new ConversationAnalysis
        {
            TacitKnowledgeCandidates = new[]
            {
                new TacitKnowledgeCandidate { Confidence = 0.5f }
            }
        };

        var result = await _orchestrator.EvaluateAsync(
            "session1", InterventionTrigger.CriticalInsight, analysis, CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task EvaluateAsync_NullAnalysis_ReturnsNull()
    {
        var result = await _orchestrator.EvaluateAsync(
            "session1", InterventionTrigger.SilenceDetected, null, CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task ExecuteAsync_WhenThrottled_DoesNotSend()
    {
        _throttler.Setup(t => t.CanSendAsync(It.IsAny<string>(), It.IsAny<InterventionType>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var action = new InterventionAction
        {
            Type = InterventionType.ChatMessage,
            Content = "Hello",
            SessionId = "session1"
        };

        await _orchestrator.ExecuteAsync(action, CancellationToken.None);

        _graphClient.Verify(g => g.SendChatMessageAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_ChatMessage_SendsViaGraph()
    {
        _throttler.Setup(t => t.CanSendAsync(It.IsAny<string>(), It.IsAny<InterventionType>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _sessionManager.Setup(s => s.GetActiveSessionAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MeetingSession
            {
                Id = "session1",
                MeetingId = "meeting1",
                Context = new MeetingContext { ChatId = "chat1" }
            });

        var action = new InterventionAction
        {
            Type = InterventionType.ChatMessage,
            Content = "Test message",
            SessionId = "session1"
        };

        await _orchestrator.ExecuteAsync(action, CancellationToken.None);

        _graphClient.Verify(g => g.SendChatMessageAsync("chat1", "Test message", It.IsAny<CancellationToken>()), Times.Once);
        _throttler.Verify(t => t.RecordSentAsync("session1", InterventionType.ChatMessage, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_AdaptiveCard_SendsCardViaGraph()
    {
        _throttler.Setup(t => t.CanSendAsync(It.IsAny<string>(), It.IsAny<InterventionType>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _sessionManager.Setup(s => s.GetActiveSessionAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MeetingSession
            {
                Id = "session1",
                Context = new MeetingContext { ChatId = "chat1" }
            });

        var action = new InterventionAction
        {
            Type = InterventionType.AdaptiveCard,
            Content = "{\"card\":true}",
            SessionId = "session1"
        };

        await _orchestrator.ExecuteAsync(action, CancellationToken.None);

        _graphClient.Verify(g => g.SendAdaptiveCardAsync("chat1", "{\"card\":true}", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_NoActiveSession_DoesNotSend()
    {
        _throttler.Setup(t => t.CanSendAsync(It.IsAny<string>(), It.IsAny<InterventionType>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _sessionManager.Setup(s => s.GetActiveSessionAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((MeetingSession?)null);

        var action = new InterventionAction
        {
            Type = InterventionType.ChatMessage,
            Content = "Test",
            SessionId = "session1"
        };

        await _orchestrator.ExecuteAsync(action, CancellationToken.None);

        _graphClient.Verify(g => g.SendChatMessageAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void IsPaused_UnknownSession_ReturnsFalse()
    {
        Assert.False(_orchestrator.IsPaused("unknown"));
    }

    [Fact]
    public async Task PauseAndResume_WorkCorrectly()
    {
        await _orchestrator.PauseAsync("session1", CancellationToken.None);
        Assert.True(_orchestrator.IsPaused("session1"));

        await _orchestrator.ResumeAsync("session1", CancellationToken.None);
        Assert.False(_orchestrator.IsPaused("session1"));
    }

    [Fact]
    public async Task EvaluateAsync_UserMention_WithQuestions_ReturnsCriticalPriority()
    {
        var analysis = CreateAnalysisWithHighPriorityQuestion();

        var result = await _orchestrator.EvaluateAsync(
            "session1", InterventionTrigger.UserMention, analysis, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(InterventionPriority.Critical, result.Priority);
        Assert.Equal(InterventionTrigger.UserMention, result.Trigger);
    }

    private static ConversationAnalysis CreateAnalysisWithHighPriorityQuestion() => new()
    {
        Questions = new[]
        {
            new GeneratedQuestion
            {
                Question = "Why was this approach chosen?",
                Type = QuestionType.WhyQuestion,
                Priority = QuestionPriority.High,
                Rationale = "This decision seems to have important background"
            }
        }
    };
}
