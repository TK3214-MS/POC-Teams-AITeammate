using Microsoft.Extensions.Logging;
using Moq;
using TeamsAITeammate.Core.Interfaces;
using TeamsAITeammate.Core.Models;
using TeamsAITeammate.Infrastructure.Services;

namespace TeamsAITeammate.UnitTests;

public class KnowledgeQualityServiceTests
{
    private readonly Mock<IKnowledgeStoreFactory> _mockStoreFactory;
    private readonly Mock<IKnowledgeStore> _mockCosmosStore;
    private readonly Mock<IKnowledgeStore> _mockSearchStore;
    private readonly Mock<IEmbeddingService> _mockEmbeddingService;
    private readonly KnowledgeQualityService _service;

    public KnowledgeQualityServiceTests()
    {
        _mockStoreFactory = new Mock<IKnowledgeStoreFactory>();
        _mockCosmosStore = new Mock<IKnowledgeStore>();
        _mockSearchStore = new Mock<IKnowledgeStore>();
        _mockEmbeddingService = new Mock<IEmbeddingService>();
        var mockLogger = new Mock<ILogger<KnowledgeQualityService>>();

        _mockStoreFactory.Setup(f => f.CreateStore("CosmosDB")).Returns(_mockCosmosStore.Object);
        _mockStoreFactory.Setup(f => f.CreateStore("AzureAISearch")).Returns(_mockSearchStore.Object);

        _service = new KnowledgeQualityService(
            _mockStoreFactory.Object,
            _mockEmbeddingService.Object,
            mockLogger.Object);
    }

