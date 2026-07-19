using Microsoft.Extensions.Logging;
using Moq;
using TeamsAITeammate.Core.Interfaces;
using TeamsAITeammate.Core.Models;
using TeamsAITeammate.Infrastructure.Services;

namespace TeamsAITeammate.UnitTests;

public class KnowledgeGraphServiceTests
{
    private readonly Mock<IKnowledgeStoreFactory> _mockStoreFactory;
    private readonly Mock<IKnowledgeStore> _mockStore;
    private readonly Mock<IEmbeddingService> _mockEmbeddingService;
    private readonly KnowledgeGraphService _service;

    public KnowledgeGraphServiceTests()
    {
        _mockStoreFactory = new Mock<IKnowledgeStoreFactory>();
        _mockStore = new Mock<IKnowledgeStore>();
        _mockEmbeddingService = new Mock<IEmbeddingService>();
        var mockLogger = new Mock<ILogger<KnowledgeGraphService>>();

        _mockStoreFactory.Setup(f => f.CreateStore("CosmosDB")).Returns(_mockStore.Object);

        _service = new KnowledgeGraphService(
            _mockStoreFactory.Object,
            _mockEmbeddingService.Object,
            mockLogger.Object);
    }

    [Fact]
    public async Task AddRelationAsync_NewRelation_Succeeds()
    {
        await _service.AddRelationAsync("s1", "t1", RelationType.RelatedTo, CancellationToken.None);

        // Verify by getting related
        _mockStore
            .Setup(s => s.GetKnowledgeAsync("t1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new KnowledgeEntry { Id = "t1" });

        var related = await _service.GetRelatedAsync("s1", 1, CancellationToken.None);
        Assert.Single(related);
        Assert.Equal("t1", related[0].Id);
    }

    [Fact]
    public async Task AddRelationAsync_DuplicateRelation_NotAdded()
    {
        await _service.AddRelationAsync("s1", "t1", RelationType.RelatedTo, CancellationToken.None);
        await _service.AddRelationAsync("s1", "t1", RelationType.RelatedTo, CancellationToken.None);

        _mockStore
            .Setup(s => s.GetKnowledgeAsync("t1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new KnowledgeEntry { Id = "t1" });

        var related = await _service.GetRelatedAsync("s1", 1, CancellationToken.None);
        Assert.Single(related); // Not duplicated
    }

    [Fact]
    public async Task AddRelationAsync_SameNodesDifferentType_BothAdded()
    {
        await _service.AddRelationAsync("s1", "t1", RelationType.RelatedTo, CancellationToken.None);
        await _service.AddRelationAsync("s1", "t1", RelationType.Contradicts, CancellationToken.None);

        _mockStore
            .Setup(s => s.GetKnowledgeAsync("t1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new KnowledgeEntry { Id = "t1" });

        // Both relations point to t1, but t1 should only appear once (same neighbor)
        var related = await _service.GetRelatedAsync("s1", 1, CancellationToken.None);
        Assert.Single(related);
    }

    [Fact]
    public async Task GetRelatedAsync_ZeroDepth_ReturnsEmpty()
    {
        await _service.AddRelationAsync("s1", "t1", RelationType.RelatedTo, CancellationToken.None);

        var related = await _service.GetRelatedAsync("s1", 0, CancellationToken.None);
        Assert.Empty(related);
    }

    [Fact]
    public async Task GetRelatedAsync_NoRelations_ReturnsEmpty()
    {
        var related = await _service.GetRelatedAsync("orphan", 1, CancellationToken.None);
        Assert.Empty(related);
    }

    [Fact]
    public async Task GetRelatedAsync_TraversesBidirectional()
    {
        await _service.AddRelationAsync("a", "b", RelationType.Supports, CancellationToken.None);

        _mockStore
            .Setup(s => s.GetKnowledgeAsync("a", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new KnowledgeEntry { Id = "a" });

        // Traverse from b should find a (bidirectional)
        var related = await _service.GetRelatedAsync("b", 1, CancellationToken.None);
        Assert.Single(related);
        Assert.Equal("a", related[0].Id);
    }

    [Fact]
    public async Task GetRelatedAsync_Depth2_TraversesMultipleLevels()
    {
        // a -> b -> c
        await _service.AddRelationAsync("a", "b", RelationType.DerivedFrom, CancellationToken.None);
        await _service.AddRelationAsync("b", "c", RelationType.DerivedFrom, CancellationToken.None);

        _mockStore
            .Setup(s => s.GetKnowledgeAsync("b", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new KnowledgeEntry { Id = "b" });
        _mockStore
            .Setup(s => s.GetKnowledgeAsync("c", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new KnowledgeEntry { Id = "c" });

        var related = await _service.GetRelatedAsync("a", 2, CancellationToken.None);
        Assert.Equal(2, related.Count);
        Assert.Contains(related, e => e.Id == "b");
        Assert.Contains(related, e => e.Id == "c");
    }

    [Fact]
    public async Task GetRelatedAsync_Depth1_DoesNotTraverseDeeper()
    {
        // a -> b -> c
        await _service.AddRelationAsync("a", "b", RelationType.DerivedFrom, CancellationToken.None);
        await _service.AddRelationAsync("b", "c", RelationType.DerivedFrom, CancellationToken.None);

        _mockStore
            .Setup(s => s.GetKnowledgeAsync("b", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new KnowledgeEntry { Id = "b" });

        var related = await _service.GetRelatedAsync("a", 1, CancellationToken.None);
        Assert.Single(related);
        Assert.Equal("b", related[0].Id);
    }

    [Fact]
    public async Task GetRelatedAsync_NullEntry_SkipsNode()
    {
        await _service.AddRelationAsync("a", "missing", RelationType.RelatedTo, CancellationToken.None);

        _mockStore
            .Setup(s => s.GetKnowledgeAsync("missing", It.IsAny<CancellationToken>()))
            .ReturnsAsync((KnowledgeEntry?)null);

        var related = await _service.GetRelatedAsync("a", 1, CancellationToken.None);
        Assert.Empty(related);
    }

    [Fact]
    public async Task DetectClustersAsync_NoEntries_ReturnsEmpty()
    {
        _mockStore
            .Setup(s => s.SearchAsync(
                It.IsAny<string>(),
                It.IsAny<KnowledgeSearchOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<KnowledgeEntry>());

        var clusters = await _service.DetectClustersAsync("tenant-1", CancellationToken.None);
        Assert.Empty(clusters);
    }

    [Fact]
    public async Task DetectClustersAsync_SingleEntryPerTopic_NoClusters()
    {
        var entries = new List<KnowledgeEntry>
        {
            new() { Id = "k1", RelatedTopics = ["topic-a"] },
            new() { Id = "k2", RelatedTopics = ["topic-b"] }
        };

        _mockStore
            .Setup(s => s.SearchAsync(
                It.IsAny<string>(),
                It.IsAny<KnowledgeSearchOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(entries);

        var clusters = await _service.DetectClustersAsync("tenant-1", CancellationToken.None);
        Assert.Empty(clusters);
    }

    [Fact]
    public async Task DetectClustersAsync_MultipleEntriesPerTopic_CreatesClusters()
    {
        var entries = new List<KnowledgeEntry>
        {
            new() { Id = "k1", RelatedTopics = ["architecture"] },
            new() { Id = "k2", RelatedTopics = ["architecture"] },
            new() { Id = "k3", RelatedTopics = ["testing"] }
        };

        _mockStore
            .Setup(s => s.SearchAsync(
                It.IsAny<string>(),
                It.IsAny<KnowledgeSearchOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(entries);

        var clusters = await _service.DetectClustersAsync("tenant-1", CancellationToken.None);

        Assert.Single(clusters);
        Assert.Equal("architecture", clusters[0].Topic);
        Assert.Equal(2, clusters[0].Entries.Count);
    }

    [Fact]
    public async Task DetectClustersAsync_LargeCluster_HigherCohesion()
    {
        var entries = new List<KnowledgeEntry>
        {
            new() { Id = "k1", RelatedTopics = ["design"] },
            new() { Id = "k2", RelatedTopics = ["design"] },
            new() { Id = "k3", RelatedTopics = ["design"] }
        };

        _mockStore
            .Setup(s => s.SearchAsync(
                It.IsAny<string>(),
                It.IsAny<KnowledgeSearchOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(entries);

        var clusters = await _service.DetectClustersAsync("tenant-1", CancellationToken.None);

        Assert.Single(clusters);
        Assert.Equal(0.8f, clusters[0].Cohesion);
    }

    [Fact]
    public void RelationType_HasExpectedValues()
    {
        Assert.Equal(6, Enum.GetValues<RelationType>().Length);
        Assert.True(Enum.IsDefined(RelationType.RelatedTo));
        Assert.True(Enum.IsDefined(RelationType.DerivedFrom));
        Assert.True(Enum.IsDefined(RelationType.Contradicts));
        Assert.True(Enum.IsDefined(RelationType.Supersedes));
        Assert.True(Enum.IsDefined(RelationType.Supports));
        Assert.True(Enum.IsDefined(RelationType.DependsOn));
    }

    [Fact]
    public void KnowledgeCluster_CanBeCreated()
    {
        var cluster = new KnowledgeCluster
        {
            Topic = "AI",
            Entries =
            [
                new KnowledgeEntry { Id = "k1" },
                new KnowledgeEntry { Id = "k2" }
            ],
            Cohesion = 0.75f
        };

        Assert.Equal("AI", cluster.Topic);
        Assert.Equal(2, cluster.Entries.Count);
        Assert.Equal(0.75f, cluster.Cohesion);
    }

    [Fact]
    public void KnowledgeRelation_DefaultValues()
    {
        var relation = new KnowledgeRelation
        {
            SourceId = "s",
            TargetId = "t",
            Type = RelationType.Supports
        };

        Assert.Equal("s", relation.SourceId);
        Assert.Equal("t", relation.TargetId);
        Assert.Equal(RelationType.Supports, relation.Type);
        Assert.True(relation.CreatedAt <= DateTimeOffset.UtcNow);
    }
}
