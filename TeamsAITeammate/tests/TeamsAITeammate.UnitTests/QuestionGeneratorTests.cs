using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Moq;
using TeamsAITeammate.AI.Services;
using TeamsAITeammate.Core.Models;

namespace TeamsAITeammate.UnitTests;

public class QuestionGeneratorTests
{
    private readonly Mock<IChatClient> _mockChatClient;
    private readonly QuestionGenerator _generator;

    public QuestionGeneratorTests()
    {
        _mockChatClient = new Mock<IChatClient>();
        var mockLogger = new Mock<ILogger<QuestionGenerator>>();
        _generator = new QuestionGenerator(_mockChatClient.Object, mockLogger.Object);
    }

    [Fact]
    public async Task GenerateQuestionsAsync_EmptyConversation_ReturnsEmpty()
    {
        var window = new ConversationWindow { Segments = [] };
        var result = await _generator.GenerateQuestionsAsync(
            window, new AnalysisContext(),
            new QuestionGenerationOptions());

        Assert.Empty(result);
    }

    [Fact]
    public async Task GenerateQuestionsAsync_WithTranscript_GeneratesQuestions()
    {
        var responseJson = """
        [
          {
            "question": "なぜReactを選択したのですか？他のフレームワークは検討しましたか？",
            "type": "WhyQuestion",
            "priority": "High",
            "rationale": "技術選定の根拠を明確にするため",
            "targetSpeaker": "佐藤",
            "relatedTopicId": "topic-1",
            "expectedKnowledgeCategory": "DecisionBackground"
          },
          {
            "question": "パフォーマンス要件の具体的な数値目標はありますか？",
            "type": "ClarificationQuestion",
            "priority": "Medium",
            "rationale": "要件の明確化",
            "targetSpeaker": "田中",
            "relatedTopicId": "topic-1",
            "expectedKnowledgeCategory": "DomainExpertise"
          }
        ]
        """;

        SetupMockResponse(responseJson);

        var window = CreateSampleWindow();
        var result = await _generator.GenerateQuestionsAsync(
            window, CreateSampleContext(),
            new QuestionGenerationOptions { MaxQuestions = 5 });

        Assert.Equal(2, result.Count);
        Assert.Equal(QuestionType.WhyQuestion, result[0].Type);
        Assert.Equal(QuestionPriority.High, result[0].Priority);
        Assert.Equal("佐藤", result[0].TargetSpeaker);
        Assert.Equal(TacitKnowledgeCategory.DecisionBackground, result[0].ExpectedKnowledgeCategory);
    }

