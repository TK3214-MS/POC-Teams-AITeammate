using System.Text.Json;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using TeamsAITeammate.Core.Interfaces;
using TeamsAITeammate.Core.Models;

namespace TeamsAITeammate.AI.Services;

public class TacitKnowledgeExtractor : ITacitKnowledgeExtractor
{
    private readonly IChatClient _chatClient;
    private readonly ILogger<TacitKnowledgeExtractor> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public TacitKnowledgeExtractor(
        IChatClient chatClient,
        ILogger<TacitKnowledgeExtractor> logger)
    {
        _chatClient = chatClient;
        _logger = logger;
    }

    public async Task<IReadOnlyList<TacitKnowledgeCandidate>> ExtractAsync(
        ConversationWindow conversation,
        AnalysisContext context,
        CancellationToken ct = default)
    {
        if (conversation.Segments.Count == 0)
            return [];

        var transcript = conversation.ToFormattedTranscript();

        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, GetExtractionSystemPrompt()),
            new(ChatRole.User, BuildExtractionUserPrompt(
                transcript, context.MeetingSubject,
                string.Join(", ", context.Participants),
                context.DetectedLanguage))
        };

        try
        {
            var response = await _chatClient.GetResponseAsync(messages, cancellationToken: ct);
            var responseText = response.Text ?? string.Empty;
            var jsonText = ConversationAnalyzer.ExtractJson(responseText);

            var rawCandidates = ParseCandidates(jsonText);

            return rawCandidates
                .Where(r => r.Confidence >= 0.5f)
                .Select(MapToCandidate)
                .ToList();
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse tacit knowledge extraction response");
            return [];
        }
    }

    internal static List<RawTacitKnowledge> ParseCandidates(string jsonText)
    {
        try
        {
            return JsonSerializer.Deserialize<List<RawTacitKnowledge>>(jsonText, JsonOptions) ?? [];
        }
        catch (JsonException)
        {
            try
            {
                var wrapper = JsonSerializer.Deserialize<RawTacitKnowledgeWrapper>(jsonText, JsonOptions);
                return wrapper?.Candidates ?? [];
            }
            catch (JsonException)
            {
                return [];
            }
        }
    }

    private static TacitKnowledgeCandidate MapToCandidate(RawTacitKnowledge raw) => new()
    {
        Category = Enum.TryParse<TacitKnowledgeCategory>(raw.Category, true, out var c)
            ? c : TacitKnowledgeCategory.ExpertKnowledge,
        Content = raw.Content ?? string.Empty,
        Context = raw.Context ?? string.Empty,
        SourceSpeaker = raw.SourceSpeaker ?? string.Empty,
        Confidence = raw.Confidence,
        RelatedTopics = raw.RelatedTopics ?? [],
        RequiresValidation = raw.RequiresValidation
    };

    private static string GetExtractionSystemPrompt() =>
        """
        あなたは会議の会話から暗黙知（文書化されていない知識・ノウハウ）を抽出する専門AIアシスタントです。
        抽出結果はJSON配列形式で出力してください。
        JSON以外のテキストは出力しないでください。
        """;

    private static string BuildExtractionUserPrompt(
        string transcript, string subject, string participants, string language) =>
        $$"""
        ## 会議情報
        - 件名: {{subject}}
        - 参加者: {{participants}}
        - 言語: {{language}}

        ## トランスクリプト
        {{transcript}}

        以下のパターンに注目して暗黙知を抽出してください:
        - DecisionBackground: 意思決定の背景・理由
        - UndocumentedProcess: 未文書化の業務プロセス
        - ExpertKnowledge: 個人の専門知識・ノウハウ
        - DiscussionHistory: 議論の経緯・コンテキスト
        - OrganizationalContext: 組織的な背景情報
        - TechnicalInsight: 技術的な知見
        - LessonsLearned: 教訓・過去の失敗
        - StakeholderRelationship: ステークホルダー関係性
        - ImplicitAssumption: 暗黙の前提条件
        - DomainExpertise: ドメイン固有の専門知識

        JSON配列で出力してください:
        [
          {
            "category": "TacitKnowledgeCategory",
            "content": "抽出された暗黙知の内容",
            "context": "該当箇所の引用",
            "sourceSpeaker": "発言者名",
            "confidence": 0.0-1.0,
            "relatedTopics": ["関連トピック"],
            "requiresValidation": true/false
          }
        ]
        """;

    internal record RawTacitKnowledge
    {
        public string? Category { get; init; }
        public string? Content { get; init; }
        public string? Context { get; init; }
        public string? SourceSpeaker { get; init; }
        public float Confidence { get; init; }
        public List<string>? RelatedTopics { get; init; }
        public bool RequiresValidation { get; init; }
    }

    internal record RawTacitKnowledgeWrapper
    {
        public List<RawTacitKnowledge>? Candidates { get; init; }
    }
}
