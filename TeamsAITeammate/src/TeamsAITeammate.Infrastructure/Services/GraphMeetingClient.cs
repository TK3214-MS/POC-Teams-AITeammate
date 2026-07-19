using Microsoft.Extensions.Logging;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using TeamsAITeammate.Core.Interfaces;
using CoreModels = TeamsAITeammate.Core.Models;

namespace TeamsAITeammate.Infrastructure.Services;

public class GraphMeetingClient : IGraphMeetingClient
{
    private readonly GraphServiceClient _graphClient;
    private readonly ILogger<GraphMeetingClient> _logger;

    public GraphMeetingClient(GraphClientService graphClientService, ILogger<GraphMeetingClient> logger)
    {
        _graphClient = graphClientService.Client;
        _logger = logger;
    }

    public async Task<CoreModels.MeetingInfo> GetMeetingAsync(string meetingId, CancellationToken ct = default)
    {
        _logger.LogInformation("Getting meeting info for {MeetingId}", meetingId);

        var meeting = await _graphClient.Communications.OnlineMeetings[meetingId]
            .GetAsync(cancellationToken: ct);

        return new CoreModels.MeetingInfo
        {
            Id = meeting?.Id ?? meetingId,
            Subject = meeting?.Subject ?? string.Empty,
            JoinUrl = meeting?.JoinWebUrl ?? string.Empty,
            StartDateTime = meeting?.StartDateTime,
            EndDateTime = meeting?.EndDateTime,
            ChatId = meeting?.ChatInfo?.ThreadId ?? string.Empty,
            Organizer = meeting?.Participants?.Organizer is { } organizer
                ? new CoreModels.MeetingParticipantInfo
                {
                    UserId = organizer.Identity?.User?.Id ?? string.Empty,
                    DisplayName = organizer.Identity?.User?.DisplayName ?? string.Empty,
                }
                : null,
        };
    }

    public async Task<IReadOnlyList<CoreModels.MeetingParticipantInfo>> GetParticipantsAsync(string meetingId, CancellationToken ct = default)
    {
        _logger.LogInformation("Getting participants for meeting {MeetingId}", meetingId);

        var meeting = await _graphClient.Communications.OnlineMeetings[meetingId]
            .GetAsync(cancellationToken: ct);

        var participants = new List<CoreModels.MeetingParticipantInfo>();

        if (meeting?.Participants?.Attendees is { } attendees)
        {
            foreach (var attendee in attendees)
            {
                participants.Add(new CoreModels.MeetingParticipantInfo
                {
                    UserId = attendee.Identity?.User?.Id ?? string.Empty,
                    DisplayName = attendee.Identity?.User?.DisplayName ?? string.Empty,
                    Role = CoreModels.ParticipantRole.Attendee,
                });
            }
        }

        return participants;
    }

    public async Task SendChatMessageAsync(string chatId, string message, CancellationToken ct = default)
    {
        _logger.LogInformation("Sending message to chat {ChatId}", chatId);

        var chatMessage = new ChatMessage
        {
            Body = new ItemBody
            {
                Content = message,
                ContentType = BodyType.Text,
            },
        };

        await _graphClient.Chats[chatId].Messages
            .PostAsync(chatMessage, cancellationToken: ct);
    }

    public async Task SendAdaptiveCardAsync(string chatId, string cardJson, CancellationToken ct = default)
    {
        _logger.LogInformation("Sending Adaptive Card to chat {ChatId}", chatId);

        var chatMessage = new ChatMessage
        {
            Body = new ItemBody
            {
                ContentType = BodyType.Html,
                Content = "<attachment id=\"adaptiveCard\"></attachment>",
            },
            Attachments =
            [
                new ChatMessageAttachment
                {
                    Id = "adaptiveCard",
                    ContentType = "application/vnd.microsoft.card.adaptive",
                    Content = cardJson,
                },
            ],
        };

        await _graphClient.Chats[chatId].Messages
            .PostAsync(chatMessage, cancellationToken: ct);
    }

    public async Task<string> GetMeetingChatIdAsync(string meetingId, CancellationToken ct = default)
    {
        var meeting = await _graphClient.Communications.OnlineMeetings[meetingId]
            .GetAsync(cancellationToken: ct);

        return meeting?.ChatInfo?.ThreadId ?? string.Empty;
    }
}
