using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace TeamsAITeammate.Agent.Hubs;

public class MeetingAnalysisHub : Hub
{
    private readonly ILogger<MeetingAnalysisHub> _logger;

    public MeetingAnalysisHub(ILogger<MeetingAnalysisHub> logger)
    {
        _logger = logger;
    }

    public async Task JoinMeeting(string meetingId)
    {
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
