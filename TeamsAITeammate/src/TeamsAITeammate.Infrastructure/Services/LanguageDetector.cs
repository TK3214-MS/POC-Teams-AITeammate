using Microsoft.Extensions.Logging;
using TeamsAITeammate.Core.Interfaces;
using TeamsAITeammate.Core.Models;

namespace TeamsAITeammate.Infrastructure.Services;

public class LanguageDetector : ILanguageDetector
{
    private readonly ILogger<LanguageDetector> _logger;

    public LanguageDetector(ILogger<LanguageDetector> logger)
    {
        _logger = logger;
    }

    public Task<LanguageDetectionResult> DetectLanguageAsync(
        IReadOnlyList<TranscriptSegment> segments,
        CancellationToken ct = default)
    {
        if (segments.Count == 0)
        {
            return Task.FromResult(new LanguageDetectionResult
            {
                PrimaryLanguage = "und",
                Confidence = 0f,
            });
        }

        // Use language tags from segments if available
        var tagged = segments
            .Where(s => !string.IsNullOrEmpty(s.Language) && s.Language != "und")
            .ToList();

        if (tagged.Count > 0)
        {
            var distribution = tagged
                .GroupBy(s => s.Language)
                .ToDictionary(g => g.Key, g => (float)g.Count() / tagged.Count);

            var primary = distribution.MaxBy(kv => kv.Value);

            _logger.LogInformation("Detected language from segment tags: {Language} ({Confidence:P0})",
                primary.Key, primary.Value);

            return Task.FromResult(new LanguageDetectionResult
            {
                PrimaryLanguage = primary.Key,
                Confidence = primary.Value,
                LanguageDistribution = distribution,
            });
        }

        // Heuristic fallback: detect based on character ranges
        var allText = string.Join(' ', segments.Select(s => s.Text));
        var detected = DetectLanguageFromText(allText);

        _logger.LogInformation("Detected language from text heuristic: {Language}", detected.PrimaryLanguage);

        return Task.FromResult(detected);
    }

    internal static LanguageDetectionResult DetectLanguageFromText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return new LanguageDetectionResult
            {
                PrimaryLanguage = "und",
                Confidence = 0f,
            };
        }

        int cjkCount = 0, latinCount = 0, totalLetters = 0;

        foreach (var ch in text)
        {
            if (char.IsLetter(ch))
            {
                totalLetters++;

                if (ch >= '\u3000' && ch <= '\u9FFF' || ch >= '\uF900' && ch <= '\uFAFF')
                    cjkCount++;
                else if (ch >= 'A' && ch <= 'z')
                    latinCount++;
            }
        }

        if (totalLetters == 0)
        {
            return new LanguageDetectionResult { PrimaryLanguage = "und", Confidence = 0f };
        }

        float cjkRatio = (float)cjkCount / totalLetters;
        float latinRatio = (float)latinCount / totalLetters;

        // Check for Japanese-specific characters (hiragana/katakana)
        bool hasJapanese = text.Any(ch =>
            ch >= '\u3040' && ch <= '\u309F' ||  // Hiragana
            ch >= '\u30A0' && ch <= '\u30FF');    // Katakana

        string primary;
        float confidence;

        if (hasJapanese || cjkRatio > 0.3f && hasJapanese)
        {
            primary = "ja-JP";
            confidence = Math.Min(0.6f + cjkRatio * 0.4f, 1.0f);
        }
        else if (cjkRatio > 0.5f)
        {
            primary = "zh-CN";
            confidence = cjkRatio;
        }
        else if (latinRatio > 0.5f)
        {
            primary = "en-US";
            confidence = latinRatio;
        }
        else
        {
            primary = "und";
            confidence = 0.3f;
        }

        var distribution = new Dictionary<string, float>();
        if (cjkRatio > 0) distribution[hasJapanese ? "ja-JP" : "zh-CN"] = cjkRatio;
        if (latinRatio > 0) distribution["en-US"] = latinRatio;

        return new LanguageDetectionResult
        {
            PrimaryLanguage = primary,
            Confidence = confidence,
            LanguageDistribution = distribution,
        };
    }
}
