using TeamsAITeammate.Core.Models;

namespace TeamsAITeammate.UnitTests;

public class AdminModelsTests
{
    [Fact]
    public void AgentSettings_DefaultValues_AreCorrect()
    {
        var settings = new AgentSettings();

        Assert.Equal("medium", settings.Intervention.Frequency);
        Assert.Equal(15, settings.Intervention.SilenceThresholdSeconds);
        Assert.Equal(20, settings.Intervention.MaxInterventionsPerMeeting);
        Assert.True(settings.Intervention.EnableProactiveIntervention);
        Assert.Equal(60, settings.Intervention.CooldownSeconds);
    }

    [Fact]
    public void AgentSettings_QuestionGeneration_DefaultValues()
    {
        var settings = new AgentSettings();

        Assert.Equal(3, settings.QuestionGeneration.MaxQuestionsPerIntervention);
        Assert.Equal("Medium", settings.QuestionGeneration.PriorityThreshold);
        Assert.Equal(5, settings.QuestionGeneration.EnabledCategories.Count);
    }

    [Fact]
    public void AgentSettings_DataStore_DefaultValues()
    {
        var settings = new AgentSettings();

        Assert.Equal("CosmosDB", settings.DataStore.PrimaryProvider);
        Assert.True(settings.DataStore.EnableRAG);
        Assert.Equal(0.7, settings.DataStore.RagMinRelevanceScore);
    }

    [Fact]
    public void AgentSettings_Language_DefaultValues()
    {
        var settings = new AgentSettings();

        Assert.True(settings.Language.AutoDetect);
        Assert.Equal("ja-JP", settings.Language.PreferredLanguage);
        Assert.Contains("ja-JP", settings.Language.SupportedLanguages);
        Assert.Contains("en-US", settings.Language.SupportedLanguages);
    }

    [Fact]
    public void AgentSettings_MeetingFilter_DefaultValues()
    {
        var settings = new AgentSettings();

        Assert.True(settings.MeetingFilter.IncludeAllMeetings);
        Assert.Empty(settings.MeetingFilter.IncludedOrganizers);
        Assert.Empty(settings.MeetingFilter.ExcludedMeetingPatterns);
        Assert.Equal(2, settings.MeetingFilter.MinimumParticipants);
    }

    [Fact]
    public void AgentSettings_WithTenant_SetsCorrectly()
    {
        var settings = new AgentSettings
        {
            TenantId = "tenant-123",
            Intervention = new InterventionConfig { Frequency = "high" }
        };

        Assert.Equal("tenant-123", settings.TenantId);
        Assert.Equal("high", settings.Intervention.Frequency);
    }

    [Fact]
    public void DashboardStats_DefaultValues()
    {
        var stats = new DashboardStats();

        Assert.Equal(0, stats.TotalKnowledgeEntries);
        Assert.Equal(0, stats.TotalMeetingSessions);
        Assert.Empty(stats.KnowledgeByCategory);
    }

    [Fact]
    public void TenantUser_DefaultRole_IsUser()
    {
        var user = new TenantUser();

        Assert.Equal(UserRole.User, user.Role);
    }

    [Fact]
    public void TenantUser_WithAdminRole()
    {
        var user = new TenantUser { Role = UserRole.Admin };

        Assert.Equal(UserRole.Admin, user.Role);
    }

    [Fact]
    public void AuditLogEntry_HasIdAndTimestamp()
    {
        var entry = new AuditLogEntry
        {
            TenantId = "t1",
            UserId = "u1",
            Action = "UpdateSettings"
        };

        Assert.NotEmpty(entry.Id);
        Assert.True(entry.Timestamp <= DateTimeOffset.UtcNow);
        Assert.Equal("UpdateSettings", entry.Action);
    }

    [Fact]
    public void UserRole_HasThreeLevels()
    {
        var values = Enum.GetValues<UserRole>();
        Assert.Equal(3, values.Length);
        Assert.Contains(UserRole.Viewer, values);
        Assert.Contains(UserRole.User, values);
        Assert.Contains(UserRole.Admin, values);
    }

    [Fact]
    public void AICostStats_DefaultValues()
    {
        var stats = new AICostStats();

        Assert.Equal(0, stats.TotalPromptTokens);
        Assert.Equal(0, stats.TotalCompletionTokens);
        Assert.Equal(0m, stats.EstimatedCostUsd);
    }
}
