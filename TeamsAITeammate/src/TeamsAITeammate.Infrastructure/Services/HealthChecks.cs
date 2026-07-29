using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;

namespace TeamsAITeammate.Infrastructure.Services;

/// <summary>Azure OpenAI ヘルスチェック</summary>
public class AzureOpenAIHealthCheck : IHealthCheck
{
    private readonly IConfiguration _config;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<AzureOpenAIHealthCheck> _logger;

    public AzureOpenAIHealthCheck(IConfiguration config, IHttpClientFactory httpClientFactory,
        ILogger<AzureOpenAIHealthCheck> logger)
    {
        _config = config;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context,
        CancellationToken ct = default)
    {
        try
        {
            var endpoint = _config["AzureOpenAI:Endpoint"];
            if (string.IsNullOrEmpty(endpoint))
                return HealthCheckResult.Degraded("Azure OpenAI endpoint not configured");

            // Just verify endpoint is reachable
            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(5);
            var response = await client.GetAsync(new Uri(new Uri(endpoint), "openai/models?api-version=2025-06-01-preview"), ct);

            return response.IsSuccessStatusCode || response.StatusCode is System.Net.HttpStatusCode.Unauthorized
                or System.Net.HttpStatusCode.NotFound
                ? HealthCheckResult.Healthy("Azure OpenAI is reachable")
                : HealthCheckResult.Degraded($"Azure OpenAI returned {response.StatusCode}");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Azure OpenAI health check failed");
            return HealthCheckResult.Unhealthy("Azure OpenAI is unreachable", ex);
        }
    }
}

/// <summary>Cosmos DB ヘルスチェック</summary>
public class CosmosDBHealthCheck : IHealthCheck
{
    private readonly IConfiguration _config;
    private readonly ILogger<CosmosDBHealthCheck> _logger;

    public CosmosDBHealthCheck(IConfiguration config, ILogger<CosmosDBHealthCheck> logger)
    {
        _config = config;
        _logger = logger;
    }

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context,
        CancellationToken ct = default)
    {
        try
        {
            var endpoint = _config["CosmosDb:Endpoint"];
            if (string.IsNullOrEmpty(endpoint))
                return Task.FromResult(HealthCheckResult.Degraded("Cosmos DB endpoint not configured"));

            return Task.FromResult(HealthCheckResult.Healthy("Cosmos DB endpoint configured"));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cosmos DB health check failed");
            return Task.FromResult(HealthCheckResult.Unhealthy("Cosmos DB health check failed", ex));
        }
    }
}

/// <summary>Azure AI Search ヘルスチェック</summary>
public class AzureAISearchHealthCheck : IHealthCheck
{
    private readonly IConfiguration _config;
    private readonly ILogger<AzureAISearchHealthCheck> _logger;

    public AzureAISearchHealthCheck(IConfiguration config, ILogger<AzureAISearchHealthCheck> logger)
    {
        _config = config;
        _logger = logger;
    }

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context,
        CancellationToken ct = default)
    {
        try
        {
            var endpoint = _config["AzureAISearch:Endpoint"];
            if (string.IsNullOrEmpty(endpoint))
                return Task.FromResult(HealthCheckResult.Degraded("AI Search endpoint not configured"));

            return Task.FromResult(HealthCheckResult.Healthy("AI Search endpoint configured"));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AI Search health check failed");
            return Task.FromResult(HealthCheckResult.Unhealthy("AI Search health check failed", ex));
        }
    }
}

/// <summary>Microsoft Graph API ヘルスチェック</summary>
public class GraphAPIHealthCheck : IHealthCheck
{
    private readonly IConfiguration _config;
    private readonly ILogger<GraphAPIHealthCheck> _logger;

    public GraphAPIHealthCheck(IConfiguration config, ILogger<GraphAPIHealthCheck> logger)
    {
        _config = config;
        _logger = logger;
    }

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context,
        CancellationToken ct = default)
    {
        try
        {
            var appId = _config["Agents:MicrosoftAppId"];
            if (string.IsNullOrEmpty(appId))
                return Task.FromResult(HealthCheckResult.Degraded("Bot App ID not configured"));

            return Task.FromResult(HealthCheckResult.Healthy("Graph API configuration present"));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Graph API health check failed");
            return Task.FromResult(HealthCheckResult.Unhealthy("Graph API health check failed", ex));
        }
    }
}

/// <summary>トランスクリプトプロバイダー ヘルスチェック</summary>
public class TranscriptProviderHealthCheck : IHealthCheck
{
    private readonly IConfiguration _config;
    private readonly ILogger<TranscriptProviderHealthCheck> _logger;

    public TranscriptProviderHealthCheck(IConfiguration config,
        ILogger<TranscriptProviderHealthCheck> logger)
    {
        _config = config;
        _logger = logger;
    }

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context,
        CancellationToken ct = default)
    {
        try
        {
            var provider = _config["MeetingTranscript:RealtimeProvider"]
                ?? _config["MeetingTranscript:Provider"];
            if (string.IsNullOrEmpty(provider))
                return Task.FromResult(HealthCheckResult.Degraded("Transcript provider not configured"));

            return Task.FromResult(HealthCheckResult.Healthy($"Transcript provider: {provider}"));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Transcript provider health check failed");
            return Task.FromResult(HealthCheckResult.Unhealthy("Transcript provider health check failed", ex));
        }
    }
}
