using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TeamsAITeammate.Core.Interfaces;

namespace TeamsAITeammate.Infrastructure.Services;

public class TenantAwareKnowledgeStoreResolver
{
    private readonly IKnowledgeStoreFactory _factory;
    private readonly string _defaultProvider;
    private readonly ILogger<TenantAwareKnowledgeStoreResolver> _logger;

    // In-memory tenant preferences — production would use a tenant settings store
    private readonly Dictionary<string, string> _tenantProviders = new();

    public TenantAwareKnowledgeStoreResolver(
        IKnowledgeStoreFactory factory,
        IConfiguration configuration,
        ILogger<TenantAwareKnowledgeStoreResolver> logger)
    {
        _factory = factory;
        _defaultProvider = configuration["DataStore:DefaultProvider"] ?? "CosmosDB";
        _logger = logger;
    }

    public Task<IKnowledgeStore> ResolveAsync(string tenantId, CancellationToken ct)
    {
        var providerName = _tenantProviders.GetValueOrDefault(tenantId, _defaultProvider);
        _logger.LogDebug("Resolved knowledge store for tenant {TenantId}: {Provider}", tenantId, providerName);
        return Task.FromResult(_factory.CreateStore(providerName));
    }

    public void SetTenantProvider(string tenantId, string providerName)
    {
        _tenantProviders[tenantId] = providerName;
        _logger.LogInformation("Set tenant {TenantId} knowledge store to {Provider}", tenantId, providerName);
    }

    public string GetTenantProvider(string tenantId)
    {
        return _tenantProviders.GetValueOrDefault(tenantId, _defaultProvider);
    }
}
