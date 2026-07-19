using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using TeamsAITeammate.Core.Interfaces;
using TeamsAITeammate.Core.Models;

namespace TeamsAITeammate.Infrastructure.Services;

public class NotificationThrottler : INotificationThrottler
{
    private readonly ILogger<NotificationThrottler> _logger;
    private readonly ConcurrentDictionary<string, SessionThrottleState> _states = new();

    private static readonly TimeSpan MinInterventionInterval = TimeSpan.FromSeconds(60);
    private const int DefaultMaxInterventions = 20;
    private const int MaxConsecutiveCards = 3;

    public NotificationThrottler(ILogger<NotificationThrottler> logger)
    {
        _logger = logger;
    }

    public Task<bool> CanSendAsync(string sessionId, InterventionType type, CancellationToken ct)
    {
        var state = _states.GetOrAdd(sessionId, _ => new SessionThrottleState());

        // Check max interventions per meeting
        if (state.TotalCount >= DefaultMaxInterventions)
        {
            _logger.LogInformation("Throttled: max interventions ({Max}) reached for session {SessionId}",
                DefaultMaxInterventions, sessionId);
            return Task.FromResult(false);
        }

        // Check minimum interval since last intervention
        var elapsed = DateTimeOffset.UtcNow - state.LastSentAt;
        if (state.LastSentAt != default && elapsed < MinInterventionInterval)
        {
            _logger.LogDebug("Throttled: too soon ({Elapsed}s) since last intervention for session {SessionId}",
                elapsed.TotalSeconds, sessionId);
            return Task.FromResult(false);
        }

        // Check consecutive adaptive card limit (suppress cards when previous ones are unanswered)
        if (type == InterventionType.AdaptiveCard && state.ConsecutiveCardCount >= MaxConsecutiveCards)
        {
            _logger.LogInformation("Throttled: {Max} consecutive cards sent without response for session {SessionId}",
                MaxConsecutiveCards, sessionId);
            return Task.FromResult(false);
        }

        return Task.FromResult(true);
    }

    public Task RecordSentAsync(string sessionId, InterventionType type, CancellationToken ct)
    {
        var state = _states.GetOrAdd(sessionId, _ => new SessionThrottleState());

        state.LastSentAt = DateTimeOffset.UtcNow;
        state.TotalCount++;

        if (type == InterventionType.AdaptiveCard)
        {
            state.ConsecutiveCardCount++;
        }
        else
        {
            state.ConsecutiveCardCount = 0;
        }

        _logger.LogDebug("Recorded intervention #{Count} (type: {Type}) for session {SessionId}",
            state.TotalCount, type, sessionId);
        return Task.CompletedTask;
    }

    public Task ResetAsync(string sessionId, CancellationToken ct)
    {
        _states.TryRemove(sessionId, out _);
        return Task.CompletedTask;
    }

    // Called when a user responds to a card — resets the consecutive card counter
    internal void ResetConsecutiveCards(string sessionId)
    {
        if (_states.TryGetValue(sessionId, out var state))
        {
            state.ConsecutiveCardCount = 0;
        }
    }

    private sealed class SessionThrottleState
    {
        public DateTimeOffset LastSentAt { get; set; }
        public int TotalCount { get; set; }
        public int ConsecutiveCardCount { get; set; }
    }
}
