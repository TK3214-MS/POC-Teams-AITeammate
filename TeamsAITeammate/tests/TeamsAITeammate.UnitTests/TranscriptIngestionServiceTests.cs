using Moq;
using TeamsAITeammate.Core.Interfaces;
using TeamsAITeammate.Core.Models;
using TeamsAITeammate.Infrastructure.Services;

namespace TeamsAITeammate.UnitTests;

public class TranscriptIngestionServiceTests
{
    [Fact]
    public async Task AppendAsync_PersistsAndSchedulesNormalizedSegment()
    {
        var buffer = new Mock<ITranscriptBuffer>();
        var persistence = new Mock<ITranscriptPersistence>();
        var transcripts = new Mock<ITranscriptRepository>();
        var timer = new Mock<IInterventionTimer>();
        var scheduler = new Mock<IAnalysisScheduler>();
        var service = new TranscriptIngestionService(
            buffer.Object,
            persistence.Object,
            transcripts.Object,
            timer.Object,
            scheduler.Object);
        var session = new MeetingSession
        {
            Id = "session-1",
            TenantId = "tenant-1",
            MeetingId = "meeting-1",
        };
        var segment = new TranscriptSegment
        {
            Id = "segment-1",
            MeetingId = "untrusted-meeting",
            SpeakerId = "user-1",
            SpeakerName = "User One",
            Text = "確認します",
            Language = "ja-JP",
            Timestamp = DateTimeOffset.UtcNow,
            Duration = TimeSpan.FromSeconds(2),
            Confidence = 0.95f,
        };

        await service.AppendAsync(session, segment);

        buffer.Verify(b => b.AppendAsync(
            "session-1",
            It.Is<TranscriptSegment>(s => s.MeetingId == "meeting-1" && s.Id == "segment-1"),
            It.IsAny<CancellationToken>()), Times.Once);
        persistence.Verify(p => p.AppendSegmentAsync(
            "tenant-1",
            "meeting-1",
            "session-1",
            It.Is<TranscriptSegment>(s => s.MeetingId == "meeting-1"),
            It.IsAny<CancellationToken>()), Times.Once);
        transcripts.Verify(r => r.AddAsync(
            It.Is<TranscriptEntry>(e =>
                e.Id == "segment-1" &&
                e.SessionId == "session-1" &&
                e.Text == "確認します"),
            It.IsAny<CancellationToken>()), Times.Once);
        timer.Verify(t => t.ResetSilenceTimerAsync(
            "session-1", It.IsAny<CancellationToken>()), Times.Once);
        scheduler.Verify(s => s.RequestAnalysisAsync(
            "session-1", "new_segment", It.IsAny<CancellationToken>()), Times.Once);
    }
}