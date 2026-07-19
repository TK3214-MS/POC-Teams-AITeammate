using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Moq;
using TeamsAITeammate.AI.Services;
using TeamsAITeammate.Core.Interfaces;
using TeamsAITeammate.Core.Models;

namespace TeamsAITeammate.UnitTests;

public class RagEnhancedConversationAnalyzerTests
{
    private readonly Mock<IChatClient> _mockChatClient;
    private readonly Mock<IKnowledgeRetriever> _mockRetriever;
    private readonly RagEnhancedConversationAnalyzer _analyzer;

    public RagEnhancedConversationAnalyzerTests()
    {
        _mockChatClient = new Mock<IChatClient>();
        _mockRetriever = new Mock<IKnowledgeRetriever>();
        var mockLogger = new Mock<ILogger<RagEnhancedConversationAnalyzer>>();
        var kernelBuilder = Kernel.CreateBuilder();
        _analyzer = new RagEnhancedConversationAnalyzer(
            kernelBuilder.Build(),
            _mockChatClient.Object,
            _mockRetriever.Object,
            mockLogger.Object);
    }

    [Fact]
    public async Task AnalyzeAsync_EmptyConversation_ReturnsEmptyAnalysis()
    {
        var window = new ConversationWindow
        {
            SessionId = "s1",
            Segments = [],
            WindowStart = DateTimeOffset.UtcNow.AddMinutes(-5),
            WindowEnd = DateTimeOffset.UtcNow
        };

        var result = await _analyzer.AnalyzeAsync(window, new AnalysisContext());

        Assert.Empty(result.Topics);
        Assert.Equal("empty", result.Metadata.AnalysisType);
    }

