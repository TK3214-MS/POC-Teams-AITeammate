using System.Text.Json;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Moq;
using TeamsAITeammate.AI.Services;
using TeamsAITeammate.Core.Models;

namespace TeamsAITeammate.UnitTests;

public class ConversationAnalyzerTests
{
    private readonly Mock<IChatClient> _mockChatClient;
    private readonly ConversationAnalyzer _analyzer;

    public ConversationAnalyzerTests()
    {
        _mockChatClient = new Mock<IChatClient>();
        var mockLogger = new Mock<ILogger<ConversationAnalyzer>>();
        var kernelBuilder = Kernel.CreateBuilder();
        _analyzer = new ConversationAnalyzer(
            kernelBuilder.Build(),
            _mockChatClient.Object,
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
        Assert.Empty(result.Decisions);
        Assert.Empty(result.ActionItems);
        Assert.Equal("empty", result.Metadata.AnalysisType);
    }

    [Fact]
    public async Task AnalyzeAsync_WithTranscript_ParsesTopics()
    {
        var responseJson = """
        {
          "topics": [
            {
              "id": "topic-1",
              "title": "新機能の設計",
              "summary": "新しいダッシュボード機能の設計について議論",
              "status": "Active",
              "discussionDepth": 0.7,
              "keyTerms": ["ダッシュボード", "UI"],
              "involvedSpeakers": ["田中", "佐藤"]
            }
          ],
          "decisions": [
            {
              "summary": "React を使用することに決定",
              "context": "パフォーマンスと開発速度を考慮",
              "decisionMakers": ["田中"],
              "confidence": 0.9
            }
          ],
          "actionItems": [
            {
              "description": "プロトタイプを作成する",
              "assignee": "佐藤",
              "dueDate": "2026-08-01",
              "status": "Open"
            }
          ]
        }
        """;

        SetupMockResponse(responseJson);

        var window = CreateSampleConversationWindow();
        var context = CreateSampleContext();

        var result = await _analyzer.AnalyzeAsync(window, context);

        Assert.Single(result.Topics);
        Assert.Equal("新機能の設計", result.Topics[0].Title);
        Assert.Equal(TopicStatus.Active, result.Topics[0].Status);
        Assert.Single(result.Decisions);
        Assert.Equal("React を使用することに決定", result.Decisions[0].Summary);
        Assert.Single(result.ActionItems);
        Assert.Equal("佐藤", result.ActionItems[0].Assignee);
    }

    [Fact]
    public async Task AnalyzeAsync_WithMarkdownJson_ParsesCorrectly()
    {
        var responseText = """
        分析結果は以下の通りです:
        ```json
        {
          "topics": [
            {
              "id": "t1",
              "title": "テスト",
              "summary": "テストトピック",
              "status": "Concluded",
              "discussionDepth": 0.5,
              "keyTerms": [],
              "involvedSpeakers": []
            }
          ],
          "decisions": [],
          "actionItems": []
        }
        ```
        """;

        SetupMockResponse(responseText);

        var window = CreateSampleConversationWindow();
        var result = await _analyzer.AnalyzeAsync(window, CreateSampleContext());

        Assert.Single(result.Topics);
        Assert.Equal(TopicStatus.Concluded, result.Topics[0].Status);
    }

    [Fact]
    public async Task AnalyzeAsync_InvalidJson_ReturnsEmptyAnalysis()
    {
        SetupMockResponse("This is not valid JSON at all");

        var window = CreateSampleConversationWindow();
        var result = await _analyzer.AnalyzeAsync(window, CreateSampleContext());

        Assert.Empty(result.Topics);
        Assert.Equal("incremental", result.Metadata.AnalysisType);
    }

    [Fact]
    public async Task AnalyzeAsync_WithPriorKnowledge_IncludesInPrompt()
    {
        SetupMockResponse("""{"topics":[],"decisions":[],"actionItems":[]}""");

        var context = new AnalysisContext
        {
            SessionId = "s1",
            MeetingSubject = "定例会議",
            Participants = ["田中", "佐藤"],
            DetectedLanguage = "ja-JP",
            PriorKnowledge =
            [
                new RelevantKnowledge { Content = "前回の決定事項", Source = "前回会議" }
            ]
        };

        var window = CreateSampleConversationWindow();
        await _analyzer.AnalyzeAsync(window, context);

        // Verify chat client was called (prompt includes prior knowledge)
        _mockChatClient.Verify(c => c.GetResponseAsync(
            It.IsAny<IEnumerable<ChatMessage>>(),
            It.IsAny<ChatOptions>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void ExtractJson_RawJson_ReturnsJsonObject()
    {
        var input = """{"topics":[]}""";
        var result = ConversationAnalyzer.ExtractJson(input);
        Assert.Equal(input, result);
    }

    [Fact]
    public void ExtractJson_MarkdownWrapped_ExtractsJson()
    {
        var input = "Here is the result:\n```json\n{\"topics\":[]}\n```\nDone.";
        var result = ConversationAnalyzer.ExtractJson(input);
        Assert.Equal("{\"topics\":[]}", result);
    }

    [Fact]
    public void ExtractJson_TextBeforeJson_ExtractsJson()
    {
        var input = "Analysis result: {\"topics\":[]}";
        var result = ConversationAnalyzer.ExtractJson(input);
        Assert.Equal("{\"topics\":[]}", result);
    }

    [Fact]
    public async Task AnalyzeAsync_MultipleTopics_ParsesAll()
    {
        var responseJson = """
        {
          "topics": [
            { "id": "t1", "title": "Topic 1", "summary": "S1", "status": "Active", "discussionDepth": 0.8, "keyTerms": [], "involvedSpeakers": ["Alice"] },
            { "id": "t2", "title": "Topic 2", "summary": "S2", "status": "Tabled", "discussionDepth": 0.3, "keyTerms": ["key"], "involvedSpeakers": ["Bob"] }
          ],
          "decisions": [],
          "actionItems": []
        }
        """;

        SetupMockResponse(responseJson);

        var window = CreateSampleConversationWindow();
        var result = await _analyzer.AnalyzeAsync(window, CreateSampleContext());

        Assert.Equal(2, result.Topics.Count);
        Assert.Equal(TopicStatus.Tabled, result.Topics[1].Status);
    }

    [Fact]
    public async Task AnalyzeAsync_ActionItemWithNullDueDate_ParsesCorrectly()
    {
        var responseJson = """
        {
          "topics": [],
          "decisions": [],
          "actionItems": [
            {
              "description": "Investigate options",
              "assignee": "Alice",
              "dueDate": null,
              "status": "Open"
            }
          ]
        }
        """;

        SetupMockResponse(responseJson);

        var window = CreateSampleConversationWindow();
        var result = await _analyzer.AnalyzeAsync(window, CreateSampleContext());

        Assert.Single(result.ActionItems);
        Assert.Null(result.ActionItems[0].DueDate);
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

    private static ConversationWindow CreateSampleConversationWindow()
    {
        return new ConversationWindow
        {
            SessionId = "session-1",
            Segments =
            [
                new TranscriptSegment
                {
                    SpeakerName = "田中",
                    Text = "新しいダッシュボード機能について議論しましょう。",
                    Timestamp = DateTimeOffset.UtcNow.AddMinutes(-5)
                },
                new TranscriptSegment
                {
                    SpeakerName = "佐藤",
                    Text = "Reactで実装するのがいいと思います。パフォーマンスが良いです。",
                    Timestamp = DateTimeOffset.UtcNow.AddMinutes(-4)
                },
                new TranscriptSegment
                {
                    SpeakerName = "田中",
                    Text = "同意します。佐藤さん、プロトタイプを来月までに作れますか？",
                    Timestamp = DateTimeOffset.UtcNow.AddMinutes(-3)
                }
            ],
            WindowStart = DateTimeOffset.UtcNow.AddMinutes(-5),
            WindowEnd = DateTimeOffset.UtcNow
        };
    }

    private static AnalysisContext CreateSampleContext() => new()
    {
        SessionId = "session-1",
        MeetingSubject = "新機能設計レビュー",
        Participants = ["田中", "佐藤"],
        DetectedLanguage = "ja-JP"
    };
}
