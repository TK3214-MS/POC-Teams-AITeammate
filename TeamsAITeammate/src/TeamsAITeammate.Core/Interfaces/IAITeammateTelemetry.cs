namespace TeamsAITeammate.Core.Interfaces;

/// <summary>Application Insightsカスタムテレメトリ</summary>
public interface IAITeammateTelemetry
{
    void TrackAnalysisExecution(string sessionId, TimeSpan duration,
        int topicsDetected, int questionsGenerated, int knowledgeExtracted);

    void TrackTranscriptProcessing(string sessionId, int segmentCount,
        TimeSpan processingTime);

    void TrackUserInteraction(string sessionId, string actionType, string cardType);

    void TrackAIModelUsage(string modelName, int promptTokens,
        int completionTokens, TimeSpan latency);

    void TrackKnowledgeIngestion(string tenantId, string category, string storeProvider);

    void TrackMeetingJoined(string meetingId, string tenantId, int participantCount);

    void TrackMeetingLeft(string meetingId, TimeSpan sessionDuration,
        int totalInterventions, int totalKnowledgeEntries);

    void TrackTranscriptError(string provider, Exception ex);
    void TrackAIModelError(string model, Exception ex);
}
