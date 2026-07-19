using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using TeamsAITeammate.Core.Interfaces;
using TeamsAITeammate.Core.Models;

namespace TeamsAITeammate.Infrastructure.Services;

public class InterventionOrchestrator : IInterventionOrchestrator
{
    private readonly INotificationThrottler _throttler;
    private readonly IMessageFormatter _formatter;
    private readonly IGraphMeetingClient _graphClient;
    private readonly IMeetingSessionManager _sessionManager;
    private readonly ILogger<InterventionOrchestrator> _logger;
    private readonly ConcurrentDictionary<string, bool> _pausedSessions = new();

    public InterventionOrchestrator(
        INotificationThrottler throttler,
        IMessageFormatter formatter,
        IGraphMeetingClient graphClient,
        IMeetingSessionManager sessionManager,
        ILogger<InterventionOrchestrator> logger)
    {
        _throttler = throttler;
        _formatter = formatter;
        _graphClient = graphClient;
        _sessionManager = sessionManager;
        _logger = logger;
    }

    public async Task<InterventionAction?> EvaluateAsync(
        string sessionId,
        InterventionTrigger trigger,
        ConversationAnalysis? analysis,
        CancellationToken ct)
    {
        if (IsPaused(sessionId))
        {
            _logger.LogDebug("Session {SessionId} is paused, skipping intervention", sessionId);
            return null;
        }

        return trigger switch
        {
            InterventionTrigger.UserMention => EvaluateUserMention(sessionId, analysis),
            InterventionTrigger.SilenceDetected => EvaluateSilenceDetected(sessionId, analysis),
            InterventionTrigger.TopicChange => EvaluateTopicChange(sessionId, analysis),
            InterventionTrigger.PeriodicAnalysis => EvaluatePeriodicAnalysis(sessionId, analysis),
            InterventionTrigger.CriticalInsight => EvaluateCriticalInsight(sessionId, analysis),
            _ => null
        };
    }

    public async Task ExecuteAsync(InterventionAction action, CancellationToken ct)
    {
        if (!await _throttler.CanSendAsync(action.SessionId, action.Type, ct))
        {
            _logger.LogInformation("Intervention throttled for session {SessionId}, type {Type}",
                action.SessionId, action.Type);
            return;
        }

        var session = await _sessionManager.GetActiveSessionAsync(action.SessionId, ct);
        if (session is null)
        {
            _logger.LogWarning("No active session found for {SessionId}", action.SessionId);
            return;
        }

        var chatId = session.Context?.ChatId;
        if (string.IsNullOrEmpty(chatId))
        {
            _logger.LogWarning("No chat ID available for session {SessionId}", action.SessionId);
            return;
        }

        switch (action.Type)
        {
            case InterventionType.ChatMessage:
                await _graphClient.SendChatMessageAsync(chatId, action.Content?.ToString() ?? string.Empty, ct);
                break;

            case InterventionType.AdaptiveCard:
                await _graphClient.SendAdaptiveCardAsync(chatId, action.Content?.ToString() ?? string.Empty, ct);
                break;

            case InterventionType.SidePanelUpdate:
                // SignalR hub push handled separately via Hub
                _logger.LogDebug("SidePanelUpdate action — handled via SignalR");
                break;

            case InterventionType.ProactiveNotification:
                await _graphClient.SendChatMessageAsync(chatId, action.Content?.ToString() ?? string.Empty, ct);
                break;
        }

        await _throttler.RecordSentAsync(action.SessionId, action.Type, ct);
        _logger.LogInformation("Executed intervention {Type} for session {SessionId}", action.Type, action.SessionId);
    }

    public Task PauseAsync(string sessionId, CancellationToken ct)
    {
        _pausedSessions[sessionId] = true;
        _logger.LogInformation("Interventions paused for session {SessionId}", sessionId);
        return Task.CompletedTask;
    }

    public Task ResumeAsync(string sessionId, CancellationToken ct)
    {
        _pausedSessions.TryRemove(sessionId, out _);
        _logger.LogInformation("Interventions resumed for session {SessionId}", sessionId);
        return Task.CompletedTask;
    }

