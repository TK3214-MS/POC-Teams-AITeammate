using TeamsAITeammate.Core.Interfaces;
using TeamsAITeammate.Core.Models;

namespace TeamsAITeammate.Infrastructure.Services;

public class TranscriptIngestionService : ITranscriptIngestionService
{
    private readonly ITranscriptBuffer _buffer;
    private readonly ITranscriptPersistence _persistence;
    private readonly ITranscriptRepository _transcripts;
    private readonly IInterventionTimer _interventionTimer;
    private readonly IAnalysisScheduler _analysisScheduler;

    public TranscriptIngestionService(
        ITranscriptBuffer buffer,
        ITranscriptPersistence persistence,
        ITranscriptRepository transcripts,
        IInterventionTimer interventionTimer,
        IAnalysisScheduler analysisScheduler)
    {
        _buffer = buffer;
        _persistence = persistence;
        _transcripts = transcripts;
        _interventionTimer = interventionTimer;
        _analysisScheduler = analysisScheduler;
    }

    public async Task AppendAsync(
        MeetingSession session,
        TranscriptSegment segment,
        CancellationToken ct = default)
    {
        var normalizedSegment = segment with { MeetingId = session.MeetingId };

        await _buffer.AppendAsync(session.Id, normalizedSegment, ct);
        await _persistence.AppendSegmentAsync(
            session.TenantId,
            session.MeetingId,
            session.Id,
            normalizedSegment,
            ct);
        await _transcripts.AddAsync(new TranscriptEntry
        {
            Id = normalizedSegment.Id,
            SessionId = session.Id,
            SpeakerId = normalizedSegment.SpeakerId,
            SpeakerName = normalizedSegment.SpeakerName,
            Text = normalizedSegment.Text,
            Timestamp = normalizedSegment.Timestamp,
            Confidence = normalizedSegment.Confidence,
            Language = normalizedSegment.Language,
        }, ct);

        await _interventionTimer.ResetSilenceTimerAsync(session.Id, ct);
        await _analysisScheduler.RequestAnalysisAsync(session.Id, "new_segment", ct);
    }
}