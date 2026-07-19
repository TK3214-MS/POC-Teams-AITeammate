using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using TeamsAITeammate.Core.Interfaces;
using TeamsAITeammate.Core.Models;

namespace TeamsAITeammate.AI.Services;

public class AnalysisScheduler : IAnalysisScheduler
{
    private readonly IConversationAnalyzer _analyzer;
    private readonly IQuestionGenerator _questionGenerator;
    private readonly ITacitKnowledgeExtractor _tacitKnowledgeExtractor;
    private readonly ITranscriptBuffer _transcriptBuffer;
    private readonly IInterventionTimer _interventionTimer;
    private readonly ILogger<AnalysisScheduler> _logger;

    private readonly ConcurrentDictionary<string, SchedulerState> _sessions = new();
    private readonly ConcurrentDictionary<string, Timer> _debounceTimers = new();
    private readonly ConcurrentDictionary<string, Timer> _periodicTimers = new();

    private static readonly TimeSpan DebounceInterval = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan PeriodicInterval = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan IncrementalWindow = TimeSpan.FromMinutes(5);

    public AnalysisScheduler(
        IConversationAnalyzer analyzer,
        IQuestionGenerator questionGenerator,
        ITacitKnowledgeExtractor tacitKnowledgeExtractor,
        ITranscriptBuffer transcriptBuffer,
        IInterventionTimer interventionTimer,
        ILogger<AnalysisScheduler> logger)
    {
        _analyzer = analyzer;
        _questionGenerator = questionGenerator;
        _tacitKnowledgeExtractor = tacitKnowledgeExtractor;
        _transcriptBuffer = transcriptBuffer;
        _interventionTimer = interventionTimer;
        _logger = logger;
    }

    public event Func<string, ConversationAnalysis, Task>? OnAnalysisCompleted;

    public async Task StartAsync(string sessionId, CancellationToken ct = default)
    {
        var state = new SchedulerState(sessionId);
        _sessions[sessionId] = state;

        // Subscribe to intervention timer events
        _interventionTimer.OnSilenceDetected += async evt =>
        {
            if (evt.SessionId == sessionId)
                await RequestAnalysisAsync(sessionId, "silence", ct);
        };

        _interventionTimer.OnTopicChanged += async evt =>
        {
            if (evt.SessionId == sessionId)
                await RequestAnalysisAsync(sessionId, "topic_change", ct);
        };

        _interventionTimer.OnPeriodicAnalysis += async evt =>
        {
            if (evt.SessionId == sessionId)
                await RequestAnalysisAsync(sessionId, "periodic", ct);
        };

        // Start periodic timer
        var periodicTimer = new Timer(
            async _ => await RequestAnalysisAsync(sessionId, "periodic", ct),
            null, PeriodicInterval, PeriodicInterval);
        _periodicTimers[sessionId] = periodicTimer;

        _logger.LogInformation("Analysis scheduler started for session {SessionId}", sessionId);
        await Task.CompletedTask;
    }

    public async Task StopAsync(string sessionId, CancellationToken ct = default)
    {
        _sessions.TryRemove(sessionId, out _);

        if (_debounceTimers.TryRemove(sessionId, out var debounceTimer))
            await debounceTimer.DisposeAsync();

        if (_periodicTimers.TryRemove(sessionId, out var periodicTimer))
            await periodicTimer.DisposeAsync();

        _logger.LogInformation("Analysis scheduler stopped for session {SessionId}", sessionId);
    }

    public async Task RequestAnalysisAsync(string sessionId, string trigger, CancellationToken ct = default)
    {
        if (!_sessions.TryGetValue(sessionId, out var state))
            return;

        if (state.IsAnalyzing)
        {
            _logger.LogDebug("Analysis already in progress for {SessionId}, skipping", sessionId);
            return;
        }

        switch (trigger)
        {
            case "new_segment":
                // Debounce: wait 10 seconds before running incremental analysis
                ScheduleDebounced(sessionId, async () =>
                    await RunIncrementalAnalysis(sessionId, state, ct));
                break;

            case "silence":
                // Full analysis + question generation on silence
                await RunFullAnalysis(sessionId, state, ct);
                break;

            case "topic_change":
                // Extract tacit knowledge from previous topic + prepare questions
                await RunTopicChangeAnalysis(sessionId, state, ct);
                break;

            case "periodic":
                // Summary update + question queue refresh
                await RunIncrementalAnalysis(sessionId, state, ct);
                break;

            case "mention":
                // On-demand full analysis
                await RunFullAnalysis(sessionId, state, ct);
                break;

            default:
                await RunIncrementalAnalysis(sessionId, state, ct);
                break;
        }
    }

    private void ScheduleDebounced(string sessionId, Func<Task> action)
    {
        if (_debounceTimers.TryRemove(sessionId, out Timer? existingTimer))
            existingTimer.Dispose();

        var timer = new Timer(async _ =>
        {
            _debounceTimers.TryRemove(sessionId, out Timer? _);
            await action();
        }, null, DebounceInterval, Timeout.InfiniteTimeSpan);

        _debounceTimers[sessionId] = timer;
    }

