using TeamsAITeammate.Core.Models;
using TeamsAITeammate.Infrastructure.Services;

namespace TeamsAITeammate.UnitTests;

public class GraphTranscriptProviderTests
{
    [Fact]
    public void ParseVtt_EmptyContent_ReturnsEmpty()
    {
        var result = GraphTranscriptProvider.ParseVtt("", "meeting-1");
        Assert.Empty(result);
    }

    [Fact]
    public void ParseVtt_ValidVtt_ParsesSegments()
    {
        var vtt = """
            WEBVTT

            00:00:01.000 --> 00:00:03.500
            <v Alice>Hello everyone, let's get started.</v>

            00:00:04.000 --> 00:00:07.000
            <v Bob>Sounds good, thanks for setting this up.</v>
            """;

        var result = GraphTranscriptProvider.ParseVtt(vtt, "meeting-1");

        Assert.Equal(2, result.Count);
        Assert.Equal("Alice", result[0].SpeakerName);
        Assert.Equal("Hello everyone, let's get started.", result[0].Text);
        Assert.Equal("Bob", result[1].SpeakerName);
        Assert.Equal("Sounds good, thanks for setting this up.", result[1].Text);
        Assert.Equal("meeting-1", result[0].MeetingId);
    }

    [Fact]
    public void ParseVtt_WithoutSpeakerTags_ParsesText()
    {
        var vtt = """
            WEBVTT

            00:00:01.000 --> 00:00:03.000
            Plain text without speaker tags.
            """;

        var result = GraphTranscriptProvider.ParseVtt(vtt, "meeting-1");

        Assert.Single(result);
        Assert.Equal("Plain text without speaker tags.", result[0].Text);
        Assert.Equal(string.Empty, result[0].SpeakerName);
    }

    [Fact]
    public void ParseVtt_MultiLineSegment_CombinesText()
    {
        var vtt = """
            WEBVTT

            00:00:01.000 --> 00:00:05.000
            <v Alice>First line of text.</v>

            00:00:06.000 --> 00:00:10.000
            <v Alice>Second segment by Alice.</v>
            """;

        var result = GraphTranscriptProvider.ParseVtt(vtt, "meeting-1");

        Assert.Equal(2, result.Count);
        Assert.All(result, s => Assert.Equal("Alice", s.SpeakerName));
    }

    [Fact]
    public void ParseVtt_CorrectTimestamps()
    {
        var vtt = """
            WEBVTT

            01:30:45.500 --> 01:30:50.000
            <v Speaker>Test</v>
            """;

        var result = GraphTranscriptProvider.ParseVtt(vtt, "meeting-1");

        Assert.Single(result);
        var segment = result[0];
        var expected = new TimeSpan(0, 1, 30, 45, 500);
        Assert.Equal(DateTimeOffset.UnixEpoch.Add(expected), segment.Timestamp);
        Assert.Equal(TimeSpan.FromMilliseconds(4500), segment.Duration);
    }

    [Fact]
    public void ParseVtt_SkipsEmptyTextSegments()
    {
        var vtt = """
            WEBVTT

            00:00:01.000 --> 00:00:02.000

            00:00:03.000 --> 00:00:05.000
            <v Alice>Actual content here.</v>
            """;

        var result = GraphTranscriptProvider.ParseVtt(vtt, "meeting-1");

        Assert.Single(result);
        Assert.Equal("Actual content here.", result[0].Text);
    }

    [Fact]
    public void ParseVtt_Confidence_IsOne()
    {
        var vtt = """
            WEBVTT

            00:00:01.000 --> 00:00:03.000
            <v Alice>Test</v>
            """;

        var result = GraphTranscriptProvider.ParseVtt(vtt, "meeting-1");

        Assert.Single(result);
        Assert.Equal(1.0f, result[0].Confidence);
    }
}
