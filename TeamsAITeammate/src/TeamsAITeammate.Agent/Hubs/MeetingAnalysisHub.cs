using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using TeamsAITeammate.Core.Interfaces;

namespace TeamsAITeammate.Agent.Hubs;

[Authorize(AuthenticationSchemes = "TeamsTab")]
public class MeetingAnalysisHub : Hub
{
    private readonly ILogger<MeetingAnalysisHub> _logger;
    private readonly IMeetingSessionRepository _sessions;

    public MeetingAnalysisHub(
        ILogger<MeetingAnalysisHub> logger,
        IMeetingSessionRepository sessions)
    {
        _logger = logger;
        _sessions = sessions;
    }

    public async Task JoinMeeting(string meetingId)
    {
        var tenantId = Context.User?.FindFirstValue("tid");
        var session = await _sessions.GetByMeetingIdAsync(meetingId, Context.ConnectionAborted);
        if (session is null ||
            string.IsNullOrWhiteSpace(tenantId) ||
            !string.Equals(session.TenantId, tenantId, StringComparison.OrdinalIgnoreCase))
        {
            throw new HubException("The meeting is not available for this tenant.");
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, meetingId);
        _logger.LogInformation("Client {ConnectionId} joined meeting group {MeetingId}",
            Context.ConnectionId, meetingId);
    }

    public async Task LeaveMeeting(string meetingId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, meetingId);
        _logger.LogInformation("Client {ConnectionId} left meeting group {MeetingId}",
            Context.ConnectionId, meetingId);
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("Client {ConnectionId} disconnected", Context.ConnectionId);
        return base.OnDisconnectedAsync(exception);
    }
}
