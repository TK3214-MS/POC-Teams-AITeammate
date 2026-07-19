using Azure.AI.OpenAI;
using Azure.Identity;
using Azure.Storage.Blobs;
using Microsoft.Agents.Hosting.AspNetCore;
using Microsoft.Extensions.AI;
using Microsoft.SemanticKernel;
using TeamsAITeammate.Agent.Hubs;
using TeamsAITeammate.AI.Services;
using TeamsAITeammate.Core.Interfaces;
using TeamsAITeammate.Infrastructure.Repositories;
using TeamsAITeammate.Infrastructure.Services;

var builder = WebApplication.CreateBuilder(args);

builder.AddAgentDefaults()
    .AddAgent<TeammateAgent>();

// Application Insights
builder.Services.AddApplicationInsightsTelemetry();
builder.Services.AddHttpClient();

// Azure OpenAI + Semantic Kernel
var aoaiEndpoint = builder.Configuration["AzureOpenAI:Endpoint"]!;
var primaryDeployment = builder.Configuration["AzureOpenAI:DeploymentName"] ?? "gpt-55";
var fallbackDeployment = builder.Configuration["AzureOpenAI:FallbackDeploymentName"] ?? "gpt-41";
var credential = new DefaultAzureCredential();

builder.Services.AddKernel()
    .AddAzureOpenAIChatCompletion(
        deploymentName: primaryDeployment,
        endpoint: aoaiEndpoint,
        credentials: credential)
    .AddAzureOpenAIEmbeddingGenerator(
        deploymentName: "text-embedding-3-large",
        endpoint: aoaiEndpoint,
        credential: credential);

// Microsoft.Extensions.AI — Resilient chat client with fallback
builder.Services.AddSingleton<IChatClient>(sp =>
{
    var azureClient = new AzureOpenAIClient(new Uri(aoaiEndpoint), credential);
    var primaryClient = azureClient.GetChatClient(primaryDeployment).AsIChatClient();
    var fallbackClient = azureClient.GetChatClient(fallbackDeployment).AsIChatClient();
    return new ResilientChatClient(
        primaryClient, fallbackClient,
        sp.GetRequiredService<ILogger<ResilientChatClient>>());
});

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
        return new BlobServiceClient(new Uri(endpoint), credential);
    var connectionString = builder.Configuration["BlobStorage:ConnectionString"]
        ?? "UseDevelopmentStorage=true";
    return new BlobServiceClient(connectionString);
});
builder.Services.AddSingleton<ITranscriptPersistence, TranscriptPersistenceService>();
builder.Services.AddHostedService<TranscriptPipelineOrchestrator>();

// Phase 4 services — AI Analysis Engine
builder.Services.AddSingleton<ConversationAnalyzer>();
builder.Services.AddSingleton<IConversationAnalyzer, RagEnhancedConversationAnalyzer>();
builder.Services.AddSingleton<IQuestionGenerator, QuestionGenerator>();
builder.Services.AddSingleton<ITacitKnowledgeExtractor, TacitKnowledgeExtractor>();
builder.Services.AddSingleton<IAnalysisScheduler, AnalysisScheduler>();

// Phase 5 services — Agent Intervention & UI
builder.Services.AddSingleton<INotificationThrottler, NotificationThrottler>();
builder.Services.AddSingleton<IMessageFormatter, MessageFormatter>();
builder.Services.AddSingleton<ICardActionHandler, CardActionHandler>();
builder.Services.AddSingleton<IInterventionOrchestrator, InterventionOrchestrator>();
builder.Services.AddSignalR();

// Phase 6 services — Data Store & Knowledge Base
builder.Services.AddSingleton<IKnowledgeStore, CosmosKnowledgeStore>();
builder.Services.AddSingleton<IKnowledgeStore, DataverseKnowledgeStore>();
builder.Services.AddSingleton<IKnowledgeStore, AzureAISearchKnowledgeStore>();
builder.Services.AddSingleton<IKnowledgeStore, SharePointKnowledgeStore>();
builder.Services.AddSingleton<IKnowledgeStoreFactory, KnowledgeStoreFactory>();
builder.Services.AddSingleton<TenantAwareKnowledgeStoreResolver>();
builder.Services.AddSingleton<IEmbeddingService, EmbeddingService>();
builder.Services.AddSingleton<IKnowledgeIngestionPipeline, KnowledgeIngestionPipeline>();
builder.Services.AddSingleton<IDataSyncService, DataSyncService>();
builder.Services.AddHttpClient<DataverseKnowledgeStore>();

// Phase 7 services — RAG Search & Copilot Studio Integration
builder.Services.AddSingleton<IKnowledgeRetriever, AzureAISearchRetriever>();
builder.Services.AddSingleton<IKnowledgeQualityService, KnowledgeQualityService>();
builder.Services.AddSingleton<IKnowledgeGraphService, KnowledgeGraphService>();
builder.Services.AddSingleton<KnowledgeGraphConnector>();
builder.Services.AddControllers();

// Phase 8 services — Admin, Telemetry, Health Checks, Security
builder.Services.AddSingleton<IAITeammateTelemetry, AITeammateTelemetry>();
builder.Services.AddSingleton<IAgentSettingsRepository, CosmosAgentSettingsRepository>();
builder.Services.AddSingleton<IAuditLogService, CosmosAuditLogService>();
builder.Services.AddSingleton<ITenantUserRepository, CosmosTenantUserRepository>();
builder.Services.AddSingleton<IDashboardService, DashboardService>();

builder.Services.AddHealthChecks()
    .AddCheck<AzureOpenAIHealthCheck>("azure-openai")
    .AddCheck<CosmosDBHealthCheck>("cosmos-db")
    .AddCheck<AzureAISearchHealthCheck>("ai-search")
    .AddCheck<GraphAPIHealthCheck>("graph-api")
    .AddCheck<TranscriptProviderHealthCheck>("transcript-provider");

var app = builder.Build();

app.UseAgents();
app.MapControllers();
app.MapHub<MeetingAnalysisHub>("/hubs/meeting-analysis");
app.MapHealthChecks("/healthz");

app.Run();