    [Fact]
    public async Task DetectStaleKnowledgeAsync_NoEntries_ReturnsEmpty()
    {
        _mockCosmosStore
            .Setup(s => s.SearchAsync(
                It.IsAny<string>(),
                It.IsAny<KnowledgeSearchOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<KnowledgeEntry>());

        var result = await _service.DetectStaleKnowledgeAsync(
            "tenant-1", TimeSpan.FromDays(90), CancellationToken.None);

        Assert.Empty(result);
    }

    [Fact]
    public async Task DetectStaleKnowledgeAsync_AllRecent_ReturnsEmpty()
    {
        var entries = new List<KnowledgeEntry>
        {
            new()
            {
                Id = "k1",
                TenantId = "tenant-1",
                CreatedAt = DateTimeOffset.UtcNow.AddDays(-10)
            }
        };

        _mockCosmosStore
            .Setup(s => s.SearchAsync(
                It.IsAny<string>(),
                It.IsAny<KnowledgeSearchOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(entries);

        var result = await _service.DetectStaleKnowledgeAsync(
            "tenant-1", TimeSpan.FromDays(90), CancellationToken.None);

        Assert.Empty(result);
    }

    [Fact]
    public async Task DetectStaleKnowledgeAsync_StaleEntries_ReturnsThem()
    {
        var entries = new List<KnowledgeEntry>
        {
            new()
            {
                Id = "k1",
                TenantId = "tenant-1",
                CreatedAt = DateTimeOffset.UtcNow.AddDays(-100)
            },
            new()
            {
                Id = "k2",
                TenantId = "tenant-1",
                CreatedAt = DateTimeOffset.UtcNow.AddDays(-10)
            }
        };

        _mockCosmosStore
            .Setup(s => s.SearchAsync(
                It.IsAny<string>(),
                It.IsAny<KnowledgeSearchOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(entries);

        var result = await _service.DetectStaleKnowledgeAsync(
            "tenant-1", TimeSpan.FromDays(90), CancellationToken.None);

        Assert.Single(result);
        Assert.Equal("k1", result[0].Id);
    }

    [Fact]
    public async Task DetectStaleKnowledgeAsync_UpdatedEntryNotStale_Excluded()
    {
        var entries = new List<KnowledgeEntry>
        {
            new()
            {
                Id = "k1",
                TenantId = "tenant-1",
                CreatedAt = DateTimeOffset.UtcNow.AddDays(-200),
                UpdatedAt = DateTimeOffset.UtcNow.AddDays(-5)
            }
        };

        _mockCosmosStore
            .Setup(s => s.SearchAsync(
                It.IsAny<string>(),
                It.IsAny<KnowledgeSearchOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(entries);

        var result = await _service.DetectStaleKnowledgeAsync(
            "tenant-1", TimeSpan.FromDays(90), CancellationToken.None);

        Assert.Empty(result);
    }

    [Fact]
    public async Task DetectStaleKnowledgeAsync_SearchesConfirmedOnly()
    {
        _mockCosmosStore
            .Setup(s => s.SearchAsync(
                It.IsAny<string>(),
                It.IsAny<KnowledgeSearchOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<KnowledgeEntry>());

        await _service.DetectStaleKnowledgeAsync(
            "tenant-1", TimeSpan.FromDays(90), CancellationToken.None);

        _mockCosmosStore.Verify(s => s.SearchAsync(
            It.IsAny<string>(),
            It.Is<KnowledgeSearchOptions>(o =>
                o.TenantId == "tenant-1" && o.Status == KnowledgeStatus.Confirmed),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DetectConflictsAsync_SameId_Excluded()
    {
        var newEntry = new KnowledgeEntry
        {
            Id = "k1",
            TenantId = "tenant-1",
            Title = "Test",
            Content = "Content",
            Category = TacitKnowledgeCategory.ExpertKnowledge
        };

        _mockEmbeddingService
            .Setup(e => e.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new float[] { 1f, 0f, 0f });

        _mockSearchStore
            .Setup(s => s.SearchAsync(
                It.IsAny<string>(),
                It.IsAny<KnowledgeSearchOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<KnowledgeEntry> { newEntry });

        var result = await _service.DetectConflictsAsync(newEntry, CancellationToken.None);

        Assert.Empty(result);
    }

    [Fact]
    public async Task DetectConflictsAsync_DifferentCategory_NoConflict()
    {
        var newEntry = new KnowledgeEntry
        {
            Id = "k1",
            TenantId = "tenant-1",
            Title = "Test",
            Content = "Content",
            Category = TacitKnowledgeCategory.ExpertKnowledge,
            Embedding = [1f, 0f, 0f]
        };

        var existing = new KnowledgeEntry
        {
            Id = "k2",
            TenantId = "tenant-1",
            Category = TacitKnowledgeCategory.LessonsLearned,
            Embedding = [1f, 0f, 0f]
        };

        _mockEmbeddingService
            .Setup(e => e.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new float[] { 1f, 0f, 0f });

        _mockSearchStore
            .Setup(s => s.SearchAsync(
                It.IsAny<string>(),
                It.IsAny<KnowledgeSearchOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<KnowledgeEntry> { existing });

        var result = await _service.DetectConflictsAsync(newEntry, CancellationToken.None);

        Assert.Empty(result);
    }

    [Fact]
    public async Task DetectConflictsAsync_EmptyContent_SkipsEmbedding()
    {
        var newEntry = new KnowledgeEntry
        {
            Id = "k1",
            TenantId = "tenant-1",
            Title = "Test",
            Content = "",
            Category = TacitKnowledgeCategory.ExpertKnowledge
        };

        _mockSearchStore
            .Setup(s => s.SearchAsync(
                It.IsAny<string>(),
                It.IsAny<KnowledgeSearchOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<KnowledgeEntry>());

        await _service.DetectConflictsAsync(newEntry, CancellationToken.None);

        _mockEmbeddingService.Verify(
            e => e.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task SuggestMergesAsync_NoEntries_ReturnsEmpty()
    {
        _mockCosmosStore
            .Setup(s => s.SearchAsync(
                It.IsAny<string>(),
                It.IsAny<KnowledgeSearchOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<KnowledgeEntry>());

        var result = await _service.SuggestMergesAsync("tenant-1", CancellationToken.None);

        Assert.Empty(result);
    }

    [Fact]
    public async Task SuggestMergesAsync_HighSimilarity_SuggestsMerge()
    {
        // Two entries in the same category with near-identical embeddings
        var identicalEmbedding = new float[] { 1f, 0f, 0f };
        var entries = new List<KnowledgeEntry>
        {
            new()
            {
                Id = "k1",
                Category = TacitKnowledgeCategory.ExpertKnowledge,
                Embedding = identicalEmbedding
            },
            new()
            {
                Id = "k2",
                Category = TacitKnowledgeCategory.ExpertKnowledge,
                Embedding = identicalEmbedding
            }
        };

        _mockCosmosStore
            .Setup(s => s.SearchAsync(
                It.IsAny<string>(),
                It.IsAny<KnowledgeSearchOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(entries);

        var result = await _service.SuggestMergesAsync("tenant-1", CancellationToken.None);

        Assert.Single(result);
        Assert.Equal("k1", result[0].Source.Id);
        Assert.Equal("k2", result[0].Target.Id);
    }

    [Fact]
    public async Task SuggestMergesAsync_LowSimilarity_NoSuggestion()
    {
        var entries = new List<KnowledgeEntry>
        {
            new()
            {
                Id = "k1",
                Category = TacitKnowledgeCategory.ExpertKnowledge,
                Embedding = [1f, 0f, 0f]
            },
            new()
            {
                Id = "k2",
                Category = TacitKnowledgeCategory.ExpertKnowledge,
                Embedding = [0f, 1f, 0f] // Orthogonal — cosine similarity = 0
            }
        };

        _mockCosmosStore
            .Setup(s => s.SearchAsync(
                It.IsAny<string>(),
                It.IsAny<KnowledgeSearchOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(entries);

        var result = await _service.SuggestMergesAsync("tenant-1", CancellationToken.None);

        Assert.Empty(result);
    }

    [Fact]
    public void CosineSimilarity_IdenticalVectors_ReturnsOne()
    {
        var a = new float[] { 1f, 0f, 0f };
        var similarity = KnowledgeQualityService.CosineSimilarity(a, a);
        Assert.Equal(1f, similarity, precision: 5);
    }

    [Fact]
    public void CosineSimilarity_OrthogonalVectors_ReturnsZero()
    {
        var a = new float[] { 1f, 0f, 0f };
        var b = new float[] { 0f, 1f, 0f };
        var similarity = KnowledgeQualityService.CosineSimilarity(a, b);
        Assert.Equal(0f, similarity, precision: 5);
    }

    [Fact]
    public void CosineSimilarity_EmptyVectors_ReturnsZero()
    {
        var similarity = KnowledgeQualityService.CosineSimilarity([], []);
        Assert.Equal(0f, similarity);
    }

    [Fact]
    public void CosineSimilarity_DifferentLengths_ReturnsZero()
    {
        var a = new float[] { 1f, 0f };
        var b = new float[] { 1f, 0f, 0f };
        var similarity = KnowledgeQualityService.CosineSimilarity(a, b);
        Assert.Equal(0f, similarity);
    }

    [Fact]
    public void CosineSimilarity_ZeroVector_ReturnsZero()
    {
        var a = new float[] { 0f, 0f, 0f };
        var b = new float[] { 1f, 0f, 0f };
        var similarity = KnowledgeQualityService.CosineSimilarity(a, b);
        Assert.Equal(0f, similarity);
    }

    [Fact]
    public void KnowledgeConflict_CanBeCreated()
    {
        var conflict = new KnowledgeConflict
        {
            Existing = new KnowledgeEntry { Title = "Old" },
            New = new KnowledgeEntry { Title = "New" },
            ConflictDescription = "Contradicts",
            SimilarityScore = 0.9f
        };

        Assert.Equal("Old", conflict.Existing.Title);
        Assert.Equal("New", conflict.New.Title);
        Assert.Equal(0.9f, conflict.SimilarityScore);
    }

    [Fact]
    public void MergeSuggestion_CanBeCreated()
    {
        var suggestion = new MergeSuggestion
        {
            Source = new KnowledgeEntry { Id = "s" },
            Target = new KnowledgeEntry { Id = "t" },
            MergeRationale = "Very similar",
            SimilarityScore = 0.95f
        };

        Assert.Equal("s", suggestion.Source.Id);
        Assert.Equal("t", suggestion.Target.Id);
        Assert.Equal(0.95f, suggestion.SimilarityScore);
    }
}
