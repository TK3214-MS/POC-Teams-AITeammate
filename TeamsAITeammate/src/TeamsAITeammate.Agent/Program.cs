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

builder.Services.AddHealthChecks();

var app = builder.Build();

app.UseAgents();
app.MapHealthChecks("/healthz");

app.Run();
