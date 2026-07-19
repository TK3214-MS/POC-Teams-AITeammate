namespace TeamsAITeammate.IntegrationTests;

public class HealthCheckTests
{
    [Fact(Skip = "Requires running application")]
    public async Task HealthEndpoint_ShouldReturnHealthy()
    {
        using var client = new HttpClient { BaseAddress = new Uri("http://localhost:5000") };
        var response = await client.GetAsync("/healthz");
        response.EnsureSuccessStatusCode();
    }
}
