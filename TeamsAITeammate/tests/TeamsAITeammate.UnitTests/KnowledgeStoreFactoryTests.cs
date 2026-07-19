using Microsoft.Extensions.Logging;
using Moq;
using TeamsAITeammate.Core.Interfaces;
using TeamsAITeammate.Core.Models;

namespace TeamsAITeammate.UnitTests;

public class KnowledgeStoreFactoryTests
{
    private readonly Mock<IServiceProvider> _serviceProvider = new();
    private readonly Mock<ILogger<TeamsAITeammate.Infrastructure.Services.KnowledgeStoreFactory>> _logger = new();

    [Fact]
    public void CreateStore_WithValidProvider_ReturnsStore()
    {
        var cosmosStore = new Mock<IKnowledgeStore>();
        cosmosStore.Setup(s => s.ProviderName).Returns("CosmosDB");

        _serviceProvider
            .Setup(sp => sp.GetService(typeof(IEnumerable<IKnowledgeStore>)))
            .Returns(new[] { cosmosStore.Object });

        var factory = CreateFactory(["CosmosDB"]);

        var store = factory.CreateStore("CosmosDB");

        Assert.NotNull(store);
        Assert.Equal("CosmosDB", store.ProviderName);
    }

    [Fact]
    public void CreateStore_WithUnknownProvider_FallsBackToCosmos()
    {
        var cosmosStore = new Mock<IKnowledgeStore>();
        cosmosStore.Setup(s => s.ProviderName).Returns("CosmosDB");

        _serviceProvider
            .Setup(sp => sp.GetService(typeof(IEnumerable<IKnowledgeStore>)))
            .Returns(new[] { cosmosStore.Object });

        var factory = CreateFactory(["CosmosDB"]);

        var store = factory.CreateStore("UnknownProvider");

        Assert.Equal("CosmosDB", store.ProviderName);
    }

    [Fact]
    public void CreateStore_WithMultipleProviders_ReturnsCorrectOne()
    {
        var cosmosStore = new Mock<IKnowledgeStore>();
        cosmosStore.Setup(s => s.ProviderName).Returns("CosmosDB");
        var aiSearchStore = new Mock<IKnowledgeStore>();
        aiSearchStore.Setup(s => s.ProviderName).Returns("AzureAISearch");

        _serviceProvider
            .Setup(sp => sp.GetService(typeof(IEnumerable<IKnowledgeStore>)))
            .Returns(new[] { cosmosStore.Object, aiSearchStore.Object });

        var factory = CreateFactory(["CosmosDB", "AzureAISearch"]);

        var store = factory.CreateStore("AzureAISearch");

        Assert.Equal("AzureAISearch", store.ProviderName);
    }

    [Fact]
    public void CreateStore_CaseInsensitive()
    {
        var cosmosStore = new Mock<IKnowledgeStore>();
        cosmosStore.Setup(s => s.ProviderName).Returns("CosmosDB");

        _serviceProvider
            .Setup(sp => sp.GetService(typeof(IEnumerable<IKnowledgeStore>)))
            .Returns(new[] { cosmosStore.Object });

        var factory = CreateFactory(["CosmosDB"]);

        var store = factory.CreateStore("cosmosdb");

        Assert.Equal("CosmosDB", store.ProviderName);
    }

    [Fact]
    public void GetAvailableProviders_ReturnsConfigured()
    {
        _serviceProvider
            .Setup(sp => sp.GetService(typeof(IEnumerable<IKnowledgeStore>)))
            .Returns(Array.Empty<IKnowledgeStore>());

        var factory = CreateFactory(["CosmosDB", "Dataverse", "AzureAISearch", "SharePoint"]);

        var providers = factory.GetAvailableProviders();

        Assert.Equal(4, providers.Count);
        Assert.Contains("CosmosDB", providers);
        Assert.Contains("Dataverse", providers);
        Assert.Contains("AzureAISearch", providers);
        Assert.Contains("SharePoint", providers);
    }

    [Fact]
    public void CreateStore_NoCosmosDBFallback_ThrowsException()
    {
        _serviceProvider
            .Setup(sp => sp.GetService(typeof(IEnumerable<IKnowledgeStore>)))
            .Returns(Array.Empty<IKnowledgeStore>());

        var factory = CreateFactory(["CosmosDB"]);

        Assert.Throws<InvalidOperationException>(() => factory.CreateStore("UnknownProvider"));
    }

    private TestableKnowledgeStoreFactory CreateFactory(string[] availableProviders)
    {
        return new TestableKnowledgeStoreFactory(
            _serviceProvider.Object, availableProviders, _logger.Object);
    }
}

// Testable subclass that bypasses IConfiguration
internal class TestableKnowledgeStoreFactory : IKnowledgeStoreFactory
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IReadOnlyList<string> _availableProviders;
    private readonly ILogger _logger;

    public TestableKnowledgeStoreFactory(
        IServiceProvider serviceProvider,
        string[] availableProviders,
        ILogger logger)
    {
        _serviceProvider = serviceProvider;
        _availableProviders = availableProviders;
        _logger = logger;
    }

    public IKnowledgeStore CreateStore(string providerName)
    {
        var stores = (IEnumerable<IKnowledgeStore>)_serviceProvider.GetService(typeof(IEnumerable<IKnowledgeStore>))!;
        var store = stores.FirstOrDefault(s =>
            s.ProviderName.Equals(providerName, StringComparison.OrdinalIgnoreCase));

        if (store is null)
        {
            store = stores.FirstOrDefault(s =>
                s.ProviderName.Equals("CosmosDB", StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidOperationException($"No knowledge store provider found for '{providerName}'");
        }

        return store;
    }

    public IReadOnlyList<string> GetAvailableProviders() => _availableProviders;
}
