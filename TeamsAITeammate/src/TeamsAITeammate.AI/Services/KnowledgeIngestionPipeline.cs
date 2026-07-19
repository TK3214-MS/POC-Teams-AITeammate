using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using TeamsAITeammate.Core.Interfaces;
using TeamsAITeammate.Core.Models;

namespace TeamsAITeammate.AI.Services;

public class KnowledgeIngestionPipeline : IKnowledgeIngestionPipeline
{
    private readonly IKnowledgeStoreFactory _storeFactory;
    private readonly IEmbeddingService _embeddingService;
    private readonly IChatClient _chatClient;
    private readonly ILogger<KnowledgeIngestionPipeline> _logger;

    public KnowledgeIngestionPipeline(
        IKnowledgeStoreFactory storeFactory,
        IEmbeddingService embeddingService,
        IChatClient chatClient,
        ILogger<KnowledgeIngestionPipeline> logger)
    {
        _storeFactory = storeFactory;
        _embeddingService = embeddingService;
        _chatClient = chatClient;
        _logger = logger;
    }

    public async Task<KnowledgeEntry> IngestAsync(
        TacitKnowledgeCandidate candidate,
        IngestionContext context,
        CancellationToken ct)
    {
        _logger.LogInformation(
            "Ingesting tacit knowledge candidate {Id} for session {SessionId}",
            candidate.Id, context.SessionId);

        // 1. Check for duplicates
        var store = _storeFactory.CreateStore(context.DataStoreProvider);
        var duplicates = await store.SearchAsync(
            candidate.Content,
            new KnowledgeSearchOptions
            {
                TenantId = context.TenantId,
                MaxResults = 3
            },
            ct);

        if (duplicates.Any(d => IsDuplicate(d, candidate)))
        {
            _logger.LogInformation("Duplicate detected for candidate {Id}, skipping", candidate.Id);
            var existing = duplicates.First(d => IsDuplicate(d, candidate));
            return existing;
        }

        // 2. Generate title and summary via LLM
        var (title, summary) = await GenerateTitleAndSummaryAsync(candidate, ct);

        // 3. Generate tags via LLM
        var tags = await GenerateTagsAsync(candidate, context, ct);

        // 4. Generate embedding
        var textToEmbed = $"{title} {candidate.Content} {summary}";
        var embedding = await _embeddingService.GenerateEmbeddingAsync(textToEmbed, ct);

        // 5. Create KnowledgeEntry
        var entry = new KnowledgeEntry
        {
            TenantId = context.TenantId,
            MeetingId = context.MeetingId,
            SessionId = context.SessionId,
            Title = title,
            Content = candidate.Content,
            Summary = summary,
            Type = KnowledgeType.TacitKnowledge,
            Category = candidate.Category,
            SourceSpeaker = candidate.SourceSpeaker,
            SourceContext = candidate.Context,
            MeetingSubject = context.MeetingSubject,
            MeetingDate = context.MeetingDate,
            Participants = context.Participants.ToList(),
            Tags = tags.ToList(),
            RelatedTopics = candidate.RelatedTopics,
            Language = context.Language,
            ConfidenceScore = candidate.Confidence,
            Status = KnowledgeStatus.Draft,
            Embedding = embedding
        };

        // 6. Save to selected data store
        var id = await store.SaveKnowledgeAsync(entry, ct);
        entry = entry with { Id = id };

        _logger.LogInformation(
            "Ingested knowledge entry {Id} with title '{Title}'", id, title);

        return entry;
    }

    public async Task<KnowledgeEntry> UpdateStatusAsync(
        string id,
        KnowledgeStatus status,
        string? validatedBy,
        string? correctedContent,
        CancellationToken ct)
    {
        // Try all available stores to find the entry
        foreach (var providerName in _storeFactory.GetAvailableProviders())
        {
            var store = _storeFactory.CreateStore(providerName);
            var entry = await store.GetKnowledgeAsync(id, ct);
            if (entry is null) continue;

            var updated = entry with
            {
                Status = status,
                ValidatedBy = validatedBy,
                ValidatedAt = DateTimeOffset.UtcNow,
                Content = correctedContent ?? entry.Content,
                UpdatedAt = DateTimeOffset.UtcNow
            };

            // Re-generate embedding if content was corrected
            if (correctedContent is not null)
            {
                var textToEmbed = $"{updated.Title} {updated.Content} {updated.Summary}";
                var embedding = await _embeddingService.GenerateEmbeddingAsync(textToEmbed, ct);
                updated = updated with { Embedding = embedding };
            }

            await store.UpdateKnowledgeAsync(id, updated, ct);

            _logger.LogInformation(
                "Updated knowledge entry {Id} status to {Status}", id, status);
            return updated;
        }

        throw new InvalidOperationException($"Knowledge entry '{id}' not found in any store");
    }

