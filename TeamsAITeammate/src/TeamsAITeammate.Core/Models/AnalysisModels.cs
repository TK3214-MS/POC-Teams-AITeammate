namespace TeamsAITeammate.Core.Models;

public record ConversationAnalysis
{
    public IReadOnlyList<DetectedTopic> Topics { get; init; } = [];
    public IReadOnlyList<TacitKnowledgeCandidate> TacitKnowledgeCandidates { get; init; } = [];
    public IReadOnlyList<GeneratedQuestion> Questions { get; init; } = [];
    public IReadOnlyList<SuggestedAgendaItem> SuggestedAgenda { get; init; } = [];
    public IReadOnlyList<DetectedDecision> Decisions { get; init; } = [];
    public IReadOnlyList<ActionItem> ActionItems { get; init; } = [];
    public AnalysisMetadata Metadata { get; init; } = new();
}

public record AnalysisContext
{
    public string SessionId { get; init; } = string.Empty;
    public string MeetingSubject { get; init; } = string.Empty;
    public IReadOnlyList<string> Participants { get; init; } = [];
    public string DetectedLanguage { get; init; } = string.Empty;
    public IReadOnlyList<RelevantKnowledge> PriorKnowledge { get; init; } = [];
    public ConversationAnalysis? PreviousAnalysis { get; init; }
}

public record DetectedTopic
{
    public string Id { get; init; } = Guid.NewGuid().ToString();
    public string Title { get; init; } = string.Empty;
    public string Summary { get; init; } = string.Empty;
    public DateTimeOffset FirstMentionedAt { get; init; }
    public DateTimeOffset LastMentionedAt { get; init; }
    public TopicStatus Status { get; init; }
    public float DiscussionDepth { get; init; }
    public IReadOnlyList<string> KeyTerms { get; init; } = [];
    public IReadOnlyList<string> InvolvedSpeakers { get; init; } = [];
}

public enum TopicStatus
{
    Active,
    Concluded,
    Tabled
}

public record TacitKnowledgeCandidate
{
    public string Id { get; init; } = Guid.NewGuid().ToString();
    public TacitKnowledgeCategory Category { get; init; }
    public string Content { get; init; } = string.Empty;
    public string Context { get; init; } = string.Empty;
    public string SourceSpeaker { get; init; } = string.Empty;
    public float Confidence { get; init; }
    public IReadOnlyList<string> RelatedTopics { get; init; } = [];
    public bool RequiresValidation { get; init; }
}

public enum TacitKnowledgeCategory
{
    DecisionBackground,
    UndocumentedProcess,
    ExpertKnowledge,
    DiscussionHistory,
    OrganizationalContext,
    TechnicalInsight,
    LessonsLearned,
    StakeholderRelationship,
    ImplicitAssumption,
    DomainExpertise
}

public record GeneratedQuestion
{
    public string Id { get; init; } = Guid.NewGuid().ToString();
    public string Question { get; init; } = string.Empty;
    public QuestionType Type { get; init; }
    public QuestionPriority Priority { get; init; }
    public string Rationale { get; init; } = string.Empty;
    public string TargetSpeaker { get; init; } = string.Empty;
    public string RelatedTopicId { get; init; } = string.Empty;
    public TacitKnowledgeCategory ExpectedKnowledgeCategory { get; init; }
}

public enum QuestionType
{
    WhyQuestion,
    ImpactQuestion,
    ClarificationQuestion,
    AlternativeQuestion,
    TimelineQuestion,
    StakeholderQuestion,
    RiskQuestion,
    ProcessQuestion,
    PrecedentQuestion,
    AssumptionQuestion
}

public enum QuestionPriority
{
    Critical,
    High,
    Medium,
    Low
}

public record QuestionGenerationOptions
{
    public int MaxQuestions { get; init; } = 5;
    public IReadOnlyList<QuestionType> PreferredTypes { get; init; } = [];
    public bool AvoidDuplicates { get; init; } = true;
    public IReadOnlyList<string> AlreadyAskedQuestionIds { get; init; } = [];
}

public record SuggestedAgendaItem
{
    public string Id { get; init; } = Guid.NewGuid().ToString();
    public string Title { get; init; } = string.Empty;
    public string Rationale { get; init; } = string.Empty;
    public QuestionPriority Priority { get; init; }
    public IReadOnlyList<string> RelatedTopicIds { get; init; } = [];
}

public record DetectedDecision
{
    public string Id { get; init; } = Guid.NewGuid().ToString();
    public string Summary { get; init; } = string.Empty;
    public string Context { get; init; } = string.Empty;
    public IReadOnlyList<string> DecisionMakers { get; init; } = [];
    public DateTimeOffset DetectedAt { get; init; }
    public float Confidence { get; init; }
}

public record ActionItem
{
    public string Id { get; init; } = Guid.NewGuid().ToString();
    public string Description { get; init; } = string.Empty;
    public string Assignee { get; init; } = string.Empty;
    public DateTimeOffset? DueDate { get; init; }
    public ActionItemStatus Status { get; init; }
    public string RelatedTopicId { get; init; } = string.Empty;
}

public enum ActionItemStatus
{
    Open,
    InProgress,
    Completed,
    Cancelled
}

public record AnalysisMetadata
{
    public string AnalysisId { get; init; } = Guid.NewGuid().ToString();
    public DateTimeOffset AnalyzedAt { get; init; } = DateTimeOffset.UtcNow;
    public TimeSpan AnalysisDuration { get; init; }
    public string ModelUsed { get; init; } = string.Empty;
    public bool UsedFallbackModel { get; init; }
    public int TokensUsed { get; init; }
    public string AnalysisType { get; init; } = string.Empty;
}

public record RelevantKnowledge
{
    public string Id { get; init; } = string.Empty;
    public string Content { get; init; } = string.Empty;
    public string Source { get; init; } = string.Empty;
    public float RelevanceScore { get; init; }
}
