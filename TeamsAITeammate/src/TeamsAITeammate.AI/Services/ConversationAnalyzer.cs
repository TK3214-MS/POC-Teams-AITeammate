using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using TeamsAITeammate.Core.Interfaces;
using TeamsAITeammate.Core.Models;

namespace TeamsAITeammate.AI.Services;

public class ConversationAnalyzer : IConversationAnalyzer
{
    private readonly Kernel _kernel;
    private readonly IChatClient _chatClient;
    private readonly ILogger<ConversationAnalyzer> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public ConversationAnalyzer(
        Kernel kernel,
        IChatClient chatClient,
        ILogger<ConversationAnalyzer> logger)
    {
        _kernel = kernel;
        _chatClient = chatClient;
        _logger = logger;
    }

    public async Task<ConversationAnalysis> AnalyzeAsync(
        ConversationWindow conversation,
        AnalysisContext context,
        CancellationToken ct = default)
    {
        if (conversation.Segments.Count == 0)
        {
            return new ConversationAnalysis
            {
                Metadata = new AnalysisMetadata
                {
                    AnalysisType = "empty",
                    AnalysisDuration = TimeSpan.Zero
                }
            };
        }

        var sw = Stopwatch.StartNew();
        var transcript = conversation.ToFormattedTranscript();

        var priorKnowledge = context.PriorKnowledge.Count > 0
            ? string.Join('\n', context.PriorKnowledge.Select(k => $"- {k.Content} (Source: {k.Source})"))
            : "なし";

        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, GetAnalysisSystemPrompt()),
            new(ChatRole.User, BuildAnalysisUserPrompt(
                transcript, context.MeetingSubject,
                string.Join(", ", context.Participants),
                context.DetectedLanguage, priorKnowledge))
        };

        try
        {
            var response = await _chatClient.GetResponseAsync(messages, cancellationToken: ct);
            var responseText = response.Text ?? string.Empty;
            var jsonText = ExtractJson(responseText);
            var rawAnalysis = JsonSerializer.Deserialize<RawAnalysisResponse>(jsonText, JsonOptions);

            sw.Stop();

            return MapToConversationAnalysis(rawAnalysis, sw.Elapsed);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse analysis response as JSON");
            sw.Stop();
            return new ConversationAnalysis
            {
                Metadata = new AnalysisMetadata
                {
                    AnalysisType = "incremental",
                    AnalysisDuration = sw.Elapsed
                }
            };
        }
    }

    internal static string ExtractJson(string text)
    {
        // Try to find JSON between ```json ... ``` markers
        var jsonStart = text.IndexOf("```json", StringComparison.OrdinalIgnoreCase);
        if (jsonStart >= 0)
        {
            jsonStart = text.IndexOf('\n', jsonStart) + 1;
            var jsonEnd = text.IndexOf("```", jsonStart, StringComparison.Ordinal);
            if (jsonEnd > jsonStart)
                return text[jsonStart..jsonEnd].Trim();
        }

        // Find earliest JSON structure start
        var braceStart = text.IndexOf('{');
        var bracketStart = text.IndexOf('[');

        // If array starts before object (or no object found), extract array
        if (bracketStart >= 0 && (braceStart < 0 || bracketStart < braceStart))
        {
            var bracketEnd = text.LastIndexOf(']');
            if (bracketEnd > bracketStart)
                return text[bracketStart..(bracketEnd + 1)];
        }

        // Otherwise extract object
        if (braceStart >= 0)
        {
            var braceEnd = text.LastIndexOf('}');
            if (braceEnd > braceStart)
                return text[braceStart..(braceEnd + 1)];
        }

        return text;
    }

    private static ConversationAnalysis MapToConversationAnalysis(
        RawAnalysisResponse? raw, TimeSpan duration)
    {
        if (raw is null)
            return new ConversationAnalysis
            {
                Metadata = new AnalysisMetadata
                {
                    AnalysisType = "incremental",
                    AnalysisDuration = duration
                }
            };

        var topics = raw.Topics?.Select(t => new DetectedTopic
        {
            Title = t.Title ?? string.Empty,
            Summary = t.Summary ?? string.Empty,
            Status = Enum.TryParse<TopicStatus>(t.Status, true, out var s) ? s : TopicStatus.Active,
            DiscussionDepth = t.DiscussionDepth,
            KeyTerms = t.KeyTerms ?? [],
            InvolvedSpeakers = t.InvolvedSpeakers ?? []
        }).ToList() ?? [];

        var decisions = raw.Decisions?.Select(d => new DetectedDecision
        {
            Summary = d.Summary ?? string.Empty,
            Context = d.Context ?? string.Empty,
            DecisionMakers = d.DecisionMakers ?? [],
            DetectedAt = DateTimeOffset.UtcNow,
            Confidence = d.Confidence
        }).ToList() ?? [];

        var actionItems = raw.ActionItems?.Select(a => new ActionItem
        {
            Description = a.Description ?? string.Empty,
            Assignee = a.Assignee ?? string.Empty,
            DueDate = DateTimeOffset.TryParse(a.DueDate, out var d) ? d : null,
            Status = ActionItemStatus.Open
        }).ToList() ?? [];

        return new ConversationAnalysis
        {
            Topics = topics,
            Decisions = decisions,
            ActionItems = actionItems,
            Metadata = new AnalysisMetadata
            {
                AnalysisType = "incremental",
                AnalysisDuration = duration
            }
        };
    }

    private static string GetAnalysisSystemPrompt() =>
        """
        あなたは会議トランスクリプトを分析する専門AIアシスタントです。
        構造化された分析結果をJSON形式で出力してください。
        JSON以外のテキストは出力しないでください。
        """;

    private static string BuildAnalysisUserPrompt(
        string transcript, string subject, string participants,
        string language, string priorKnowledge) =>
        $$"""
        ## 会議情報
        - 件名: {{subject}}
        - 参加者: {{participants}}
        - 言語: {{language}}

        ## 関連する過去のナレッジ
        {{priorKnowledge}}

        ## トランスクリプト
        {{transcript}}

        以下のJSON形式で分析結果を出力してください:
        {
          "topics": [
            {
              "id": "topic-1",
              "title": "トピックタイトル",
              "summary": "要約",
              "status": "Active|Concluded|Tabled",
              "discussionDepth": 0.0-1.0,
              "keyTerms": ["キーワード"],
              "involvedSpeakers": ["話者名"]
            }
          ],
          "decisions": [
            {
              "summary": "決定事項",
              "context": "背景",
              "decisionMakers": ["決定者名"],
              "confidence": 0.0-1.0
            }
          ],
          "actionItems": [
            {
              "description": "アクション",
              "assignee": "担当者",
              "dueDate": "YYYY-MM-DD or null",
              "status": "Open"
            }
          ]
        }
        """;

    // Raw JSON DTOs for deserialization
    internal record RawAnalysisResponse
    {
        public List<RawTopic>? Topics { get; init; }
        public List<RawDecision>? Decisions { get; init; }
        public List<RawActionItem>? ActionItems { get; init; }
    }

    internal record RawTopic
    {
        public string? Id { get; init; }
        public string? Title { get; init; }
        public string? Summary { get; init; }
        public string? Status { get; init; }
        public float DiscussionDepth { get; init; }
        public List<string>? KeyTerms { get; init; }
        public List<string>? InvolvedSpeakers { get; init; }
    }

    internal record RawDecision
    {
        public string? Summary { get; init; }
        public string? Context { get; init; }
        public List<string>? DecisionMakers { get; init; }
        public float Confidence { get; init; }
    }

    internal record RawActionItem
    {
        public string? Description { get; init; }
        public string? Assignee { get; init; }
        public string? DueDate { get; init; }
        public string? Status { get; init; }
    }
}
