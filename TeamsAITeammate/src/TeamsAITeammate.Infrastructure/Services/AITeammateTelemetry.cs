using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using TeamsAITeammate.Core.Interfaces;

namespace TeamsAITeammate.Infrastructure.Services;

/// <summary>Application Insights カスタムテレメトリ実装</summary>
public class AITeammateTelemetry : IAITeammateTelemetry
{
    private readonly TelemetryClient _telemetry;

    public AITeammateTelemetry(TelemetryClient telemetry)
    {
        _telemetry = telemetry;
    }

    private void TrackEventWithMetrics(string name, Dictionary<string, string> properties,
        Dictionary<string, double>? metrics = null)
    {
        _telemetry.TrackEvent(name, properties, metrics);
    }

    public void TrackAnalysisExecution(string sessionId, TimeSpan duration,
        int topicsDetected, int questionsGenerated, int knowledgeExtracted)
    {
        TrackEventWithMetrics("AnalysisExecution", new Dictionary<string, string>
        {
            ["SessionId"] = sessionId
        }, new Dictionary<string, double>
        {
            ["DurationMs"] = duration.TotalMilliseconds,
            ["TopicsDetected"] = topicsDetected,
            ["QuestionsGenerated"] = questionsGenerated,
            ["KnowledgeExtracted"] = knowledgeExtracted
        });

        _telemetry.GetMetric("AnalysisLatencyMs").TrackValue(duration.TotalMilliseconds);
    }

    public void TrackTranscriptProcessing(string sessionId, int segmentCount,
        TimeSpan processingTime)
    {
        TrackEventWithMetrics("TranscriptProcessing", new Dictionary<string, string>
        {
            ["SessionId"] = sessionId
        }, new Dictionary<string, double>
        {
            ["SegmentCount"] = segmentCount,
            ["ProcessingTimeMs"] = processingTime.TotalMilliseconds
        });
    }

    public void TrackUserInteraction(string sessionId, string actionType, string cardType)
    {
        TrackEventWithMetrics("UserInteraction", new Dictionary<string, string>
        {
            ["SessionId"] = sessionId,
            ["ActionType"] = actionType,
            ["CardType"] = cardType
        });
    }

    public void TrackAIModelUsage(string modelName, int promptTokens,
        int completionTokens, TimeSpan latency)
    {
        TrackEventWithMetrics("AIModelUsage", new Dictionary<string, string>
        {
            ["ModelName"] = modelName
        }, new Dictionary<string, double>
        {
            ["PromptTokens"] = promptTokens,
            ["CompletionTokens"] = completionTokens,
            ["LatencyMs"] = latency.TotalMilliseconds
        });

        _telemetry.GetMetric("AIPromptTokens", "ModelName").TrackValue(promptTokens, modelName);
        _telemetry.GetMetric("AICompletionTokens", "ModelName").TrackValue(completionTokens, modelName);
        _telemetry.GetMetric("AILatencyMs", "ModelName").TrackValue(latency.TotalMilliseconds, modelName);
    }

    public void TrackKnowledgeIngestion(string tenantId, string category, string storeProvider)
    {
        TrackEventWithMetrics("KnowledgeIngestion", new Dictionary<string, string>
        {
            ["TenantId"] = tenantId,
            ["Category"] = category,
            ["StoreProvider"] = storeProvider
        });

        _telemetry.GetMetric("KnowledgeIngested", "Category").TrackValue(1, category);
    }

    public void TrackMeetingJoined(string meetingId, string tenantId, int participantCount)
    {
        TrackEventWithMetrics("MeetingJoined", new Dictionary<string, string>
        {
            ["MeetingId"] = meetingId,
            ["TenantId"] = tenantId
        }, new Dictionary<string, double>
        {
            ["ParticipantCount"] = participantCount
        });
    }

    public void TrackMeetingLeft(string meetingId, TimeSpan sessionDuration,
        int totalInterventions, int totalKnowledgeEntries)
    {
        TrackEventWithMetrics("MeetingLeft", new Dictionary<string, string>
        {
            ["MeetingId"] = meetingId
        }, new Dictionary<string, double>
        {
            ["SessionDurationMin"] = sessionDuration.TotalMinutes,
            ["TotalInterventions"] = totalInterventions,
            ["TotalKnowledgeEntries"] = totalKnowledgeEntries
        });
    }

    public void TrackTranscriptError(string provider, Exception ex)
    {
        var telemetry = new ExceptionTelemetry(ex)
        {
            SeverityLevel = SeverityLevel.Error
        };
        telemetry.Properties["Provider"] = provider;
        telemetry.Properties["ErrorType"] = "TranscriptError";
        _telemetry.TrackException(telemetry);
    }

    public void TrackAIModelError(string model, Exception ex)
    {
        var telemetry = new ExceptionTelemetry(ex)
        {
            SeverityLevel = SeverityLevel.Error
        };
        telemetry.Properties["Model"] = model;
        telemetry.Properties["ErrorType"] = "AIModelError";
        _telemetry.TrackException(telemetry);
    }
}
