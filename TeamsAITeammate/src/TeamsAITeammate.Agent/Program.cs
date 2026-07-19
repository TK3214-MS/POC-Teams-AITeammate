using Azure.Identity;
using Azure.Storage.Blobs;
using Microsoft.Agents.Hosting.AspNetCore;
using TeamsAITeammate.AI.Services;
using TeamsAITeammate.Core.Interfaces;
using TeamsAITeammate.Infrastructure.Repositories;
using TeamsAITeammate.Infrastructure.Services;

var builder = WebApplication.CreateBuilder(args);

builder.AddAgentDefaults()
    .AddAgent<TeammateAgent>();

// Core services
builder.Services.AddSingleton<IAnalysisEngine, AnalysisEngine>();
builder.Services.AddSingleton<IMeetingSessionRepository, CosmosMeetingSessionRepository>();
builder.Services.AddSingleton<ITranscriptRepository, CosmosTranscriptRepository>();
builder.Services.AddSingleton<IKnowledgeRepository, CosmosKnowledgeRepository>();
builder.Services.AddSingleton<GraphClientService>();

// Phase 2 services
builder.Services.AddSingleton<ICommandParser, CommandParser>();
builder.Services.AddSingleton<IMeetingSessionManager, MeetingSessionManager>();
builder.Services.AddSingleton<IGraphMeetingClient, GraphMeetingClient>();
builder.Services.AddSingleton<IInterventionTimer, InterventionTimer>();

// Phase 3 services — Transcript pipeline
builder.Services.AddSingleton<ITranscriptProvider, WorkIQTranscriptProvider>();
builder.Services.AddSingleton<ITranscriptProvider, GraphTranscriptProvider>();
builder.Services.AddSingleton<ITranscriptBuffer, TranscriptBuffer>();
builder.Services.AddSingleton<ILanguageDetector, LanguageDetector>();
builder.Services.AddSingleton(sp =>
{
    var endpoint = builder.Configuration["BlobStorage:Endpoint"];
    if (!string.IsNullOrEmpty(endpoint))
        return new BlobServiceClient(new Uri(endpoint), new DefaultAzureCredential());
    var connectionString = builder.Configuration["BlobStorage:ConnectionString"]
        ?? "UseDevelopmentStorage=true";
    return new BlobServiceClient(connectionString);
});
builder.Services.AddSingleton<ITranscriptPersistence, TranscriptPersistenceService>();
builder.Services.AddHostedService<TranscriptPipelineOrchestrator>();

builder.Services.AddHealthChecks();

var app = builder.Build();

app.UseAgents();
app.MapHealthChecks("/healthz");

app.Run();
