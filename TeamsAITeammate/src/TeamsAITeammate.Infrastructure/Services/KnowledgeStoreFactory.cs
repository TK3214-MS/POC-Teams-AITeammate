using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TeamsAITeammate.Core.Interfaces;

namespace TeamsAITeammate.Infrastructure.Services;

public class KnowledgeStoreFactory : IKnowledgeStoreFactory
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IReadOnlyList<string> _availableProviders;
    private readonly ILogger<KnowledgeStoreFactory> _logger;

    public KnowledgeStoreFactory(
        IServiceProvider serviceProvider,
        IConfiguration configuration,
        ILogger<KnowledgeStoreFactory> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        var section = configuration.GetSection("DataStore:AvailableProviders");
        var providers = new List<string>();
        foreach (var child in section.GetChildren())
        {
            if (child.Value is not null)
                providers.Add(child.Value);
        }
        _availableProviders = providers.Count > 0 ? providers : ["CosmosDB"];
    }

    public IKnowledgeStore CreateStore(string providerName)
    {
        var stores = _serviceProvider.GetServices<IKnowledgeStore>();
        var store = stores.FirstOrDefault(s =>
            s.ProviderName.Equals(providerName, StringComparison.OrdinalIgnoreCase));

        if (store is null)
        {
            _logger.LogWarning("Provider {ProviderName} not found, falling back to CosmosDB", providerName);
            store = stores.FirstOrDefault(s =>
                s.ProviderName.Equals("CosmosDB", StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidOperationException($"No knowledge store provider found for '{providerName}'");
        }

        return store;
    }

    public IReadOnlyList<string> GetAvailableProviders() => _availableProviders;
}
