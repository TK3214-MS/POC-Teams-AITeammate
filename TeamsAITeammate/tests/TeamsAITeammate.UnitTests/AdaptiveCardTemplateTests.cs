using System.Text.Json;
using TeamsAITeammate.Core.Models;
using TeamsAITeammate.Infrastructure.Services;

namespace TeamsAITeammate.UnitTests;

public class AdaptiveCardTemplateTests
{
    [Fact]
    public void BuildQuestionCard_Japanese_ReturnsValidJson()
    {
        var question = new GeneratedQuestion
        {
            Id = "q1",
            Question = "なぜこのアプローチですか？",
            Type = QuestionType.WhyQuestion,
            Priority = QuestionPriority.High,
            Rationale = "背景が不明確"
        };

        var json = AdaptiveCardTemplates.BuildQuestionCard(question, "ja");

        Assert.NotEmpty(json);
        var doc = JsonDocument.Parse(json);
        Assert.Equal("AdaptiveCard", doc.RootElement.GetProperty("type").GetString());
        Assert.Equal("1.6", doc.RootElement.GetProperty("version").GetString());
    }

    [Fact]
    public void BuildQuestionCard_English_ReturnsValidJson()
    {
        var question = new GeneratedQuestion
        {
            Id = "q1",
            Question = "Why this approach?",
            Rationale = "Background unclear"
        };

        var json = AdaptiveCardTemplates.BuildQuestionCard(question, "en");

        var doc = JsonDocument.Parse(json);
        Assert.Equal("AdaptiveCard", doc.RootElement.GetProperty("type").GetString());
    }

    [Fact]
    public void BuildQuestionCard_ContainsActionVerbs()
    {
        var question = new GeneratedQuestion { Id = "q1", Question = "Test?" };

        var json = AdaptiveCardTemplates.BuildQuestionCard(question, "ja");

        Assert.Contains("questionAnswer", json);
        Assert.Contains("questionSkip", json);
        Assert.Contains("questionDefer", json);
    }

    [Fact]
    public void BuildAgendaSuggestionCard_ReturnsValidJson()
    {
        var items = new[]
        {
            new SuggestedAgendaItem { Id = "a1", Title = "Review timeline", Rationale = "Not discussed", Priority = QuestionPriority.High },
            new SuggestedAgendaItem { Id = "a2", Title = "Budget review", Rationale = "Pending", Priority = QuestionPriority.Low }
        };

        var json = AdaptiveCardTemplates.BuildAgendaSuggestionCard(items, "ja");

        Assert.NotEmpty(json);
        var doc = JsonDocument.Parse(json);
        Assert.Equal("AdaptiveCard", doc.RootElement.GetProperty("type").GetString());
        Assert.Contains("agendaAccept", json);
        Assert.Contains("agendaSkipAll", json);
    }

    [Fact]
    public void BuildAgendaSuggestionCard_ShowsPriorityIcons()
    {
        var items = new[]
        {
            new SuggestedAgendaItem { Priority = QuestionPriority.Critical },
            new SuggestedAgendaItem { Priority = QuestionPriority.High },
            new SuggestedAgendaItem { Priority = QuestionPriority.Medium },
            new SuggestedAgendaItem { Priority = QuestionPriority.Low }
        };

        var json = AdaptiveCardTemplates.BuildAgendaSuggestionCard(items, "en");

        Assert.Contains("🔴", json);
        Assert.Contains("🟠", json);
        Assert.Contains("🟡", json);
        Assert.Contains("🟢", json);
    }

    [Fact]
    public void BuildTacitKnowledgeConfirmCard_ReturnsValidJson()
    {
        var candidate = new TacitKnowledgeCandidate
        {
            Id = "k1",
            Content = "Important insight about the system",
            Category = TacitKnowledgeCategory.ExpertKnowledge,
            SourceSpeaker = "Alice",
            Context = "We've always handled it this way"
        };

        var json = AdaptiveCardTemplates.BuildTacitKnowledgeConfirmCard(candidate, "ja");

        Assert.NotEmpty(json);
        var doc = JsonDocument.Parse(json);
        Assert.Equal("AdaptiveCard", doc.RootElement.GetProperty("type").GetString());
        Assert.Contains("knowledgeConfirm", json);
        Assert.Contains("knowledgeEdit", json);
        Assert.Contains("knowledgeReject", json);
    }

    [Fact]
    public void BuildConversationSummaryCard_ReturnsValidJson()
    {
        var analysis = new ConversationAnalysis
        {
            Topics = new[]
            {
                new DetectedTopic { Title = "Design", Summary = "UI discussion", Status = TopicStatus.Active },
                new DetectedTopic { Title = "Budget", Summary = "Cost review", Status = TopicStatus.Concluded }
            },
            Decisions = new[]
            {
                new DetectedDecision { Summary = "Use React" }
            },
            ActionItems = new[]
            {
                new ActionItem { Description = "Create mockup", Assignee = "Bob" }
            },
            TacitKnowledgeCandidates = new[]
            {
                new TacitKnowledgeCandidate { Content = "Legacy approach" }
            },
            Questions = new[]
            {
                new GeneratedQuestion { Question = "Why?" }
            }
        };

        var json = AdaptiveCardTemplates.BuildConversationSummaryCard(analysis, "ja");

        Assert.NotEmpty(json);
        var doc = JsonDocument.Parse(json);
        Assert.Equal("AdaptiveCard", doc.RootElement.GetProperty("type").GetString());
        Assert.Contains("openSidePanel", json);
    }

    [Fact]
    public void BuildSettingsCard_ReturnsValidJson()
    {
        var settings = new InterventionSettings();

        var json = AdaptiveCardTemplates.BuildSettingsCard(settings, "ja");

        Assert.NotEmpty(json);
        var doc = JsonDocument.Parse(json);
        Assert.Equal("AdaptiveCard", doc.RootElement.GetProperty("type").GetString());
        Assert.Contains("settingsUpdate", json);
        Assert.Contains("settingsCancel", json);
    }

    [Fact]
    public void BuildSettingsCard_English_ReturnsValidJson()
    {
        var settings = new InterventionSettings
        {
            SilenceThreshold = TimeSpan.FromSeconds(15),
            EnableProactiveIntervention = false,
            MaxInterventionsPerMeeting = 10
        };

        var json = AdaptiveCardTemplates.BuildSettingsCard(settings, "en");

        var doc = JsonDocument.Parse(json);
        Assert.Equal("AdaptiveCard", doc.RootElement.GetProperty("type").GetString());
    }

    [Fact]
    public void BuildConversationSummaryCard_EmptyAnalysis_StillValid()
    {
        var analysis = new ConversationAnalysis();

        var json = AdaptiveCardTemplates.BuildConversationSummaryCard(analysis, "en");

        var doc = JsonDocument.Parse(json);
        Assert.Equal("AdaptiveCard", doc.RootElement.GetProperty("type").GetString());
    }
}