    private async Task<(string Title, string Summary)> GenerateTitleAndSummaryAsync(
        TacitKnowledgeCandidate candidate, CancellationToken ct)
    {
        var prompt = $$"""
            以下の暗黙知候補からタイトルとサマリーを生成してください。

            カテゴリ: {{candidate.Category}}
            内容: {{candidate.Content}}
            コンテキスト: {{candidate.Context}}

            以下のJSON形式で出力してください:
            {"title": "簡潔なタイトル", "summary": "2-3文の要約"}
            """;

        var response = await _chatClient.GetResponseAsync(prompt, cancellationToken: ct);
        var text = response.Text ?? string.Empty;

        try
        {
            var json = ExtractJson(text);
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            var root = doc.RootElement;
            var title = root.GetProperty("title").GetString() ?? candidate.Content[..Math.Min(50, candidate.Content.Length)];
            var summary = root.GetProperty("summary").GetString() ?? candidate.Content;
            return (title, summary);
        }
        catch
        {
            _logger.LogWarning("Failed to parse title/summary from LLM response, using defaults");
            return (
                candidate.Content[..Math.Min(50, candidate.Content.Length)],
                candidate.Content
            );
        }
    }

    private async Task<IReadOnlyList<string>> GenerateTagsAsync(
        TacitKnowledgeCandidate candidate, IngestionContext context, CancellationToken ct)
    {
        var prompt = $$"""
            以下の暗黙知から関連タグを3-5個生成してください。

            カテゴリ: {{candidate.Category}}
            内容: {{candidate.Content}}
            会議テーマ: {{context.MeetingSubject}}

            JSON配列形式で出力してください: ["タグ1", "タグ2", ...]
            """;

        var response = await _chatClient.GetResponseAsync(prompt, cancellationToken: ct);
        var text = response.Text ?? string.Empty;

        try
        {
            var json = ExtractJson(text);
            return System.Text.Json.JsonSerializer.Deserialize<string[]>(json) ?? [];
        }
        catch
        {
            _logger.LogWarning("Failed to parse tags from LLM response, using category as tag");
            return [candidate.Category.ToString()];
        }
    }

    private static bool IsDuplicate(KnowledgeEntry existing, TacitKnowledgeCandidate candidate)
    {
        // Simple content similarity check
        var similarity = CalculateContentSimilarity(existing.Content, candidate.Content);
        return similarity > 0.85;
    }

    private static double CalculateContentSimilarity(string a, string b)
    {
        if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b))
            return 0;

        if (a == b)
            return 1.0;

        // Simple Jaccard similarity on words
        var wordsA = a.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var wordsB = b.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var intersection = wordsA.Intersect(wordsB, StringComparer.OrdinalIgnoreCase).Count();
        var union = wordsA.Union(wordsB, StringComparer.OrdinalIgnoreCase).Count();

        return union == 0 ? 0 : (double)intersection / union;
    }

    internal static string ExtractJson(string text)
    {
        // Try to extract JSON from markdown code blocks
        var jsonStart = text.IndexOf("```json", StringComparison.OrdinalIgnoreCase);
        if (jsonStart >= 0)
        {
            jsonStart = text.IndexOf('\n', jsonStart) + 1;
            var jsonEnd = text.IndexOf("```", jsonStart, StringComparison.Ordinal);
            if (jsonEnd > jsonStart)
                return text[jsonStart..jsonEnd].Trim();
        }

        // Try to find raw JSON
        var firstBrace = text.IndexOf('{');
        var firstBracket = text.IndexOf('[');

        if (firstBracket >= 0 && (firstBrace < 0 || firstBracket < firstBrace))
        {
            var lastBracket = text.LastIndexOf(']');
            if (lastBracket > firstBracket)
                return text[firstBracket..(lastBracket + 1)];
        }

        if (firstBrace >= 0)
        {
            var lastBrace = text.LastIndexOf('}');
            if (lastBrace > firstBrace)
                return text[firstBrace..(lastBrace + 1)];
        }

        return text;
    }
}
