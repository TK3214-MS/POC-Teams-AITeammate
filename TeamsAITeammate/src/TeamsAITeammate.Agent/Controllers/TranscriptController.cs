using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TeamsAITeammate.Core.Interfaces;
using TeamsAITeammate.Core.Models;

namespace TeamsAITeammate.Agent.Controllers;

[ApiController]
[Route("api/transcript")]
[Authorize(AuthenticationSchemes = "TeamsTab")]
public class TranscriptController : ControllerBase
{
    private readonly IMeetingSessionRepository _sessions;
    private readonly ITranscriptIngestionService _ingestion;

    public TranscriptController(
        IMeetingSessionRepository sessions,
        ITranscriptIngestionService ingestion)
    {
        _sessions = sessions;
        _ingestion = ingestion;
    }

    [HttpPost("segments")]
    public async Task<IActionResult> AppendSegment(
        [FromBody] ClientTranscriptSegment request,
        CancellationToken ct)
    {
        var tenantId = User.FindFirstValue("tid");
        if (string.IsNullOrWhiteSpace(tenantId))
            return Unauthorized();

        var session = await _sessions.GetByMeetingIdAsync(request.MeetingId, ct);
        if (session is null)
            return NotFound("No meeting session is active for this meeting.");
        if (!string.Equals(session.TenantId, tenantId, StringComparison.OrdinalIgnoreCase))
            return Forbid();
        if (session.State is not (SessionState.Active or SessionState.Analyzing))
            return Conflict("The meeting session is not accepting transcript segments.");

        var speakerId = User.FindFirstValue("oid")
            ?? User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? "unknown";
        var speakerName = User.FindFirstValue("name")
            ?? User.Identity?.Name
            ?? "Participant";
        var segment = new TranscriptSegment
        {
            Id = request.Id,
            MeetingId = session.MeetingId,
            SpeakerId = speakerId,
            SpeakerName = speakerName,
            Text = request.Text.Trim(),
            Language = request.Language,
            Timestamp = request.Timestamp,
            Duration = TimeSpan.FromMilliseconds(Math.Max(0, request.DurationMs)),
            Confidence = Math.Clamp(request.Confidence, 0, 1),
        };

        await _ingestion.AppendAsync(session, segment, ct);
        return Accepted();
    }
}

public record ClientTranscriptSegment
{
    [Required]
    public string Id { get; init; } = string.Empty;

    [Required]
    public string MeetingId { get; init; } = string.Empty;

    [Required, StringLength(4000, MinimumLength = 1)]
    public string Text { get; init; } = string.Empty;

    [Required, StringLength(20)]
    public string Language { get; init; } = "ja-JP";

    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
    public double DurationMs { get; init; }
    public float Confidence { get; init; }
}