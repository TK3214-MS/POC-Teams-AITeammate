using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
using TeamsAITeammate.Infrastructure.Services;

namespace TeamsAITeammate.UnitTests;

public class AITeammateTelemetryTests
{
    private readonly List<ITelemetry> _sentTelemetry = [];
    private readonly AITeammateTelemetry _telemetry;

    public AITeammateTelemetryTests()
    {
        var config = new TelemetryConfiguration
        {
            TelemetryChannel = new StubTelemetryChannel(_sentTelemetry),
            ConnectionString = "InstrumentationKey=test-key;IngestionEndpoint=https://test.in.ai.monitor.azure.com/"
        };
        var client = new TelemetryClient(config);
        _telemetry = new AITeammateTelemetry(client);
    }

    [Fact]
    public void TrackAnalysisExecution_DoesNotThrow()
    {
        var ex = Record.Exception(() =>
            _telemetry.TrackAnalysisExecution("session-1", TimeSpan.FromSeconds(5), 3, 2, 1));
        Assert.Null(ex);

        var evt = _sentTelemetry.OfType<EventTelemetry>().FirstOrDefault(e => e.Name == "AnalysisExecution");
        Assert.NotNull(evt);
        Assert.Equal("session-1", evt.Properties["SessionId"]);
    }

    [Fact]
    public void TrackTranscriptProcessing_SendsEvent()
    {
        _telemetry.TrackTranscriptProcessing("session-2", 10, TimeSpan.FromMilliseconds(500));

        var evt = _sentTelemetry.OfType<EventTelemetry>().FirstOrDefault(e => e.Name == "TranscriptProcessing");
        Assert.NotNull(evt);
        Assert.Equal("session-2", evt.Properties["SessionId"]);
    }

    [Fact]
    public void TrackUserInteraction_SendsEvent()
    {
        _telemetry.TrackUserInteraction("session-3", "questionAnswer", "QuestionCard");

        var evt = _sentTelemetry.OfType<EventTelemetry>().FirstOrDefault(e => e.Name == "UserInteraction");
        Assert.NotNull(evt);
        Assert.Equal("questionAnswer", evt.Properties["ActionType"]);
        Assert.Equal("QuestionCard", evt.Properties["CardType"]);
    }

    [Fact]
    public void TrackAIModelUsage_SendsEvent()
    {
        _telemetry.TrackAIModelUsage("gpt-55", 100, 200, TimeSpan.FromSeconds(2));

        var evt = _sentTelemetry.OfType<EventTelemetry>().FirstOrDefault(e => e.Name == "AIModelUsage");
        Assert.NotNull(evt);
        Assert.Equal("gpt-55", evt.Properties["ModelName"]);
    }

    [Fact]
    public void TrackKnowledgeIngestion_SendsEvent()
    {
        _telemetry.TrackKnowledgeIngestion("tenant-1", "ExpertiseSkill", "CosmosDB");

        var evt = _sentTelemetry.OfType<EventTelemetry>().FirstOrDefault(e => e.Name == "KnowledgeIngestion");
        Assert.NotNull(evt);
        Assert.Equal("tenant-1", evt.Properties["TenantId"]);
        Assert.Equal("ExpertiseSkill", evt.Properties["Category"]);
    }

    [Fact]
    public void TrackMeetingJoined_SendsEvent()
    {
        _telemetry.TrackMeetingJoined("meeting-1", "tenant-1", 5);

        var evt = _sentTelemetry.OfType<EventTelemetry>().FirstOrDefault(e => e.Name == "MeetingJoined");
        Assert.NotNull(evt);
        Assert.Equal("meeting-1", evt.Properties["MeetingId"]);
    }

    [Fact]
    public void TrackMeetingLeft_SendsEvent()
    {
        _telemetry.TrackMeetingLeft("meeting-2", TimeSpan.FromMinutes(30), 5, 10);

        var evt = _sentTelemetry.OfType<EventTelemetry>().FirstOrDefault(e => e.Name == "MeetingLeft");
        Assert.NotNull(evt);
        Assert.Equal("meeting-2", evt.Properties["MeetingId"]);
    }

    [Fact]
    public void TrackTranscriptError_SendsException()
    {
        var ex = new InvalidOperationException("Test error");
        _telemetry.TrackTranscriptError("GraphAPI", ex);

        var exTelemetry = _sentTelemetry.OfType<ExceptionTelemetry>().FirstOrDefault();
        Assert.NotNull(exTelemetry);
        Assert.Equal("GraphAPI", exTelemetry.Properties["Provider"]);
        Assert.Equal("TranscriptError", exTelemetry.Properties["ErrorType"]);
    }

    [Fact]
    public void TrackAIModelError_SendsException()
    {
        var ex = new TimeoutException("Model timeout");
        _telemetry.TrackAIModelError("gpt-55", ex);

        var exTelemetry = _sentTelemetry.OfType<ExceptionTelemetry>().FirstOrDefault();
        Assert.NotNull(exTelemetry);
        Assert.Equal("gpt-55", exTelemetry.Properties["Model"]);
        Assert.Equal("AIModelError", exTelemetry.Properties["ErrorType"]);
    }

    private class StubTelemetryChannel : ITelemetryChannel
    {
        private readonly List<ITelemetry> _items;
        public StubTelemetryChannel(List<ITelemetry> items) => _items = items;
        public bool? DeveloperMode { get; set; } = true;
        public string EndpointAddress { get; set; } = "https://test";
        public void Dispose() { }
        public void Flush() { }
        public void Send(ITelemetry item) => _items.Add(item);
    }
}
