using System.Text.Json;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using TeamsAITeammate.Core.Interfaces;
using TeamsAITeammate.Core.Models;

namespace TeamsAITeammate.AI.Services;

public class QuestionGenerator : IQuestionGenerator
{
    private readonly IChatClient _chatClient;
    private readonly ILogger<QuestionGenerator> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public QuestionGenerator(
        IChatClient chatClient,
        ILogger<QuestionGenerator> logger)
    {
        _chatClient = chatClient;
        _logger = logger;
    }

    public async Task<IReadOnlyList<GeneratedQuestion>> GenerateQuestionsAsync(
        ConversationWindow conversation,
        AnalysisContext context,
        QuestionGenerationOptions options,
        CancellationToken ct = default)
    {
        if (conversation.Segments.Count == 0)
            return [];

        var transcript = conversation.ToFormattedTranscript();
        var alreadyAsked = options.AlreadyAskedQuestionIds.Count > 0
            ? string.Join('\n', options.AlreadyAskedQuestionIds.Select(id => $"- {id}"))
            : "なし";

        var preferredTypes = options.PreferredTypes.Count > 0
            ? string.Join(", ", options.PreferredTypes)
            : "全タイプ";

        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, GetQuestionSystemPrompt()),
            new(ChatRole.User, BuildQuestionUserPrompt(
                transcript, context.MeetingSubject,
                string.Join(", ", context.Participants),
                context.DetectedLanguage,
                options.MaxQuestions, alreadyAsked, preferredTypes))
        };

        try
        {
            var response = await _chatClient.GetResponseAsync(messages, cancellationToken: ct);
            var responseText = response.Text ?? string.Empty;
            var jsonText = ConversationAnalyzer.ExtractJson(responseText);

            // Could be array or object with array
            var rawQuestions = ParseQuestions(jsonText);

            var questions = rawQuestions
                .Select(MapToGeneratedQuestion)
                .ToList();

            if (options.AvoidDuplicates && options.AlreadyAskedQuestionIds.Count > 0)
            {
                questions = questions
                    .Where(q => !options.AlreadyAskedQuestionIds.Contains(q.Id))
                    .ToList();
            }

            return questions.Take(options.MaxQuestions).ToList();
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse question generation response");
            return [];
        }
    }

    internal static List<RawQuestion> ParseQuestions(string jsonText)
    {
        // Try parsing as array first
        try
        {
            return JsonSerializer.Deserialize<List<RawQuestion>>(jsonText, JsonOptions) ?? [];
        }
        catch (JsonException)
        {
            // Try as wrapped object
            try
            {
                var wrapper = JsonSerializer.Deserialize<RawQuestionWrapper>(jsonText, JsonOptions);
                return wrapper?.Questions ?? [];
            }
            catch (JsonException)
            {
                return [];
            }
        }
    }

    private static GeneratedQuestion MapToGeneratedQuestion(RawQuestion raw) => new()
    {
        Question = raw.Question ?? string.Empty,
        Type = Enum.TryParse<QuestionType>(raw.Type, true, out var t) ? t : QuestionType.ClarificationQuestion,
        Priority = Enum.TryParse<QuestionPriority>(raw.Priority, true, out var p) ? p : QuestionPriority.Medium,
        Rationale = raw.Rationale ?? string.Empty,
        TargetSpeaker = raw.TargetSpeaker ?? string.Empty,
        RelatedTopicId = raw.RelatedTopicId ?? string.Empty,
        ExpectedKnowledgeCategory = Enum.TryParse<TacitKnowledgeCategory>(
            raw.ExpectedKnowledgeCategory, true, out var k) ? k : TacitKnowledgeCategory.ExpertKnowledge
    };

    private static string GetQuestionSystemPrompt() =>
        """
        あなたは会議の議論を深め、暗黙知を引き出すための質問を生成する専門AIアシスタントです。
        質問はJSON配列形式で出力してください。
        JSON以外のテキストは出力しないでください。
        """;

    private static string BuildQuestionUserPrompt(
        string transcript, string subject, string participants,
        string language, int maxQuestions, string alreadyAsked, string preferredTypes) =>
        $$"""
        ## 会議情報
        - 件名: {{subject}}
        - 参加者: {{participants}}
        - 言語: {{language}}

        ## トランスクリプト
        {{transcript}}

        ## 既出の質問
        {{alreadyAsked}}

        ## 希望する質問タイプ
        {{preferredTypes}}

        最大{{maxQuestions}}個の深掘り質問を{{language}}で生成し、以下のJSON配列で出力してください:
        [
          {
            "question": "質問テキスト",
            "type": "WhyQuestion|ImpactQuestion|ClarificationQuestion|AlternativeQuestion|TimelineQuestion|StakeholderQuestion|RiskQuestion|ProcessQuestion|PrecedentQuestion|AssumptionQuestion",
            "priority": "Critical|High|Medium|Low",
            "rationale": "この質問が重要な理由",
            "targetSpeaker": "回答を期待する話者名",
            "relatedTopicId": "",
            "expectedKnowledgeCategory": "DecisionBackground|UndocumentedProcess|ExpertKnowledge|DiscussionHistory|OrganizationalContext|TechnicalInsight|LessonsLearned|StakeholderRelationship|ImplicitAssumption|DomainExpertise"
          }
        ]
        """;

    internal record RawQuestion
    {
        public string? Question { get; init; }
        public string? Type { get; init; }
        public string? Priority { get; init; }
        public string? Rationale { get; init; }
        public string? TargetSpeaker { get; init; }
        public string? RelatedTopicId { get; init; }
        public string? ExpectedKnowledgeCategory { get; init; }
    }

    internal record RawQuestionWrapper
    {
        public List<RawQuestion>? Questions { get; init; }
    }
}