    private async Task RunIncrementalAnalysis(
        string sessionId, SchedulerState state, CancellationToken ct)
    {
        state.IsAnalyzing = true;
        try
        {
            var window = await _transcriptBuffer.GetRecentWindowAsync(
                sessionId, IncrementalWindow, ct);

            if (window.Segments.Count == 0)
                return;

            var context = BuildContext(sessionId, state);
            var analysis = await _analyzer.AnalyzeAsync(window, context, ct);

            state.LastAnalysis = analysis;
            OnAnalysisCompleted?.Invoke(sessionId, analysis);

            _logger.LogInformation(
                "Incremental analysis completed for {SessionId}: {TopicCount} topics, {DecisionCount} decisions",
                sessionId, analysis.Topics.Count, analysis.Decisions.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Incremental analysis failed for session {SessionId}", sessionId);
        }
        finally
        {
            state.IsAnalyzing = false;
        }
    }

    private async Task RunFullAnalysis(
        string sessionId, SchedulerState state, CancellationToken ct)
    {
        state.IsAnalyzing = true;
        try
        {
            var window = await _transcriptBuffer.GetFullConversationAsync(sessionId, ct);

            if (window.Segments.Count == 0)
                return;

            var context = BuildContext(sessionId, state);

            // Run analysis and question generation in parallel
            var analysisTask = _analyzer.AnalyzeAsync(window, context, ct);
            var questionsTask = _questionGenerator.GenerateQuestionsAsync(
                window, context, new QuestionGenerationOptions
                {
                    MaxQuestions = 5,
                    AvoidDuplicates = true,
                    AlreadyAskedQuestionIds = state.AskedQuestionIds.ToList()
                }, ct);
            var tacitTask = _tacitKnowledgeExtractor.ExtractAsync(window, context, ct);

            await Task.WhenAll(analysisTask, questionsTask, tacitTask);

            var analysis = analysisTask.Result with
            {
                Questions = questionsTask.Result,
                TacitKnowledgeCandidates = tacitTask.Result
            };

            // Track asked questions for deduplication
            foreach (var q in questionsTask.Result)
                state.AskedQuestionIds.Add(q.Id);

            state.LastAnalysis = analysis;
            OnAnalysisCompleted?.Invoke(sessionId, analysis);

            _logger.LogInformation(
                "Full analysis completed for {SessionId}: {TopicCount} topics, {QuestionCount} questions, {TacitCount} tacit knowledge",
                sessionId, analysis.Topics.Count, analysis.Questions.Count,
                analysis.TacitKnowledgeCandidates.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Full analysis failed for session {SessionId}", sessionId);
        }
        finally
        {
            state.IsAnalyzing = false;
        }
    }

    private async Task RunTopicChangeAnalysis(
        string sessionId, SchedulerState state, CancellationToken ct)
    {
        state.IsAnalyzing = true;
        try
        {
            var window = await _transcriptBuffer.GetRecentWindowAsync(
                sessionId, IncrementalWindow, ct);

            if (window.Segments.Count == 0)
                return;

            var context = BuildContext(sessionId, state);
            var tacitKnowledge = await _tacitKnowledgeExtractor.ExtractAsync(window, context, ct);
            var questions = await _questionGenerator.GenerateQuestionsAsync(
                window, context, new QuestionGenerationOptions
                {
                    MaxQuestions = 3,
                    AvoidDuplicates = true,
                    AlreadyAskedQuestionIds = state.AskedQuestionIds.ToList()
                }, ct);

            var analysis = (state.LastAnalysis ?? new ConversationAnalysis()) with
            {
                TacitKnowledgeCandidates = tacitKnowledge,
                Questions = questions
            };

            foreach (var q in questions)
                state.AskedQuestionIds.Add(q.Id);

            state.LastAnalysis = analysis;
            OnAnalysisCompleted?.Invoke(sessionId, analysis);

            _logger.LogInformation(
                "Topic change analysis for {SessionId}: {TacitCount} tacit knowledge, {QuestionCount} questions",
                sessionId, tacitKnowledge.Count, questions.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Topic change analysis failed for session {SessionId}", sessionId);
        }
        finally
        {
            state.IsAnalyzing = false;
        }
    }

    private static AnalysisContext BuildContext(string sessionId, SchedulerState state)
    {
        return new AnalysisContext
        {
            SessionId = sessionId,
            MeetingSubject = state.MeetingSubject,
            Participants = state.Participants,
            DetectedLanguage = state.DetectedLanguage,
            PreviousAnalysis = state.LastAnalysis
        };
    }

    internal class SchedulerState
    {
        public SchedulerState(string sessionId)
        {
            SessionId = sessionId;
        }

        public string SessionId { get; }
        public string MeetingSubject { get; set; } = string.Empty;
        public List<string> Participants { get; set; } = [];
        public string DetectedLanguage { get; set; } = "ja-JP";
        public ConversationAnalysis? LastAnalysis { get; set; }
        public HashSet<string> AskedQuestionIds { get; } = [];
        public volatile bool IsAnalyzing;
    }
}
