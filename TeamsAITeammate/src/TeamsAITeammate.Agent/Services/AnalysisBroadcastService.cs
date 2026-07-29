using Microsoft.AspNetCore.SignalR;
using TeamsAITeammate.Agent.Hubs;
using TeamsAITeammate.Core.Interfaces;
using TeamsAITeammate.Core.Models;

namespace TeamsAITeammate.Agent.Services;

public class AnalysisBroadcastService : IHostedService
{
    private readonly IAnalysisScheduler _scheduler;
    private readonly IMeetingSessionRepository _sessions;
    private readonly IHubContext<MeetingAnalysisHub> _hub;

    public AnalysisBroadcastService(
        IAnalysisScheduler scheduler,
        IMeetingSessionRepository sessions,
        IHubContext<MeetingAnalysisHub> hub)
    {
        _scheduler = scheduler;
        _sessions = sessions;
        _hub = hub;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _scheduler.OnAnalysisCompleted += BroadcastAsync;
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _scheduler.OnAnalysisCompleted -= BroadcastAsync;
        return Task.CompletedTask;
    }

    private async Task BroadcastAsync(string sessionId, ConversationAnalysis analysis)
    {
        var session = await _sessions.GetByIdAsync(sessionId);
        if (session is null)
            return;

        var clients = _hub.Clients.Group(session.MeetingId);
        await clients.SendAsync("analysisUpdated", analysis);
        foreach (var topic in analysis.Topics)
            await clients.SendAsync("topicDetected", topic);
        foreach (var knowledge in analysis.TacitKnowledgeCandidates)
            await clients.SendAsync("knowledgeExtracted", knowledge);
        foreach (var question in analysis.Questions)
            await clients.SendAsync("questionGenerated", question);
    }
}