    [Fact]
    public async Task GenerateQuestionsAsync_RespectsMaxQuestions()
    {
        var responseJson = """
        [
          { "question": "Q1", "type": "WhyQuestion", "priority": "High", "rationale": "R1", "targetSpeaker": "A", "relatedTopicId": "", "expectedKnowledgeCategory": "ExpertKnowledge" },
          { "question": "Q2", "type": "RiskQuestion", "priority": "Medium", "rationale": "R2", "targetSpeaker": "B", "relatedTopicId": "", "expectedKnowledgeCategory": "TechnicalInsight" },
          { "question": "Q3", "type": "ProcessQuestion", "priority": "Low", "rationale": "R3", "targetSpeaker": "C", "relatedTopicId": "", "expectedKnowledgeCategory": "UndocumentedProcess" }
        ]
        """;

        SetupMockResponse(responseJson);

        var result = await _generator.GenerateQuestionsAsync(
            CreateSampleWindow(), CreateSampleContext(),
            new QuestionGenerationOptions { MaxQuestions = 2 });

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task GenerateQuestionsAsync_AllQuestionTypes_ParsedCorrectly()
    {
        var types = new[] { "WhyQuestion", "ImpactQuestion", "ClarificationQuestion",
            "AlternativeQuestion", "TimelineQuestion", "StakeholderQuestion",
            "RiskQuestion", "ProcessQuestion", "PrecedentQuestion", "AssumptionQuestion" };

        var questions = types.Select((t, i) => $$"""
            { "question": "Q{{i}}", "type": "{{t}}", "priority": "Medium", "rationale": "R", "targetSpeaker": "A", "relatedTopicId": "", "expectedKnowledgeCategory": "ExpertKnowledge" }
            """);

        var responseJson = $"[{string.Join(',', questions)}]";
        SetupMockResponse(responseJson);

        var result = await _generator.GenerateQuestionsAsync(
            CreateSampleWindow(), CreateSampleContext(),
            new QuestionGenerationOptions { MaxQuestions = 10 });

        Assert.Equal(10, result.Count);
        for (int i = 0; i < types.Length; i++)
        {
            Assert.Equal(Enum.Parse<QuestionType>(types[i]), result[i].Type);
        }
    }

    [Fact]
    public async Task GenerateQuestionsAsync_InvalidJson_ReturnsEmpty()
    {
        SetupMockResponse("I cannot generate questions for this conversation.");

        var result = await _generator.GenerateQuestionsAsync(
            CreateSampleWindow(), CreateSampleContext(),
            new QuestionGenerationOptions());

        Assert.Empty(result);
    }

    [Fact]
    public async Task GenerateQuestionsAsync_AllPriorities_ParsedCorrectly()
    {
        var responseJson = """
        [
          { "question": "Q1", "type": "WhyQuestion", "priority": "Critical", "rationale": "R", "targetSpeaker": "A", "relatedTopicId": "", "expectedKnowledgeCategory": "ExpertKnowledge" },
          { "question": "Q2", "type": "RiskQuestion", "priority": "High", "rationale": "R", "targetSpeaker": "A", "relatedTopicId": "", "expectedKnowledgeCategory": "ExpertKnowledge" },
          { "question": "Q3", "type": "ProcessQuestion", "priority": "Medium", "rationale": "R", "targetSpeaker": "A", "relatedTopicId": "", "expectedKnowledgeCategory": "ExpertKnowledge" },
          { "question": "Q4", "type": "AssumptionQuestion", "priority": "Low", "rationale": "R", "targetSpeaker": "A", "relatedTopicId": "", "expectedKnowledgeCategory": "ExpertKnowledge" }
        ]
        """;

        SetupMockResponse(responseJson);

        var result = await _generator.GenerateQuestionsAsync(
            CreateSampleWindow(), CreateSampleContext(),
            new QuestionGenerationOptions { MaxQuestions = 10 });

        Assert.Equal(QuestionPriority.Critical, result[0].Priority);
        Assert.Equal(QuestionPriority.High, result[1].Priority);
        Assert.Equal(QuestionPriority.Medium, result[2].Priority);
        Assert.Equal(QuestionPriority.Low, result[3].Priority);
    }

    [Fact]
    public void ParseQuestions_ArrayFormat_Parses()
    {
        var json = """[{"question":"Q1","type":"WhyQuestion","priority":"High","rationale":"R","targetSpeaker":"A","relatedTopicId":"","expectedKnowledgeCategory":"ExpertKnowledge"}]""";
        var result = QuestionGenerator.ParseQuestions(json);
        Assert.Single(result);
        Assert.Equal("Q1", result[0].Question);
    }

    [Fact]
    public void ParseQuestions_WrappedFormat_Parses()
    {
        var json = """{"questions":[{"question":"Q1","type":"WhyQuestion","priority":"High","rationale":"R","targetSpeaker":"A","relatedTopicId":"","expectedKnowledgeCategory":"ExpertKnowledge"}]}""";
        var result = QuestionGenerator.ParseQuestions(json);
        Assert.Single(result);
    }

    [Fact]
    public void ParseQuestions_InvalidJson_ReturnsEmpty()
    {
        var result = QuestionGenerator.ParseQuestions("not json");
        Assert.Empty(result);
    }

    [Fact]
    public async Task GenerateQuestionsAsync_UnknownType_DefaultsToClarification()
    {
        var responseJson = """
        [
          { "question": "Q1", "type": "UnknownType", "priority": "High", "rationale": "R", "targetSpeaker": "A", "relatedTopicId": "", "expectedKnowledgeCategory": "ExpertKnowledge" }
        ]
        """;

        SetupMockResponse(responseJson);

        var result = await _generator.GenerateQuestionsAsync(
            CreateSampleWindow(), CreateSampleContext(),
            new QuestionGenerationOptions());

        Assert.Single(result);
        Assert.Equal(QuestionType.ClarificationQuestion, result[0].Type);
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
            new TranscriptSegment { SpeakerName = "田中", Text = "新機能を議論しましょう", Timestamp = DateTimeOffset.UtcNow.AddMinutes(-5) },
            new TranscriptSegment { SpeakerName = "佐藤", Text = "Reactが最適だと思います", Timestamp = DateTimeOffset.UtcNow.AddMinutes(-4) }
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
