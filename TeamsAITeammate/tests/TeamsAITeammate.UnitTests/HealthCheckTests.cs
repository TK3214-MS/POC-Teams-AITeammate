using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Moq;
using TeamsAITeammate.Infrastructure.Services;

namespace TeamsAITeammate.UnitTests;

public class HealthCheckTests
{
    [Fact]
    public async Task CosmosDBHealthCheck_WithEndpoint_ReturnsHealthy()
    {
        var config = CreateConfig(new Dictionary<string, string?> { ["CosmosDb:Endpoint"] = "https://cosmos.test" });
        var check = new CosmosDBHealthCheck(config, Mock.Of<ILogger<CosmosDBHealthCheck>>());

        var result = await check.CheckHealthAsync(CreateContext());

        Assert.Equal(HealthStatus.Healthy, result.Status);
    }

    [Fact]
    public async Task CosmosDBHealthCheck_WithoutEndpoint_ReturnsDegraded()
    {
        var config = CreateConfig(new Dictionary<string, string?>());
        var check = new CosmosDBHealthCheck(config, Mock.Of<ILogger<CosmosDBHealthCheck>>());

        var result = await check.CheckHealthAsync(CreateContext());

        Assert.Equal(HealthStatus.Degraded, result.Status);
    }

    [Fact]
    public async Task AzureAISearchHealthCheck_WithEndpoint_ReturnsHealthy()
    {
        var config = CreateConfig(new Dictionary<string, string?> { ["AzureAISearch:Endpoint"] = "https://search.test" });
        var check = new AzureAISearchHealthCheck(config, Mock.Of<ILogger<AzureAISearchHealthCheck>>());

        var result = await check.CheckHealthAsync(CreateContext());

        Assert.Equal(HealthStatus.Healthy, result.Status);
    }

    [Fact]
    public async Task AzureAISearchHealthCheck_WithoutEndpoint_ReturnsDegraded()
    {
        var config = CreateConfig(new Dictionary<string, string?>());
        var check = new AzureAISearchHealthCheck(config, Mock.Of<ILogger<AzureAISearchHealthCheck>>());

        var result = await check.CheckHealthAsync(CreateContext());

        Assert.Equal(HealthStatus.Degraded, result.Status);
    }

    [Fact]
    public async Task GraphAPIHealthCheck_WithAppId_ReturnsHealthy()
    {
        var config = CreateConfig(new Dictionary<string, string?> { ["Agents:MicrosoftAppId"] = "test-app-id" });
        var check = new GraphAPIHealthCheck(config, Mock.Of<ILogger<GraphAPIHealthCheck>>());

        var result = await check.CheckHealthAsync(CreateContext());

        Assert.Equal(HealthStatus.Healthy, result.Status);
    }

    [Fact]
    public async Task GraphAPIHealthCheck_WithoutAppId_ReturnsDegraded()
    {
        var config = CreateConfig(new Dictionary<string, string?>());
        var check = new GraphAPIHealthCheck(config, Mock.Of<ILogger<GraphAPIHealthCheck>>());

        var result = await check.CheckHealthAsync(CreateContext());

        Assert.Equal(HealthStatus.Degraded, result.Status);
    }

    [Fact]
    public async Task TranscriptProviderHealthCheck_WithProvider_ReturnsHealthy()
    {
        var config = CreateConfig(new Dictionary<string, string?> { ["MeetingTranscript:Provider"] = "WorkIQ" });
        var check = new TranscriptProviderHealthCheck(config, Mock.Of<ILogger<TranscriptProviderHealthCheck>>());

        var result = await check.CheckHealthAsync(CreateContext());

        Assert.Equal(HealthStatus.Healthy, result.Status);
        Assert.Contains("WorkIQ", result.Description);
    }

    [Fact]
    public async Task TranscriptProviderHealthCheck_WithoutProvider_ReturnsDegraded()
    {
        var config = CreateConfig(new Dictionary<string, string?>());
        var check = new TranscriptProviderHealthCheck(config, Mock.Of<ILogger<TranscriptProviderHealthCheck>>());

        var result = await check.CheckHealthAsync(CreateContext());

        Assert.Equal(HealthStatus.Degraded, result.Status);
    }

    private static IConfiguration CreateConfig(Dictionary<string, string?> values)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }

    private static HealthCheckContext CreateContext() => new()
    {
        Registration = new HealthCheckRegistration("test", Mock.Of<IHealthCheck>(), null, null)
    };
}
