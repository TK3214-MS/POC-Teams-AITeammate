using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Moq;
using TeamsAITeammate.AI.Services;
using TeamsAITeammate.Core.Models;

namespace TeamsAITeammate.UnitTests;

public class TacitKnowledgeExtractorTests
{
    private readonly Mock<IChatClient> _mockChatClient;
    private readonly TacitKnowledgeExtractor _extractor;

    public TacitKnowledgeExtractorTests()
    {
        _mockChatClient = new Mock<IChatClient>();
        var mockLogger = new Mock<ILogger<TacitKnowledgeExtractor>>();
        _extractor = new TacitKnowledgeExtractor(_mockChatClient.Object, mockLogger.Object);
    }

    [Fact]
    public async Task ExtractAsync_EmptyConversation_ReturnsEmpty()
    {
        var window = new ConversationWindow { Segments = [] };
        var result = await _extractor.ExtractAsync(window, new AnalysisContext());
        Assert.Empty(result);
    }

    [Fact]
    public async Task ExtractAsync_ExtractsDecisionBackground()
    {
        var responseJson = """
        [
          {
            "category": "DecisionBackground",
            "content": "React選定の理由はパフォーマンスとチームの経験",
            "context": "佐藤: Reactが最適だと思います。前のプロジェクトでの経験から",
            "sourceSpeaker": "佐藤",
            "confidence": 0.85,
            "relatedTopics": ["技術選定"],
            "requiresValidation": false
          }
        ]
        """;

        SetupMockResponse(responseJson);
        var result = await _extractor.ExtractAsync(CreateSampleWindow(), CreateSampleContext());

        Assert.Single(result);
        Assert.Equal(TacitKnowledgeCategory.DecisionBackground, result[0].Category);
        Assert.Equal("佐藤", result[0].SourceSpeaker);
        Assert.Equal(0.85f, result[0].Confidence);
        Assert.False(result[0].RequiresValidation);
    }

    [Fact]
    public async Task ExtractAsync_ExtractsUndocumentedProcess()
    {
        var responseJson = """
        [
          {
            "category": "UndocumentedProcess",
            "content": "デプロイは金曜日の午後に行うのが慣例",
            "context": "田中: いつも金曜の午後にデプロイしています",
            "sourceSpeaker": "田中",
            "confidence": 0.9,
            "relatedTopics": ["デプロイ"],
            "requiresValidation": true
          }
        ]
        """;

        SetupMockResponse(responseJson);
        var result = await _extractor.ExtractAsync(CreateSampleWindow(), CreateSampleContext());

        Assert.Single(result);
        Assert.Equal(TacitKnowledgeCategory.UndocumentedProcess, result[0].Category);
        Assert.True(result[0].RequiresValidation);
    }

    [Fact]
    public async Task ExtractAsync_ExtractsExpertKnowledge()
    {
        var responseJson = """
        [
          {
            "category": "ExpertKnowledge",
            "content": "大規模データセットではGraphQLよりREST APIの方がキャッシュしやすい",
            "context": "佐藤: 私の経験では大規模データセットではREST APIの方が...",
            "sourceSpeaker": "佐藤",
            "confidence": 0.75,
            "relatedTopics": ["API設計"],
            "requiresValidation": false
          }
        ]
        """;

        SetupMockResponse(responseJson);
        var result = await _extractor.ExtractAsync(CreateSampleWindow(), CreateSampleContext());

        Assert.Single(result);
        Assert.Equal(TacitKnowledgeCategory.ExpertKnowledge, result[0].Category);
    }

    [Fact]
    public async Task ExtractAsync_ExtractsLessonsLearned()
    {
        var responseJson = """
        [
          {
            "category": "LessonsLearned",
            "content": "前回のリリースでデータベースマイグレーションの順序を間違えてロールバックが必要になった",
            "context": "田中: 前に失敗した時は、マイグレーションの順序が原因でした",
            "sourceSpeaker": "田中",
            "confidence": 0.88,
            "relatedTopics": ["データベース", "リリース"],
            "requiresValidation": false
          }
        ]
        """;

        SetupMockResponse(responseJson);
        var result = await _extractor.ExtractAsync(CreateSampleWindow(), CreateSampleContext());

        Assert.Single(result);
        Assert.Equal(TacitKnowledgeCategory.LessonsLearned, result[0].Category);
    }

    [Fact]
    public async Task ExtractAsync_FiltersLowConfidence()
    {
        var responseJson = """
        [
          {
            "category": "ExpertKnowledge",
            "content": "High confidence",
            "context": "ctx",
            "sourceSpeaker": "A",
            "confidence": 0.8,
            "relatedTopics": [],
            "requiresValidation": false
          },
          {
            "category": "ExpertKnowledge",
            "content": "Low confidence should be filtered",
            "context": "ctx",
            "sourceSpeaker": "B",
            "confidence": 0.3,
            "relatedTopics": [],
            "requiresValidation": true
          }
        ]
        """;

        SetupMockResponse(responseJson);
        var result = await _extractor.ExtractAsync(CreateSampleWindow(), CreateSampleContext());

        Assert.Single(result);
        Assert.Equal("High confidence", result[0].Content);
    }

