using Microsoft.Extensions.Logging;
using TeamsAITeammate.Core.Interfaces;
using TeamsAITeammate.Core.Models;

namespace TeamsAITeammate.Infrastructure.Services;

public class CardActionHandler : ICardActionHandler
{
    private readonly IKnowledgeRepository _knowledge;
    private readonly ILogger<CardActionHandler> _logger;

    public CardActionHandler(
        IKnowledgeRepository knowledge,
        ILogger<CardActionHandler> logger)
    {
        _knowledge = knowledge;
        _logger = logger;
    }

    public async Task<CardActionResult> HandleActionAsync(
        string actionVerb, IDictionary<string, object> data, string sessionId, CancellationToken ct)
    {
        _logger.LogInformation("Handling card action: {Verb} for session {SessionId}", actionVerb, sessionId);

        return actionVerb switch
        {
            "questionAnswer" => await HandleQuestionAnswerAsync(data, sessionId, ct),
            "questionSkip" => HandleQuestionSkip(data),
            "questionDefer" => HandleQuestionDefer(data),
            "agendaAccept" => HandleAgendaAccept(data),
            "agendaSkipAll" => new CardActionResult { Success = true, Message = "All agenda items skipped." },
            "knowledgeConfirm" => await HandleKnowledgeConfirmAsync(data, sessionId, ct),
            "knowledgeEdit" => await HandleKnowledgeEditAsync(data, sessionId, ct),
            "knowledgeReject" => HandleKnowledgeReject(data),
            "settingsUpdate" => HandleSettingsUpdate(data),
            "settingsCancel" => new CardActionResult { Success = true, Message = "Settings change cancelled." },
            _ => new CardActionResult { Success = false, Message = $"Unknown action: {actionVerb}" }
        };
    }

    private async Task<CardActionResult> HandleQuestionAnswerAsync(
        IDictionary<string, object> data, string sessionId, CancellationToken ct)
    {
        var questionId = GetString(data, "questionId");
        var answerText = GetString(data, "answerText");

        if (string.IsNullOrWhiteSpace(answerText))
        {
            return new CardActionResult { Success = false, Message = "Answer text is required." };
        }

        var entry = new KnowledgeEntry
        {
            SessionId = sessionId,
            Title = $"Answer to question {questionId}",
            Content = answerText,
            Type = KnowledgeType.TacitKnowledge,
            SourceContext = $"Question: {questionId}",
            ConfidenceScore = 1.0
        };

        await _knowledge.UpsertAsync(entry, ct);

        _logger.LogInformation("Saved answer for question {QuestionId} as knowledge entry", questionId);
        return new CardActionResult { Success = true, Message = "Answer saved as knowledge." };
    }

    private static CardActionResult HandleQuestionSkip(IDictionary<string, object> data)
    {
        var questionId = GetString(data, "questionId");
        return new CardActionResult { Success = true, Message = $"Question {questionId} skipped." };
    }

    private static CardActionResult HandleQuestionDefer(IDictionary<string, object> data)
    {
        var questionId = GetString(data, "questionId");
        return new CardActionResult { Success = true, Message = $"Question {questionId} deferred." };
    }

    private static CardActionResult HandleAgendaAccept(IDictionary<string, object> data)
    {
        var agendaId = GetString(data, "agendaId");
        return new CardActionResult { Success = true, Message = $"Agenda item {agendaId} accepted for discussion." };
    }

    private async Task<CardActionResult> HandleKnowledgeConfirmAsync(
        IDictionary<string, object> data, string sessionId, CancellationToken ct)
    {
        var candidateId = GetString(data, "candidateId");

        var entry = new KnowledgeEntry
        {
            Id = candidateId,
            SessionId = sessionId,
            Title = "Confirmed tacit knowledge",
            Content = GetString(data, "content"),
            Type = KnowledgeType.TacitKnowledge,
            ConfidenceScore = 1.0
        };

        await _knowledge.UpsertAsync(entry, ct);

        _logger.LogInformation("Knowledge candidate {CandidateId} confirmed and saved", candidateId);
        return new CardActionResult { Success = true, Message = "Knowledge confirmed and saved." };
    }

    private async Task<CardActionResult> HandleKnowledgeEditAsync(
        IDictionary<string, object> data, string sessionId, CancellationToken ct)
    {
        var candidateId = GetString(data, "candidateId");
        var correctionText = GetString(data, "correctionText");

        if (string.IsNullOrWhiteSpace(correctionText))
        {
            return new CardActionResult { Success = false, Message = "Correction text is required." };
        }

        var entry = new KnowledgeEntry
        {
            Id = candidateId,
            SessionId = sessionId,
            Title = "Corrected tacit knowledge",
            Content = correctionText,
            Type = KnowledgeType.TacitKnowledge,
            ConfidenceScore = 1.0,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        await _knowledge.UpsertAsync(entry, ct);

        _logger.LogInformation("Knowledge candidate {CandidateId} edited and saved", candidateId);
        return new CardActionResult { Success = true, Message = "Knowledge updated and saved." };
    }

    private static CardActionResult HandleKnowledgeReject(IDictionary<string, object> data)
    {
        var candidateId = GetString(data, "candidateId");
        return new CardActionResult { Success = true, Message = $"Knowledge candidate {candidateId} rejected." };
    }

    private static CardActionResult HandleSettingsUpdate(IDictionary<string, object> data)
    {
        // Settings would be persisted via session state — for now just acknowledge
        return new CardActionResult { Success = true, Message = "Settings updated." };
    }

    private static string GetString(IDictionary<string, object> data, string key)
    {
        return data.TryGetValue(key, out var value) ? value?.ToString() ?? string.Empty : string.Empty;
    }
}