    [Fact]
    public async Task AnalyzeAsync_WithTranscript_CallsRetriever()
    {
        _mockRetriever
            .Setup(r => r.RetrieveAsync(It.IsAny<RetrievalQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<RetrievalResult>());

        SetupMockResponse("""{"topics":[],"decisions":[],"actionItems":[]}""");

        var window = CreateSampleConversationWindow();
        var context = CreateSampleContext();

        await _analyzer.AnalyzeAsync(window, context);

        _mockRetriever.Verify(
            r => r.RetrieveAsync(It.IsAny<RetrievalQuery>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task AnalyzeAsync_RetrieverFails_FallsBackToBaseAnalyzer()
    {
        _mockRetriever
            .Setup(r => r.RetrieveAsync(It.IsAny<RetrievalQuery>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Search unavailable"));

        SetupMockResponse("""{"topics":[{"id":"t1","title":"Test","summary":"S","status":"Active","discussionDepth":0.5,"keyTerms":[],"involvedSpeakers":[]}],"decisions":[],"actionItems":[]}""");

        var window = CreateSampleConversationWindow();
        var result = await _analyzer.AnalyzeAsync(window, CreateSampleContext());

        Assert.Single(result.Topics);
        Assert.Equal("rag-enhanced", result.Metadata.AnalysisType);
    }

    [Fact]
    public async Task AnalyzeAsync_WithRetrievedKnowledge_SetsRagEnhancedType()
    {
        _mockRetriever
            .Setup(r => r.RetrieveAsync(It.IsAny<RetrievalQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<RetrievalResult>
            {
                new()
                {
                    Entry = new KnowledgeEntry
                    {
                        Id = "k1",
                        Content = "Past decision about React",
                        MeetingSubject = "Previous Meeting",
                        MeetingDate = DateTimeOffset.UtcNow.AddDays(-7)
                    },
                    RelevanceScore = 0.85f,
                    Source = RetrievalSource.HybridSearch
                }
            });

        SetupMockResponse("""{"topics":[],"decisions":[],"actionItems":[]}""");

        var window = CreateSampleConversationWindow();
        var result = await _analyzer.AnalyzeAsync(window, CreateSampleContext());

        Assert.Equal("rag-enhanced", result.Metadata.AnalysisType);
    }

    [Fact]
    public void BuildRagQuery_WithSubjectAndSegments_CombinesAll()
    {
        var window = new ConversationWindow
        {
            SessionId = "s1",
            Segments =
            [
                new TranscriptSegment { Text = "First segment", Timestamp = DateTimeOffset.UtcNow.AddMinutes(-2) },
                new TranscriptSegment { Text = "Second segment", Timestamp = DateTimeOffset.UtcNow.AddMinutes(-1) }
            ],
            WindowStart = DateTimeOffset.UtcNow.AddMinutes(-5),
            WindowEnd = DateTimeOffset.UtcNow
        };

        var context = new AnalysisContext { MeetingSubject = "Project Review" };

        var query = RagEnhancedConversationAnalyzer.BuildRagQuery(window, context);

        Assert.Contains("Project Review", query);
        Assert.Contains("Second segment", query);
        Assert.Contains("First segment", query);
    }

    [Fact]
    public void BuildRagQuery_EmptySubject_UsesSegmentsOnly()
    {
        var window = new ConversationWindow
        {
            SessionId = "s1",
            Segments =
            [
                new TranscriptSegment { Text = "Some text", Timestamp = DateTimeOffset.UtcNow }
            ],
            WindowStart = DateTimeOffset.UtcNow.AddMinutes(-5),
            WindowEnd = DateTimeOffset.UtcNow
        };

        var context = new AnalysisContext();
        var query = RagEnhancedConversationAnalyzer.BuildRagQuery(window, context);

        Assert.Contains("Some text", query);
    }

    [Fact]
    public void EnrichContext_NoRetrievedKnowledge_ReturnsOriginal()
    {
        var context = new AnalysisContext
        {
            SessionId = "s1",
            PriorKnowledge = [new RelevantKnowledge { Content = "existing" }]
        };

        var result = RagEnhancedConversationAnalyzer.EnrichContext(context, []);

        Assert.Single(result.PriorKnowledge);
        Assert.Equal("existing", result.PriorKnowledge[0].Content);
    }

    [Fact]
    public void EnrichContext_WithRetrievedKnowledge_AddsToContext()
    {
        var context = new AnalysisContext
        {
            SessionId = "s1",
            PriorKnowledge = [new RelevantKnowledge { Content = "existing" }]
        };

        var retrieved = new List<RetrievalResult>
        {
            new()
            {
                Entry = new KnowledgeEntry
                {
                    Id = "k1",
                    Content = "retrieved knowledge",
                    MeetingSubject = "Past Meeting",
                    MeetingDate = new DateTimeOffset(2026, 7, 10, 0, 0, 0, TimeSpan.Zero)
                },
                RelevanceScore = 0.9f,
                Source = RetrievalSource.HybridSearch
            }
        };

        var result = RagEnhancedConversationAnalyzer.EnrichContext(context, retrieved);

        Assert.Equal(2, result.PriorKnowledge.Count);
        Assert.Equal("existing", result.PriorKnowledge[0].Content);
        Assert.Equal("retrieved knowledge", result.PriorKnowledge[1].Content);
        Assert.Contains("Past Meeting", result.PriorKnowledge[1].Source);
        Assert.Equal(0.9f, result.PriorKnowledge[1].RelevanceScore);
    }

    [Fact]
    public void DetectContradictions_NoDecisionsNoKnowledge_ReturnsEmpty()
    {
        var analysis = new ConversationAnalysis();
        var result = RagEnhancedConversationAnalyzer.DetectContradictions(analysis, []);
        Assert.Empty(result);
    }

    [Fact]
    public void DetectContradictions_HighRelevanceDecisionBackground_Flags()
    {
        var analysis = new ConversationAnalysis
        {
            Decisions =
            [
                new DetectedDecision { Summary = "Use Vue.js" }
            ]
        };

        var retrieved = new List<RetrievalResult>
        {
            new()
            {
                Entry = new KnowledgeEntry
                {
                    Category = TacitKnowledgeCategory.DecisionBackground,
                    Content = "Team decided to use React"
                },
                RelevanceScore = 0.85f
            }
        };

        var result = RagEnhancedConversationAnalyzer.DetectContradictions(analysis, retrieved);

        Assert.Single(result);
        Assert.Contains("React", result[0]);
        Assert.Contains("Use Vue.js", result[0]);
    }

    [Fact]
    public void DetectContradictions_LowRelevance_DoesNotFlag()
    {
        var analysis = new ConversationAnalysis
        {
            Decisions = [new DetectedDecision { Summary = "decision" }]
        };

        var retrieved = new List<RetrievalResult>
        {
            new()
            {
                Entry = new KnowledgeEntry
                {
                    Category = TacitKnowledgeCategory.DecisionBackground,
                    Content = "old decision"
                },
                RelevanceScore = 0.5f // Below 0.8 threshold
            }
        };

        var result = RagEnhancedConversationAnalyzer.DetectContradictions(analysis, retrieved);
        Assert.Empty(result);
    }

    [Fact]
    public void DetectContradictions_NonDecisionCategory_DoesNotFlag()
    {
        var analysis = new ConversationAnalysis
        {
            Decisions = [new DetectedDecision { Summary = "decision" }]
        };

        var retrieved = new List<RetrievalResult>
        {
            new()
            {
                Entry = new KnowledgeEntry
                {
                    Category = TacitKnowledgeCategory.ExpertKnowledge,
                    Content = "some knowledge"
                },
                RelevanceScore = 0.95f
            }
        };

        var result = RagEnhancedConversationAnalyzer.DetectContradictions(analysis, retrieved);
        Assert.Empty(result);
    }

    private void SetupMockResponse(string responseText)
    {
        var response = new ChatResponse(new ChatMessage(ChatRole.Assistant, responseText));
        _mockChatClient
            .Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);
    }

    private static ConversationWindow CreateSampleConversationWindow() => new()
    {
        SessionId = "session-1",
        Segments =
        [
            new TranscriptSegment
            {
                SpeakerName = "Alice",
                Text = "Let's discuss the architecture",
                Timestamp = DateTimeOffset.UtcNow.AddMinutes(-3)
            },
            new TranscriptSegment
            {
                SpeakerName = "Bob",
                Text = "I think we should use microservices",
                Timestamp = DateTimeOffset.UtcNow.AddMinutes(-2)
            }
        ],
        WindowStart = DateTimeOffset.UtcNow.AddMinutes(-5),
        WindowEnd = DateTimeOffset.UtcNow
    };

    private static AnalysisContext CreateSampleContext() => new()
    {
        SessionId = "session-1",
        MeetingSubject = "Architecture Review",
        Participants = ["Alice", "Bob"],
        DetectedLanguage = "en-US"
    };
}