    [Fact]
    public async Task ExtractAsync_MultipleCategories_AllParsed()
    {
        var responseJson = """
        [
          { "category": "DecisionBackground", "content": "C1", "context": "ctx", "sourceSpeaker": "A", "confidence": 0.8, "relatedTopics": [], "requiresValidation": false },
          { "category": "TechnicalInsight", "content": "C2", "context": "ctx", "sourceSpeaker": "B", "confidence": 0.7, "relatedTopics": [], "requiresValidation": true },
          { "category": "OrganizationalContext", "content": "C3", "context": "ctx", "sourceSpeaker": "C", "confidence": 0.6, "relatedTopics": [], "requiresValidation": false },
          { "category": "ImplicitAssumption", "content": "C4", "context": "ctx", "sourceSpeaker": "A", "confidence": 0.9, "relatedTopics": [], "requiresValidation": true },
          { "category": "DomainExpertise", "content": "C5", "context": "ctx", "sourceSpeaker": "B", "confidence": 0.85, "relatedTopics": [], "requiresValidation": false }
        ]
        """;

        SetupMockResponse(responseJson);
        var result = await _extractor.ExtractAsync(CreateSampleWindow(), CreateSampleContext());

        Assert.Equal(5, result.Count);
        Assert.Equal(TacitKnowledgeCategory.DecisionBackground, result[0].Category);
        Assert.Equal(TacitKnowledgeCategory.TechnicalInsight, result[1].Category);
        Assert.Equal(TacitKnowledgeCategory.OrganizationalContext, result[2].Category);
        Assert.Equal(TacitKnowledgeCategory.ImplicitAssumption, result[3].Category);
        Assert.Equal(TacitKnowledgeCategory.DomainExpertise, result[4].Category);
    }

    [Fact]
    public async Task ExtractAsync_InvalidJson_ReturnsEmpty()
    {
        SetupMockResponse("Could not extract any tacit knowledge.");
        var result = await _extractor.ExtractAsync(CreateSampleWindow(), CreateSampleContext());
        Assert.Empty(result);
    }

    [Fact]
    public void ParseCandidates_ArrayFormat_Parses()
    {
        var json = """[{"category":"ExpertKnowledge","content":"C1","context":"ctx","sourceSpeaker":"A","confidence":0.8,"relatedTopics":[],"requiresValidation":false}]""";
        var result = TacitKnowledgeExtractor.ParseCandidates(json);
        Assert.Single(result);
    }

    [Fact]
    public void ParseCandidates_WrappedFormat_Parses()
    {
        var json = """{"candidates":[{"category":"ExpertKnowledge","content":"C1","context":"ctx","sourceSpeaker":"A","confidence":0.8,"relatedTopics":[],"requiresValidation":false}]}""";
        var result = TacitKnowledgeExtractor.ParseCandidates(json);
        Assert.Single(result);
    }

    [Fact]
    public async Task ExtractAsync_UnknownCategory_DefaultsToExpertKnowledge()
    {
        var responseJson = """
        [
          {
            "category": "UnknownCategory",
            "content": "C1",
            "context": "ctx",
            "sourceSpeaker": "A",
            "confidence": 0.8,
            "relatedTopics": [],
            "requiresValidation": false
          }
        ]
        """;

        SetupMockResponse(responseJson);
        var result = await _extractor.ExtractAsync(CreateSampleWindow(), CreateSampleContext());

        Assert.Single(result);
        Assert.Equal(TacitKnowledgeCategory.ExpertKnowledge, result[0].Category);
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

    private static ConversationWindow CreateSampleWindow() => new()
    {
        SessionId = "s1",
        Segments =
        [
            new TranscriptSegment { SpeakerName = "田中", Text = "いつも金曜の午後にデプロイしています", Timestamp = DateTimeOffset.UtcNow.AddMinutes(-5) },
            new TranscriptSegment { SpeakerName = "佐藤", Text = "私の経験ではREST APIの方がキャッシュしやすいです", Timestamp = DateTimeOffset.UtcNow.AddMinutes(-4) }
        ],
        WindowStart = DateTimeOffset.UtcNow.AddMinutes(-5),
        WindowEnd = DateTimeOffset.UtcNow
    };

    private static AnalysisContext CreateSampleContext() => new()
    {
        SessionId = "s1",
        MeetingSubject = "技術レビュー",
        Participants = ["田中", "佐藤"],
        DetectedLanguage = "ja-JP"
    };
}
