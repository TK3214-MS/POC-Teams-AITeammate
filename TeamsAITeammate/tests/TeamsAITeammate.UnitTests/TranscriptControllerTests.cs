using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using TeamsAITeammate.Agent.Controllers;
using TeamsAITeammate.Core.Interfaces;
using TeamsAITeammate.Core.Models;

namespace TeamsAITeammate.UnitTests;

public class TranscriptControllerTests
{
    private readonly Mock<IMeetingSessionRepository> _sessions = new();
    private readonly Mock<ITranscriptIngestionService> _ingestion = new();

    [Fact]
    public async Task AppendSegment_ValidTenant_IngestsAuthenticatedSpeaker()
    {
        var session = new MeetingSession
        {
            Id = "session-1",
            MeetingId = "meeting-1",
            TenantId = "tenant-1",
            State = SessionState.Active,
        };
        _sessions.Setup(s => s.GetByMeetingIdAsync("meeting-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);
        var controller = CreateController("tenant-1", "user-1", "User One");

        var result = await controller.AppendSegment(new ClientTranscriptSegment
        {
            Id = "segment-1",
            MeetingId = "meeting-1",
            Text = "  確認します  ",
            Language = "ja-JP",
            Confidence = 2,
        }, CancellationToken.None);

        Assert.IsType<AcceptedResult>(result);
        _ingestion.Verify(i => i.AppendAsync(
            session,
            It.Is<TranscriptSegment>(s =>
                s.SpeakerId == "user-1" &&
                s.SpeakerName == "User One" &&
                s.Text == "確認します" &&
                s.Confidence == 1),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AppendSegment_DifferentTenant_ReturnsForbid()
    {
        _sessions.Setup(s => s.GetByMeetingIdAsync("meeting-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MeetingSession
            {
                MeetingId = "meeting-1",
                TenantId = "other-tenant",
                State = SessionState.Active,
            });
        var controller = CreateController("tenant-1", "user-1", "User One");

        var result = await controller.AppendSegment(new ClientTranscriptSegment
        {
            Id = "segment-1",
            MeetingId = "meeting-1",
            Text = "test",
        }, CancellationToken.None);

        Assert.IsType<ForbidResult>(result);
        _ingestion.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task AppendSegment_InactiveSession_ReturnsConflict()
    {
        _sessions.Setup(s => s.GetByMeetingIdAsync("meeting-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MeetingSession
            {
                MeetingId = "meeting-1",
                TenantId = "tenant-1",
                State = SessionState.Paused,
            });
        var controller = CreateController("tenant-1", "user-1", "User One");

        var result = await controller.AppendSegment(new ClientTranscriptSegment
        {
            Id = "segment-1",
            MeetingId = "meeting-1",
            Text = "test",
        }, CancellationToken.None);

        Assert.IsType<ConflictObjectResult>(result);
        _ingestion.VerifyNoOtherCalls();
    }

    private TranscriptController CreateController(string tenantId, string userId, string name)
    {
        var controller = new TranscriptController(_sessions.Object, _ingestion.Object);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity([
                    new Claim("tid", tenantId),
                    new Claim("oid", userId),
                    new Claim("name", name),
                ], "test")),
            },
        };
        return controller;
    }
}