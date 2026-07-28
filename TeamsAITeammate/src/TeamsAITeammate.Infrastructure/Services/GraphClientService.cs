using Azure.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Graph;

namespace TeamsAITeammate.Infrastructure.Services;

public class GraphClientService
{
    private readonly GraphServiceClient _client;

    public GraphClientService(IConfiguration configuration)
    {
        var tenantId = configuration["Agents:MicrosoftAppTenantId"]
            ?? throw new InvalidOperationException("Agents:MicrosoftAppTenantId is required");
        var clientId = configuration["Agents:MicrosoftAppId"]
            ?? throw new InvalidOperationException("Agents:MicrosoftAppId is required");
        var clientSecret = configuration["Agents:MicrosoftAppPassword"]
            ?? throw new InvalidOperationException("Agents:MicrosoftAppPassword is required");
        var credential = new ClientSecretCredential(tenantId, clientId, clientSecret);
        _client = new GraphServiceClient(credential, ["https://graph.microsoft.com/.default"]);
    }

    public GraphServiceClient Client => _client;
}
