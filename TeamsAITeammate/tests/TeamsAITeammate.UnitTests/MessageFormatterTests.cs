using TeamsAITeammate.Core.Models;
using TeamsAITeammate.Infrastructure.Services;

namespace TeamsAITeammate.UnitTests;

public class MessageFormatterTests
{
    private readonly MessageFormatter _formatter = new();

    // --- FormatQuestion tests ---

    [Fact]
    public void FormatQuestion_Japanese_FormatsCorrectly()
    {
        var question = new GeneratedQuestion
        {
            Question = "なぜこのアプローチを選びましたか？",
            Rationale = "意思決定の背景が不明確",
            Type = QuestionType.WhyQuestion,
            Priority = QuestionPriority.High
        };

        var result = _formatter.FormatQuestion(question, "ja");

        Assert.Contains("追加で確認したい点があります", result);
        Assert.Contains(question.Question, result);
        Assert.Contains(question.Rationale, result);
    }

    [Fact]
    public void FormatQuestion_English_FormatsCorrectly()
    {
        var question = new GeneratedQuestion
        {
            Question = "Why was this approach chosen?",
            Rationale = "Decision background is unclear",
            Type = QuestionType.WhyQuestion,
            Priority = QuestionPriority.High
        };

        var result = _formatter.FormatQuestion(question, "en");

        Assert.Contains("I'd like to ask a follow-up", result);
        Assert.Contains(question.Question, result);
        Assert.Contains(question.Rationale, result);
    }

    [Fact]
    public void FormatQuestion_WithTarget_IncludesTargetSpeaker()
    {
        var question = new GeneratedQuestion
        {
            Question = "Could you elaborate?",
            Rationale = "More detail needed",
            TargetSpeaker = "Alice"
        };

        var result = _formatter.FormatQuestion(question, "ja");

        Assert.Contains("Aliceさんに確認したい点があります", result);
    }

    [Fact]
    public void FormatQuestion_EnglishWithTarget_IncludesTargetSpeaker()
    {
        var question = new GeneratedQuestion
        {
            Question = "Could you elaborate?",
            Rationale = "More detail needed",
            TargetSpeaker = "Bob"
        };

        var result = _formatter.FormatQuestion(question, "en-US");

        Assert.Contains("Question for Bob", result);
    }

    [Fact]
    public void FormatQuestion_UnknownLanguage_FallsBackToEnglish()
    {
        var question = new GeneratedQuestion
        {
            Question = "Test question",
            Rationale = "Test rationale"
        };

        var result = _formatter.FormatQuestion(question, "fr-FR");

        Assert.Contains("I'd like to ask a follow-up", result);
    }

    [Fact]
    public void FormatQuestion_NullLanguage_DefaultsToJapanese()
    {
        var question = new GeneratedQuestion
        {
            Question = "テスト質問",
            Rationale = "テスト理由"
        };

        var result = _formatter.FormatQuestion(question, "");

        Assert.Contains("追加で確認したい点があります", result);
    }

    // --- FormatSummary tests ---

    [Fact]
    public void FormatSummary_WithTopics_IncludesAllTopics()
    {
        var analysis = new ConversationAnalysis
        {
            Topics = new[]
            {
                new DetectedTopic { Title = "Design", Summary = "UI design discussion", Status = TopicStatus.Active },
                new DetectedTopic { Title = "Budget", Summary = "Cost analysis", Status = TopicStatus.Concluded },
                new DetectedTopic { Title = "Timeline", Summary = "Schedule review", Status = TopicStatus.Tabled }
            }
        };

        var result = _formatter.FormatSummary(analysis, "ja");

        Assert.Contains("トピック", result);
        Assert.Contains("🟢", result); // Active
        Assert.Contains("✅", result); // Concluded
        Assert.Contains("⏸️", result); // Tabled
        Assert.Contains("Design", result);
        Assert.Contains("Budget", result);
    }

    [Fact]
    public void FormatSummary_WithDecisions_IncludesDecisions()
    {
        var analysis = new ConversationAnalysis
        {
            Decisions = new[]
            {
                new DetectedDecision { Summary = "Use React for frontend" }
            }
        };

        var result = _formatter.FormatSummary(analysis, "en");

        Assert.Contains("Decisions", result);
        Assert.Contains("Use React for frontend", result);
    }

    [Fact]
    public void FormatSummary_WithActionItems_IncludesActions()
    {
        var analysis = new ConversationAnalysis
        {
            ActionItems = new[]
            {
                new ActionItem { Description = "Create wireframe", Assignee = "Alice" }
            }
        };

        var result = _formatter.FormatSummary(analysis, "ja");

        Assert.Contains("Create wireframe", result);
        Assert.Contains("Alice", result);
    }

    [Fact]
    public void FormatSummary_WithActionItems_NoAssignee_ShowsTBD()
    {
        var analysis = new ConversationAnalysis
        {
            ActionItems = new[]
            {
                new ActionItem { Description = "Review code" }
            }
        };

        var result = _formatter.FormatSummary(analysis, "en");

        Assert.Contains("TBD", result);
    }

    [Fact]
    public void FormatSummary_WithKnowledge_ShowsCount()
    {
        var analysis = new ConversationAnalysis
        {
            TacitKnowledgeCandidates = new[]
            {
                new TacitKnowledgeCandidate { Content = "Know-how 1" },
                new TacitKnowledgeCandidate { Content = "Know-how 2" }
            }
        };

        var result = _formatter.FormatSummary(analysis, "ja");

        Assert.Contains("2", result);
        Assert.Contains("ナレッジ", result);
    }

    [Fact]
    public void FormatSummary_EmptyAnalysis_ReturnsHeaderOnly()
    {
        var analysis = new ConversationAnalysis();

        var result = _formatter.FormatSummary(analysis, "ja");

        Assert.Contains("会話サマリー", result);
        Assert.DoesNotContain("トピック", result);
    }

    // --- GetLocalizedTemplate tests ---

    [Fact]
    public void GetLocalizedTemplate_ExistingKey_ReturnsTemplate()
    {
        var result = _formatter.GetLocalizedTemplate("silence_prompt", "ja");

        Assert.Contains("会話が静かになりました", result);
    }

    [Fact]
    public void GetLocalizedTemplate_UnknownKey_ReturnsBracketedKey()
    {
        var result = _formatter.GetLocalizedTemplate("nonexistent_key", "ja");

        Assert.Equal("[nonexistent_key]", result);
    }

    [Fact]
    public void GetLocalizedTemplate_UnknownLanguage_FallsBackToEnglish()
    {
        var result = _formatter.GetLocalizedTemplate("silence_prompt", "de-DE");

        Assert.Contains("The conversation has been quiet", result);
    }
}
