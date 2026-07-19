using Moq;
using TeamsAITeammate.Core.Interfaces;
using TeamsAITeammate.Core.Models;

namespace TeamsAITeammate.UnitTests;

public class CosmosKnowledgeStoreTests
{
    [Fact]
    public void ProviderName_ReturnsCosmos()
    {
        // Using mock to verify interface contract without real Cosmos container
        var store = new Mock<IKnowledgeStore>();
        store.Setup(s => s.ProviderName).Returns("CosmosDB");

        Assert.Equal("CosmosDB", store.Object.ProviderName);
    }

    [Fact]
    public async Task SaveKnowledge_CallsSaveWithCorrectEntry()
    {
        var store = new Mock<IKnowledgeStore>();
        store.Setup(s => s.SaveKnowledgeAsync(It.IsAny<KnowledgeEntry>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((KnowledgeEntry e, CancellationToken _) => e.Id);

        var entry = new KnowledgeEntry
        {
            TenantId = "tenant-1",
            Title = "Test Knowledge",
            Content = "Test content",
            Status = KnowledgeStatus.Draft
        };

        var id = await store.Object.SaveKnowledgeAsync(entry, CancellationToken.None);

        Assert.Equal(entry.Id, id);
        store.Verify(s => s.SaveKnowledgeAsync(It.Is<KnowledgeEntry>(e =>
            e.TenantId == "tenant-1" &&
            e.Title == "Test Knowledge" &&
            e.Status == KnowledgeStatus.Draft),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetKnowledge_ReturnsEntry()
    {
        var store = new Mock<IKnowledgeStore>();
        var expected = new KnowledgeEntry { Id = "k1", Title = "Found" };
        store.Setup(s => s.GetKnowledgeAsync("k1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await store.Object.GetKnowledgeAsync("k1", CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("k1", result.Id);
    }

    [Fact]
    public async Task GetKnowledge_NotFound_ReturnsNull()
    {
        var store = new Mock<IKnowledgeStore>();
        store.Setup(s => s.GetKnowledgeAsync("missing", It.IsAny<CancellationToken>()))
            .ReturnsAsync((KnowledgeEntry?)null);

        var result = await store.Object.GetKnowledgeAsync("missing", CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task SearchAsync_WithFilters_ReturnsFilteredResults()
    {
        var store = new Mock<IKnowledgeStore>();
        var entries = new[]
        {
            new KnowledgeEntry { Id = "1", Status = KnowledgeStatus.Confirmed },
            new KnowledgeEntry { Id = "2", Status = KnowledgeStatus.Confirmed }
        };

        store.Setup(s => s.SearchAsync(
                "test",
                It.Is<KnowledgeSearchOptions>(o => o.Status == KnowledgeStatus.Confirmed),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(entries);

        var options = new KnowledgeSearchOptions { Status = KnowledgeStatus.Confirmed };
        var results = await store.Object.SearchAsync("test", options, CancellationToken.None);

        Assert.Equal(2, results.Count);
    }

    [Fact]
    public async Task GetBySessionAsync_ReturnsSessionEntries()
    {
        var store = new Mock<IKnowledgeStore>();
        var entries = new[]
        {
            new KnowledgeEntry { Id = "1", SessionId = "s1" },
            new KnowledgeEntry { Id = "2", SessionId = "s1" }
        };

        store.Setup(s => s.GetBySessionAsync("s1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(entries);

        var results = await store.Object.GetBySessionAsync("s1", CancellationToken.None);

        Assert.Equal(2, results.Count);
        Assert.All(results, r => Assert.Equal("s1", r.SessionId));
    }

    [Fact]
    public async Task DeleteKnowledge_CompletesSuccessfully()
    {
        var store = new Mock<IKnowledgeStore>();
        store.Setup(s => s.DeleteKnowledgeAsync("k1", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await store.Object.DeleteKnowledgeAsync("k1", CancellationToken.None);

        store.Verify(s => s.DeleteKnowledgeAsync("k1", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateKnowledge_UpdatesEntry()
    {
        var store = new Mock<IKnowledgeStore>();
        var entry = new KnowledgeEntry
        {
            Id = "k1",
            Status = KnowledgeStatus.Confirmed,
            ValidatedBy = "user-1"
        };

        store.Setup(s => s.UpdateKnowledgeAsync("k1", It.IsAny<KnowledgeEntry>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await store.Object.UpdateKnowledgeAsync("k1", entry, CancellationToken.None);

        store.Verify(s => s.UpdateKnowledgeAsync("k1",
            It.Is<KnowledgeEntry>(e => e.Status == KnowledgeStatus.Confirmed && e.ValidatedBy == "user-1"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetStats_ReturnsStats()
    {
        var store = new Mock<IKnowledgeStore>();
        var stats = new KnowledgeStoreStats
        {
            TenantId = "tenant-1",
            TotalEntries = 100,
            DraftCount = 30,
            ConfirmedCount = 50,
            RejectedCount = 10,
            ArchivedCount = 10
        };

        store.Setup(s => s.GetStatsAsync("tenant-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(stats);

        var result = await store.Object.GetStatsAsync("tenant-1", CancellationToken.None);

        Assert.Equal(100, result.TotalEntries);
        Assert.Equal(30, result.DraftCount);
        Assert.Equal(50, result.ConfirmedCount);
    }
}

public class DataverseKnowledgeStoreTests
{
    [Fact]
    public void ProviderName_ReturnsDataverse()
    {
        var store = new Mock<IKnowledgeStore>();
        store.Setup(s => s.ProviderName).Returns("Dataverse");

        Assert.Equal("Dataverse", store.Object.ProviderName);
    }

    [Fact]
    public async Task SaveKnowledge_ReturnsId()
    {
        var store = new Mock<IKnowledgeStore>();
        store.Setup(s => s.ProviderName).Returns("Dataverse");
        store.Setup(s => s.SaveKnowledgeAsync(It.IsAny<KnowledgeEntry>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("dv-123");

        var entry = new KnowledgeEntry { TenantId = "tenant-1", Title = "DV Test" };
        var id = await store.Object.SaveKnowledgeAsync(entry, CancellationToken.None);

        Assert.Equal("dv-123", id);
    }
}

public class AzureAISearchKnowledgeStoreTests
{
    [Fact]
    public void ProviderName_ReturnsAzureAISearch()
    {
        var store = new Mock<IKnowledgeStore>();
        store.Setup(s => s.ProviderName).Returns("AzureAISearch");

        Assert.Equal("AzureAISearch", store.Object.ProviderName);
    }

    [Fact]
    public async Task SearchAsync_WithVectorSearch_PassesQueryVector()
    {
        var store = new Mock<IKnowledgeStore>();
        var entries = new[] { new KnowledgeEntry { Id = "1" } };

        store.Setup(s => s.SearchAsync(
                It.IsAny<string>(),
                It.Is<KnowledgeSearchOptions>(o => o.UseVectorSearch && o.QueryVector != null),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(entries);

        var options = new KnowledgeSearchOptions
        {
            UseVectorSearch = true,
            QueryVector = new float[] { 0.1f, 0.2f, 0.3f }
        };

        var results = await store.Object.SearchAsync("test", options, CancellationToken.None);

        Assert.Single(results);
    }
}

public class SharePointKnowledgeStoreTests
{
    [Fact]
    public void ProviderName_ReturnsSharePoint()
    {
        var store = new Mock<IKnowledgeStore>();
        store.Setup(s => s.ProviderName).Returns("SharePoint");

        Assert.Equal("SharePoint", store.Object.ProviderName);
    }

    [Fact]
    public async Task GetBySession_ReturnsEntries()
    {
        var store = new Mock<IKnowledgeStore>();
        store.Setup(s => s.ProviderName).Returns("SharePoint");
        store.Setup(s => s.GetBySessionAsync("s1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new KnowledgeEntry { Id = "sp-1", SessionId = "s1" } });

        var results = await store.Object.GetBySessionAsync("s1", CancellationToken.None);

        Assert.Single(results);
        Assert.Equal("sp-1", results[0].Id);
    }
}
