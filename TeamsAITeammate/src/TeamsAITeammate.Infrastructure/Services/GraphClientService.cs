using Azure.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Graph;

namespace TeamsAITeammate.Infrastructure.Services;

public class GraphClientService
{
    private readonly GraphServiceClient _client;

    public GraphClientService(IConfiguration configuration)
    {
        var credential = new DefaultAzureCredential();
        _client = new GraphServiceClient(credential);
    }

    public GraphServiceClient Client => _client;
}
