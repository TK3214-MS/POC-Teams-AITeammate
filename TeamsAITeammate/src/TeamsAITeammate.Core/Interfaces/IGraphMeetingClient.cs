using TeamsAITeammate.Core.Models;

namespace TeamsAITeammate.Core.Interfaces;

public interface IGraphMeetingClient
{
    Task<MeetingInfo> GetMeetingAsync(string meetingId, CancellationToken ct = default);
    Task<IReadOnlyList<MeetingParticipantInfo>> GetParticipantsAsync(string meetingId, CancellationToken ct = default);
    Task SendChatMessageAsync(string chatId, string message, CancellationToken ct = default);
    Task SendAdaptiveCardAsync(string chatId, string cardJson, CancellationToken ct = default);
    Task<string> GetMeetingChatIdAsync(string meetingId, CancellationToken ct = default);
}
