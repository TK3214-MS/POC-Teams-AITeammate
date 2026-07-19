using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Azure.Identity;
using TeamsAITeammate.Core.Interfaces;
using TeamsAITeammate.Core.Models;

namespace TeamsAITeammate.Infrastructure.Services;

public class DataSyncService : IDataSyncService
{
    private readonly IKnowledgeStoreFactory _storeFactory;
    private readonly IEmbeddingService _embeddingService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<DataSyncService> _logger;
    private ChangeFeedProcessor? _changeFeedProcessor;

    public DataSyncService(
        IKnowledgeStoreFactory storeFactory,
        IEmbeddingService embeddingService,
        IConfiguration configuration,
        ILogger<DataSyncService> logger)
    {
        _storeFactory = storeFactory;
        _embeddingService = embeddingService;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task SyncToSecondaryAsync(string tenantId, CancellationToken ct)
    {
        var primaryStore = _storeFactory.CreateStore("CosmosDB");
        var secondaryStore = _storeFactory.CreateStore("AzureAISearch");

        var entries = await primaryStore.SearchAsync(
            string.Empty,
            new KnowledgeSearchOptions { TenantId = tenantId, MaxResults = 1000 },
            ct);

        foreach (var entry in entries)
        {
            var entryWithEmbedding = entry;
            if (entry.Embedding is null)
            {
                var textToEmbed = $"{entry.Title} {entry.Content} {entry.Summary}";
                var embedding = await _embeddingService.GenerateEmbeddingAsync(textToEmbed, ct);
                entryWithEmbedding = entry with { Embedding = embedding };
            }

            await secondaryStore.SaveKnowledgeAsync(entryWithEmbedding, ct);
        }

        _logger.LogInformation(
            "Synced {Count} entries for tenant {TenantId} to secondary store",
            entries.Count, tenantId);
    }

    public async Task StartChangeFeedProcessorAsync(CancellationToken ct)
    {
        var endpoint = _configuration["CosmosDb:Endpoint"];
        if (string.IsNullOrEmpty(endpoint))
        {
            _logger.LogWarning("Cosmos DB endpoint not configured, skipping change feed processor");
            return;
        }

        var databaseName = _configuration["CosmosDb:DatabaseName"]!;
        var containerName = _configuration["CosmosDb:KnowledgeContainer"]!;
        var leaseContainerName = "knowledge-leases";

        var client = new CosmosClient(endpoint, new DefaultAzureCredential());
        var database = client.GetDatabase(databaseName);
        var container = database.GetContainer(containerName);

        // Ensure lease container exists
        await database.CreateContainerIfNotExistsAsync(
            new ContainerProperties(leaseContainerName, "/id"),
            cancellationToken: ct);

        var leaseContainer = database.GetContainer(leaseContainerName);

        _changeFeedProcessor = container
            .GetChangeFeedProcessorBuilder<KnowledgeEntry>(
                "KnowledgeSyncProcessor",
                HandleChangesAsync)
            .WithInstanceName("instance-1")
            .WithLeaseContainer(leaseContainer)
            .WithStartTime(DateTime.UtcNow)
            .Build();

        await _changeFeedProcessor.StartAsync();
        _logger.LogInformation("Change feed processor started for knowledge sync");
    }

    public async Task StopChangeFeedProcessorAsync(CancellationToken ct)
    {
        if (_changeFeedProcessor is not null)
        {
            await _changeFeedProcessor.StopAsync();
            _logger.LogInformation("Change feed processor stopped");
        }
    }

    private async Task HandleChangesAsync(
        ChangeFeedProcessorContext context,
        IReadOnlyCollection<KnowledgeEntry> changes,
        CancellationToken ct)
    {
        var secondaryStore = _storeFactory.CreateStore("AzureAISearch");

        foreach (var entry in changes)
        {
            try
            {
                var entryWithEmbedding = entry;
                if (entry.Embedding is null && !string.IsNullOrEmpty(entry.Content))
                {
                    var textToEmbed = $"{entry.Title} {entry.Content} {entry.Summary}";
                    var embedding = await _embeddingService.GenerateEmbeddingAsync(textToEmbed, ct);
                    entryWithEmbedding = entry with { Embedding = embedding };
                }

                await secondaryStore.SaveKnowledgeAsync(entryWithEmbedding, ct);
                _logger.LogDebug("Synced entry {Id} via change feed", entry.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to sync entry {Id} via change feed", entry.Id);
            }
        }
    }
}