    public bool IsPaused(string sessionId)
    {
        return _pausedSessions.TryGetValue(sessionId, out var paused) && paused;
    }

    private static InterventionAction? EvaluateUserMention(string sessionId, ConversationAnalysis? analysis)
    {
        // @mention always triggers immediate response
        if (analysis is null) return null;

        var content = analysis.Questions.Count > 0
            ? analysis.Questions[0]
            : null;

        if (content is null) return null;

        return new InterventionAction
        {
            Type = InterventionType.AdaptiveCard,
            Trigger = InterventionTrigger.UserMention,
            Content = content,
            Priority = InterventionPriority.Critical,
            SessionId = sessionId
        };
    }

    private InterventionAction? EvaluateSilenceDetected(string sessionId, ConversationAnalysis? analysis)
    {
        if (analysis is null) return null;

        // Pick highest-priority question or suggest agenda
        var highPriorityQuestion = analysis.Questions
            .Where(q => q.Priority is QuestionPriority.Critical or QuestionPriority.High)
            .FirstOrDefault();

        if (highPriorityQuestion is not null)
        {
            var language = "ja"; // default; in production would come from session context
            var cardJson = AdaptiveCardTemplates.BuildQuestionCard(highPriorityQuestion, language);
            return new InterventionAction
            {
                Type = InterventionType.AdaptiveCard,
                Trigger = InterventionTrigger.SilenceDetected,
                Content = cardJson,
                Priority = InterventionPriority.High,
                SessionId = sessionId
            };
        }

        if (analysis.SuggestedAgenda.Count > 0)
        {
            var cardJson = AdaptiveCardTemplates.BuildAgendaSuggestionCard(analysis.SuggestedAgenda, "ja");
            return new InterventionAction
            {
                Type = InterventionType.AdaptiveCard,
                Trigger = InterventionTrigger.SilenceDetected,
                Content = cardJson,
                Priority = InterventionPriority.Medium,
                SessionId = sessionId
            };
        }

        return null;
    }

    private InterventionAction? EvaluateTopicChange(string sessionId, ConversationAnalysis? analysis)
    {
        if (analysis is null) return null;

        // Summarize previous topic's tacit knowledge + post question
        if (analysis.TacitKnowledgeCandidates.Count > 0)
        {
            var candidate = analysis.TacitKnowledgeCandidates[0];
            var cardJson = AdaptiveCardTemplates.BuildTacitKnowledgeConfirmCard(candidate, "ja");

            return new InterventionAction
            {
                Type = InterventionType.AdaptiveCard,
                Trigger = InterventionTrigger.TopicChange,
                Content = cardJson,
                Priority = InterventionPriority.Medium,
                SessionId = sessionId
            };
        }

        return null;
    }

    private InterventionAction? EvaluatePeriodicAnalysis(string sessionId, ConversationAnalysis? analysis)
    {
        if (analysis is null || analysis.Questions.Count == 0) return null;

        // Post accumulated questions as a summary card
        var summaryCardJson = AdaptiveCardTemplates.BuildConversationSummaryCard(analysis, "ja");

        return new InterventionAction
        {
            Type = InterventionType.AdaptiveCard,
            Trigger = InterventionTrigger.PeriodicAnalysis,
            Content = summaryCardJson,
            Priority = InterventionPriority.Low,
            SessionId = sessionId
        };
    }

    private InterventionAction? EvaluateCriticalInsight(string sessionId, ConversationAnalysis? analysis)
    {
        if (analysis is null) return null;

        var criticalCandidate = analysis.TacitKnowledgeCandidates
            .Where(c => c.Confidence >= 0.9f)
            .FirstOrDefault();

        if (criticalCandidate is null) return null;

        var cardJson = AdaptiveCardTemplates.BuildTacitKnowledgeConfirmCard(criticalCandidate, "ja");
        return new InterventionAction
        {
            Type = InterventionType.AdaptiveCard,
            Trigger = InterventionTrigger.CriticalInsight,
            Content = cardJson,
            Priority = InterventionPriority.High,
            SessionId = sessionId
        };
    }
}
