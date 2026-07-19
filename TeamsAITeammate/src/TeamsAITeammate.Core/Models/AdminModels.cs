namespace TeamsAITeammate.Core.Models;

/// <summary>エージェント設定</summary>
public record AgentSettings
{
    public string TenantId { get; init; } = string.Empty;

    // 介入設定
    public InterventionConfig Intervention { get; init; } = new();

    // 質問生成設定
    public QuestionGenerationSettings QuestionGeneration { get; init; } = new();

    // データストア設定
    public DataStoreSettings DataStore { get; init; } = new();

    // 言語設定
    public LanguageSettings Language { get; init; } = new();

    // 対象会議フィルター
    public MeetingFilterSettings MeetingFilter { get; init; } = new();

    public DateTimeOffset UpdatedAt { get; init; } = DateTimeOffset.UtcNow;
    public string UpdatedBy { get; init; } = string.Empty;
}

public record InterventionConfig
{
    public string Frequency { get; init; } = "medium"; // low, medium, high
    public int SilenceThresholdSeconds { get; init; } = 15;
    public int MaxInterventionsPerMeeting { get; init; } = 20;
    public bool EnableProactiveIntervention { get; init; } = true;
    public int CooldownSeconds { get; init; } = 60;
}

public record QuestionGenerationSettings
{
    public List<string> EnabledCategories { get; init; } = ["Clarification", "DeepDive", "Alternative", "Impact", "Practical"];
    public int MaxQuestionsPerIntervention { get; init; } = 3;
    public string PriorityThreshold { get; init; } = "Medium"; // Low, Medium, High, Critical
}

public record DataStoreSettings
{
    public string PrimaryProvider { get; init; } = "CosmosDB";
    public bool EnableRAG { get; init; } = true;
    public double RagMinRelevanceScore { get; init; } = 0.7;
}

public record LanguageSettings
{
    public bool AutoDetect { get; init; } = true;
    public string PreferredLanguage { get; init; } = "ja-JP";
    public List<string> SupportedLanguages { get; init; } = ["ja-JP", "en-US"];
}

public record MeetingFilterSettings
{
    public bool IncludeAllMeetings { get; init; } = true;
    public List<string> IncludedOrganizers { get; init; } = [];
    public List<string> ExcludedMeetingPatterns { get; init; } = [];
    public int MinimumParticipants { get; init; } = 2;
}

/// <summary>ダッシュボード統計</summary>
public record DashboardStats
{
    public string TenantId { get; init; } = string.Empty;
    public int TotalKnowledgeEntries { get; init; }
    public int TotalMeetingSessions { get; init; }
    public int TotalAnalysisExecutions { get; init; }
    public int ActiveUsers { get; init; }
    public Dictionary<string, int> KnowledgeByCategory { get; init; } = new();
    public KnowledgeTrend DailyTrend { get; init; } = new();
    public KnowledgeTrend WeeklyTrend { get; init; } = new();
    public KnowledgeTrend MonthlyTrend { get; init; } = new();
    public AICostStats AICost { get; init; } = new();
}

public record KnowledgeTrend
{
    public List<DateTimeOffset> Dates { get; init; } = [];
    public List<int> Counts { get; init; } = [];
}

public record AICostStats
{
    public long TotalPromptTokens { get; init; }
    public long TotalCompletionTokens { get; init; }
    public decimal EstimatedCostUsd { get; init; }
}

/// <summary>ユーザー管理</summary>
public record TenantUser
{
    public string UserId { get; init; } = string.Empty;
    public string TenantId { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public UserRole Role { get; init; } = UserRole.User;
    public UserStats Stats { get; init; } = new();
}

public record UserStats
{
    public int MeetingsAttended { get; init; }
    public int KnowledgeContributed { get; init; }
    public int QuestionsAnswered { get; init; }
    public DateTimeOffset? LastActiveAt { get; init; }
}

public enum UserRole
{
    Viewer,
    User,
    Admin
}

/// <summary>監査ログ</summary>
public record AuditLogEntry
{
    public string Id { get; init; } = Guid.NewGuid().ToString();
    public string TenantId { get; init; } = string.Empty;
    public string UserId { get; init; } = string.Empty;
    public string Action { get; init; } = string.Empty;
    public string ResourceType { get; init; } = string.Empty;
    public string ResourceId { get; init; } = string.Empty;
    public string? Details { get; init; }
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
}
