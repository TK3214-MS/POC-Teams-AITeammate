using Microsoft.Extensions.Configuration;
using Moq;
using TeamsAITeammate.Core.Interfaces;
using TeamsAITeammate.Core.Models;

namespace TeamsAITeammate.UnitTests;

public class TenantAwareKnowledgeStoreResolverTests
{
    private readonly Mock<IKnowledgeStoreFactory> _factory = new();

    [Fact]
    public async Task ResolveAsync_NoTenantSetting_ReturnsDefaultProvider()
    {
        var cosmosStore = new Mock<IKnowledgeStore>();
        cosmosStore.Setup(s => s.ProviderName).Returns("CosmosDB");
        _factory.Setup(f => f.CreateStore("CosmosDB")).Returns(cosmosStore.Object);

        var resolver = CreateResolver("CosmosDB");

        var store = await resolver.ResolveAsync("tenant-1", CancellationToken.None);

        Assert.Equal("CosmosDB", store.ProviderName);
    }

    [Fact]
    public async Task ResolveAsync_WithTenantSetting_ReturnsConfiguredProvider()
    {
        var dvStore = new Mock<IKnowledgeStore>();
        dvStore.Setup(s => s.ProviderName).Returns("Dataverse");
        _factory.Setup(f => f.CreateStore("Dataverse")).Returns(dvStore.Object);

        var resolver = CreateResolver("CosmosDB");
        resolver.SetTenantProvider("tenant-1", "Dataverse");

        var store = await resolver.ResolveAsync("tenant-1", CancellationToken.None);

        Assert.Equal("Dataverse", store.ProviderName);
    }

    [Fact]
    public async Task ResolveAsync_DifferentTenants_DifferentProviders()
    {
        var cosmosStore = new Mock<IKnowledgeStore>();
        cosmosStore.Setup(s => s.ProviderName).Returns("CosmosDB");
        var spStore = new Mock<IKnowledgeStore>();
        spStore.Setup(s => s.ProviderName).Returns("SharePoint");

        _factory.Setup(f => f.CreateStore("CosmosDB")).Returns(cosmosStore.Object);
        _factory.Setup(f => f.CreateStore("SharePoint")).Returns(spStore.Object);

        var resolver = CreateResolver("CosmosDB");
        resolver.SetTenantProvider("tenant-2", "SharePoint");

        var store1 = await resolver.ResolveAsync("tenant-1", CancellationToken.None);
        var store2 = await resolver.ResolveAsync("tenant-2", CancellationToken.None);

        Assert.Equal("CosmosDB", store1.ProviderName);
        Assert.Equal("SharePoint", store2.ProviderName);
    }

    [Fact]
    public void SetTenantProvider_UpdatesExisting()
    {
        var resolver = CreateResolver("CosmosDB");

        resolver.SetTenantProvider("tenant-1", "Dataverse");
        Assert.Equal("Dataverse", resolver.GetTenantProvider("tenant-1"));

        resolver.SetTenantProvider("tenant-1", "AzureAISearch");
        Assert.Equal("AzureAISearch", resolver.GetTenantProvider("tenant-1"));
    }

    [Fact]
    public void GetTenantProvider_UnknownTenant_ReturnsDefault()
    {
        var resolver = CreateResolver("CosmosDB");

        var provider = resolver.GetTenantProvider("unknown-tenant");

        Assert.Equal("CosmosDB", provider);
    }

    private TeamsAITeammate.Infrastructure.Services.TenantAwareKnowledgeStoreResolver CreateResolver(string defaultProvider)
    {
        var config = new Mock<IConfiguration>();
        config.Setup(c => c["DataStore:DefaultProvider"]).Returns(defaultProvider);

        return new TeamsAITeammate.Infrastructure.Services.TenantAwareKnowledgeStoreResolver(
            _factory.Object,
            config.Object,
            new Mock<Microsoft.Extensions.Logging.ILogger<
                TeamsAITeammate.Infrastructure.Services.TenantAwareKnowledgeStoreResolver>>().Object);
    }
}
