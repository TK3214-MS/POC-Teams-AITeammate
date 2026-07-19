using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using TeamsAITeammate.Core.Interfaces;
using TeamsAITeammate.Core.Models;

namespace TeamsAITeammate.AI.Services;

public class RagEnhancedConversationAnalyzer : IConversationAnalyzer
{
    private readonly ConversationAnalyzer _baseAnalyzer;
    private readonly IKnowledgeRetriever _retriever;
    private readonly IChatClient _chatClient;
    private readonly ILogger<RagEnhancedConversationAnalyzer> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public RagEnhancedConversationAnalyzer(
        Kernel kernel,
        IChatClient chatClient,
        IKnowledgeRetriever retriever,
        ILogger<RagEnhancedConversationAnalyzer> logger)
    {
        _chatClient = chatClient;
        _retriever = retriever;
        _logger = logger;
        _baseAnalyzer = new ConversationAnalyzer(
            kernel, chatClient,
            LoggerFactory.Create(b => b.AddProvider(new NullLoggerProvider()))
                .CreateLogger<ConversationAnalyzer>());
    }

    internal RagEnhancedConversationAnalyzer(
        ConversationAnalyzer baseAnalyzer,
        IChatClient chatClient,
        IKnowledgeRetriever retriever,
        ILogger<RagEnhancedConversationAnalyzer> logger)
    {
        _baseAnalyzer = baseAnalyzer;
        _chatClient = chatClient;
        _retriever = retriever;
        _logger = logger;
    }

    public async Task<ConversationAnalysis> AnalyzeAsync(
        ConversationWindow conversation,
        AnalysisContext context,
        CancellationToken ct = default)
    {
        if (conversation.Segments.Count == 0)
        {
            return new ConversationAnalysis
            {
                Metadata = new AnalysisMetadata
                {
                    AnalysisType = "empty",
                    AnalysisDuration = TimeSpan.Zero
                }
            };
        }

        var sw = Stopwatch.StartNew();

        // 1. Extract keywords from conversation for RAG query
        var queryText = BuildRagQuery(conversation, context);

        // 2. Retrieve relevant knowledge
        var retrievalQuery = new RetrievalQuery
        {
            QueryText = queryText,
            TenantId = context.SessionId.Length > 0
                ? context.PriorKnowledge.FirstOrDefault()?.Source ?? string.Empty
                : string.Empty,
            Strategy = RetrievalStrategy.HybridSearch,
            MaxResults = 5,
            MinRelevanceScore = 0.5f
        };

        IReadOnlyList<RetrievalResult> retrievedKnowledge;
        try
        {
            retrievedKnowledge = await _retriever.RetrieveAsync(retrievalQuery, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "RAG retrieval failed, falling back to base analyzer");
            retrievedKnowledge = [];
        }

        // 3. Inject retrieved knowledge into context
        var enrichedContext = EnrichContext(context, retrievedKnowledge);

        // 4. Run analysis with enriched context
        var analysis = await _baseAnalyzer.AnalyzeAsync(conversation, enrichedContext, ct);

        // 5. Detect contradictions with past knowledge
        var contradictions = DetectContradictions(analysis, retrievedKnowledge);

        sw.Stop();

        // Update metadata to reflect RAG usage
        var metadata = analysis.Metadata with
        {
            AnalysisType = "rag-enhanced",
            AnalysisDuration = sw.Elapsed
        };

        return analysis with { Metadata = metadata };
    }

    internal static string BuildRagQuery(ConversationWindow conversation, AnalysisContext context)
    {
        var parts = new List<string>();

        if (!string.IsNullOrEmpty(context.MeetingSubject))
            parts.Add(context.MeetingSubject);

        // Take last few segments as they're most relevant
        var recentSegments = conversation.Segments
            .OrderByDescending(s => s.Timestamp)
            .Take(5)
            .Select(s => s.Text);

        parts.AddRange(recentSegments);

        return string.Join(" ", parts);
    }

    internal static AnalysisContext EnrichContext(
        AnalysisContext context,
        IReadOnlyList<RetrievalResult> retrievedKnowledge)
    {
        if (retrievedKnowledge.Count == 0)
            return context;

        var priorKnowledge = new List<RelevantKnowledge>(context.PriorKnowledge);
        foreach (var result in retrievedKnowledge)
        {
            priorKnowledge.Add(new RelevantKnowledge
            {
                Id = result.Entry.Id,
                Content = result.Entry.Content,
                Source = $"{result.Entry.MeetingSubject} ({result.Entry.MeetingDate:yyyy-MM-dd})",
                RelevanceScore = result.RelevanceScore
            });
        }

        return context with { PriorKnowledge = priorKnowledge };
    }

    internal static IReadOnlyList<string> DetectContradictions(
        ConversationAnalysis analysis,
        IReadOnlyList<RetrievalResult> retrievedKnowledge)
    {
        var contradictions = new List<string>();

        foreach (var decision in analysis.Decisions)
        {
            foreach (var knowledge in retrievedKnowledge)
            {
                if (knowledge.Entry.Category == TacitKnowledgeCategory.DecisionBackground
                    && knowledge.RelevanceScore > 0.8f)
                {
                    // Flag high-relevance past decisions for review
                    contradictions.Add(
                        $"Past: {knowledge.Entry.Content} | Current: {decision.Summary}");
                }
            }
        }

        return contradictions;
    }

    private sealed class NullLoggerProvider : ILoggerProvider
    {
        public ILogger CreateLogger(string categoryName) => new NullLogger();
        public void Dispose() { }

        private sealed class NullLogger : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => false;
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
                Exception? exception, Func<TState, Exception?, string> formatter) { }
        }
    }
}
