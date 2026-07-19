using TeamsAITeammate.Core.Models;
using TeamsAITeammate.Infrastructure.Services;

namespace TeamsAITeammate.UnitTests;

public class LanguageDetectorTests
{
    [Fact]
    public void DetectLanguageFromText_Japanese_DetectsJapanese()
    {
        var result = LanguageDetector.DetectLanguageFromText("こんにちは、今日の会議を始めましょう");

        Assert.Equal("ja-JP", result.PrimaryLanguage);
        Assert.True(result.Confidence > 0.5f);
    }

    [Fact]
    public void DetectLanguageFromText_English_DetectsEnglish()
    {
        var result = LanguageDetector.DetectLanguageFromText(
            "Hello everyone, let us start the meeting now.");

        Assert.Equal("en-US", result.PrimaryLanguage);
        Assert.True(result.Confidence > 0.5f);
    }

    [Fact]
    public void DetectLanguageFromText_Empty_ReturnsUndetermined()
    {
        var result = LanguageDetector.DetectLanguageFromText("");

        Assert.Equal("und", result.PrimaryLanguage);
        Assert.Equal(0f, result.Confidence);
    }

    [Fact]
    public void DetectLanguageFromText_MixedJapaneseEnglish_DetectsJapanese()
    {
        var result = LanguageDetector.DetectLanguageFromText(
            "では、next stepについて話しましょう。");

        Assert.Equal("ja-JP", result.PrimaryLanguage);
    }

    [Fact]
    public async Task DetectLanguageAsync_UsesSegmentTags_WhenAvailable()
    {
        var detector = new LanguageDetector(
            Microsoft.Extensions.Logging.Abstractions.NullLogger<LanguageDetector>.Instance);

        var segments = new List<TranscriptSegment>
        {
            new() { Text = "Hello", Language = "en-US", SpeakerId = "a" },
            new() { Text = "Test", Language = "en-US", SpeakerId = "b" },
            new() { Text = "テスト", Language = "ja-JP", SpeakerId = "c" },
        };

        var result = await detector.DetectLanguageAsync(segments);

        Assert.Equal("en-US", result.PrimaryLanguage);
        Assert.True(result.LanguageDistribution.ContainsKey("en-US"));
        Assert.True(result.LanguageDistribution.ContainsKey("ja-JP"));
    }

    [Fact]
    public async Task DetectLanguageAsync_EmptySegments_ReturnsUndetermined()
    {
        var detector = new LanguageDetector(
            Microsoft.Extensions.Logging.Abstractions.NullLogger<LanguageDetector>.Instance);

        var result = await detector.DetectLanguageAsync([]);

        Assert.Equal("und", result.PrimaryLanguage);
        Assert.Equal(0f, result.Confidence);
    }

    [Fact]
    public async Task DetectLanguageAsync_FallsBackToHeuristic_WhenNoTags()
    {
        var detector = new LanguageDetector(
            Microsoft.Extensions.Logging.Abstractions.NullLogger<LanguageDetector>.Instance);

        var segments = new List<TranscriptSegment>
        {
            new() { Text = "こんにちは", Language = "", SpeakerId = "a" },
            new() { Text = "会議を始めます", Language = "", SpeakerId = "a" },
        };

        var result = await detector.DetectLanguageAsync(segments);

        Assert.Equal("ja-JP", result.PrimaryLanguage);
    }
}
