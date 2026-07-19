using Microsoft.Extensions.Logging;
using Moq;
using TeamsAITeammate.Core.Models;
using TeamsAITeammate.Infrastructure.Services;

namespace TeamsAITeammate.UnitTests;

public class TranscriptBufferTests
{
    private readonly TranscriptBuffer _buffer;

    public TranscriptBufferTests()
    {
        _buffer = new TranscriptBuffer(Mock.Of<ILogger<TranscriptBuffer>>());
    }

    private static TranscriptSegment MakeSegment(
        string meetingId, string speakerId, string text,
        DateTimeOffset timestamp, TimeSpan? duration = null, string language = "ja-JP") =>
        new()
        {
            MeetingId = meetingId,
            SpeakerId = speakerId,
            SpeakerName = speakerId,
            Text = text,
            Timestamp = timestamp,
            Duration = duration ?? TimeSpan.FromSeconds(2),
            Language = language,
            Confidence = 1.0f,
        };

    [Fact]
    public async Task AppendAsync_And_GetFull_ReturnsAllSegments()
    {
        var now = DateTimeOffset.UtcNow;
        var s1 = MakeSegment("m1", "alice", "Hello", now);
        var s2 = MakeSegment("m1", "bob", "Hi", now.AddSeconds(3));

        await _buffer.AppendAsync(s1);
        await _buffer.AppendAsync(s2);

        var window = await _buffer.GetFullConversationAsync("m1");

        Assert.Equal(2, window.Segments.Count);
        Assert.Equal("m1", window.SessionId);
        Assert.Equal(2, window.UniqueSpeakerCount);
    }

    [Fact]
    public async Task GetRecentWindowAsync_FiltersOldSegments()
    {
        var now = DateTimeOffset.UtcNow;
        var old = MakeSegment("m1", "alice", "Old", now.AddMinutes(-10));
        var recent = MakeSegment("m1", "bob", "Recent", now.AddSeconds(-30));

        await _buffer.AppendAsync(old);
        await _buffer.AppendAsync(recent);

        var window = await _buffer.GetRecentWindowAsync("m1", TimeSpan.FromMinutes(2));

        Assert.Single(window.Segments);
        Assert.Equal("Recent", window.Segments[0].Text);
    }

    [Fact]
    public async Task GetSpeakerStatsAsync_CorrectStats()
    {
        var now = DateTimeOffset.UtcNow;
        await _buffer.AppendAsync(MakeSegment("m1", "alice", "One", now, TimeSpan.FromSeconds(3)));
        await _buffer.AppendAsync(MakeSegment("m1", "alice", "Two", now.AddSeconds(5), TimeSpan.FromSeconds(4)));
        await _buffer.AppendAsync(MakeSegment("m1", "bob", "Three", now.AddSeconds(10), TimeSpan.FromSeconds(2)));

        var stats = await _buffer.GetSpeakerStatsAsync("m1");

        Assert.Equal(2, stats.Count);
        Assert.Equal(2, stats["alice"].SegmentCount);
        Assert.Equal(TimeSpan.FromSeconds(7), stats["alice"].TotalSpeakingTime);
        Assert.Equal(1, stats["bob"].SegmentCount);
    }

    [Fact]
    public async Task DetectSilencePeriodsAsync_FindsSilence()
    {
        var now = DateTimeOffset.UtcNow;
        await _buffer.AppendAsync(MakeSegment("m1", "alice", "Before", now, TimeSpan.FromSeconds(2)));
        // 60 second gap
        await _buffer.AppendAsync(MakeSegment("m1", "bob", "After", now.AddSeconds(62), TimeSpan.FromSeconds(2)));

        var silences = await _buffer.DetectSilencePeriodsAsync("m1", TimeSpan.FromSeconds(30));

        Assert.Single(silences);
        Assert.Equal(TimeSpan.FromSeconds(60), silences[0].Duration);
    }

    [Fact]
    public async Task DetectSilencePeriodsAsync_NoSilence_WhenContinuous()
    {
        var now = DateTimeOffset.UtcNow;
        await _buffer.AppendAsync(MakeSegment("m1", "alice", "One", now, TimeSpan.FromSeconds(3)));
        await _buffer.AppendAsync(MakeSegment("m1", "bob", "Two", now.AddSeconds(3), TimeSpan.FromSeconds(3)));

        var silences = await _buffer.DetectSilencePeriodsAsync("m1", TimeSpan.FromSeconds(5));

        Assert.Empty(silences);
    }

    [Fact]
    public async Task GetFullConversation_EmptySession_ReturnsEmptyWindow()
    {
        var window = await _buffer.GetFullConversationAsync("nonexistent");

        Assert.Empty(window.Segments);
    }

    [Fact]
    public async Task ConversationWindow_ToFormattedTranscript_FormatsCorrectly()
    {
        var now = new DateTimeOffset(2025, 7, 1, 10, 30, 0, TimeSpan.Zero);
        await _buffer.AppendAsync(MakeSegment("m1", "Alice", "Hello everyone", now));
        await _buffer.AppendAsync(MakeSegment("m1", "Bob", "Hi Alice", now.AddSeconds(3)));

        var window = await _buffer.GetFullConversationAsync("m1");
        var formatted = window.ToFormattedTranscript();

        Assert.Contains("[10:30:00] Alice: Hello everyone", formatted);
        Assert.Contains("[10:30:03] Bob: Hi Alice", formatted);
    }

    [Fact]
    public async Task GetFullConversation_DetectsLanguage()
    {
        var now = DateTimeOffset.UtcNow;
        await _buffer.AppendAsync(MakeSegment("m1", "alice", "Hello", now, language: "en-US"));
        await _buffer.AppendAsync(MakeSegment("m1", "bob", "こんにちは", now.AddSeconds(3), language: "ja-JP"));
        await _buffer.AppendAsync(MakeSegment("m1", "alice", "テスト", now.AddSeconds(6), language: "ja-JP"));

        var window = await _buffer.GetFullConversationAsync("m1");

        Assert.Equal("ja-JP", window.DetectedLanguage);
    }

    [Fact]
    public async Task MultipleSessions_IndependentBuffers()
    {
        var now = DateTimeOffset.UtcNow;
        await _buffer.AppendAsync(MakeSegment("m1", "alice", "Meeting 1", now));
        await _buffer.AppendAsync(MakeSegment("m2", "bob", "Meeting 2", now));

        var w1 = await _buffer.GetFullConversationAsync("m1");
        var w2 = await _buffer.GetFullConversationAsync("m2");

        Assert.Single(w1.Segments);
        Assert.Single(w2.Segments);
        Assert.Equal("Meeting 1", w1.Segments[0].Text);
        Assert.Equal("Meeting 2", w2.Segments[0].Text);
    }
}
