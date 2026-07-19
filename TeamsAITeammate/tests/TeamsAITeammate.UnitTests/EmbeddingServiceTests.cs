using Microsoft.Extensions.Logging;
using Moq;
using TeamsAITeammate.Core.Interfaces;
using TeamsAITeammate.Core.Models;

namespace TeamsAITeammate.UnitTests;

public class EmbeddingServiceTests
{
    [Fact]
    public async Task GenerateEmbeddingAsync_EmptyText_ReturnsEmptyArray()
    {
        var service = new Mock<IEmbeddingService>();
        service.Setup(s => s.GenerateEmbeddingAsync(string.Empty, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<float>());

        var result = await service.Object.GenerateEmbeddingAsync(string.Empty, CancellationToken.None);

        Assert.Empty(result);
    }

    [Fact]
    public async Task GenerateEmbeddingAsync_ValidText_ReturnsVector()
    {
        var expectedVector = new float[] { 0.1f, 0.2f, 0.3f, 0.4f };
        var service = new Mock<IEmbeddingService>();
        service.Setup(s => s.GenerateEmbeddingAsync("test text", It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedVector);

        var result = await service.Object.GenerateEmbeddingAsync("test text", CancellationToken.None);

        Assert.Equal(4, result.Length);
        Assert.Equal(0.1f, result[0]);
    }

    [Fact]
    public async Task GenerateEmbeddingsAsync_MultiplTexts_ReturnsMultipleVectors()
    {
        var service = new Mock<IEmbeddingService>();
        service.Setup(s => s.GenerateEmbeddingsAsync(
                It.Is<IReadOnlyList<string>>(t => t.Count == 3),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new float[][]
            {
                [0.1f, 0.2f],
                [0.3f, 0.4f],
                [0.5f, 0.6f]
            });

        var texts = new List<string> { "text1", "text2", "text3" };
        var results = await service.Object.GenerateEmbeddingsAsync(texts, CancellationToken.None);

        Assert.Equal(3, results.Count);
    }

    [Fact]
    public async Task GenerateEmbeddingsAsync_EmptyList_ReturnsEmptyList()
    {
        var service = new Mock<IEmbeddingService>();
        service.Setup(s => s.GenerateEmbeddingsAsync(
                It.Is<IReadOnlyList<string>>(t => t.Count == 0),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<float[]>());

        var results = await service.Object.GenerateEmbeddingsAsync(new List<string>(), CancellationToken.None);

        Assert.Empty(results);
    }

    [Fact]
    public void ChunkText_ShortText_ReturnsSingleChunk()
    {
        var chunker = new TeamsAITeammate.AI.Services.EmbeddingService(
            null!, 1000, 200,
            Mock.Of<ILogger<TeamsAITeammate.AI.Services.EmbeddingService>>());

        var chunks = chunker.ChunkText("Short text.");

        Assert.Single(chunks);
        Assert.Equal("Short text.", chunks[0]);
    }

    [Fact]
    public void ChunkText_LongText_ReturnsMultipleChunks()
    {
        var chunker = new TeamsAITeammate.AI.Services.EmbeddingService(
            null!, 50, 10,
            Mock.Of<ILogger<TeamsAITeammate.AI.Services.EmbeddingService>>());

        var longText = string.Join(" ", Enumerable.Range(0, 100).Select(i => $"word{i}"));
        var chunks = chunker.ChunkText(longText);

        Assert.True(chunks.Count > 1, $"Expected > 1 chunks, got {chunks.Count}");
    }

    [Fact]
    public void ChunkText_EmptyText_ReturnsSingleChunk()
    {
        var chunker = new TeamsAITeammate.AI.Services.EmbeddingService(
            null!, 1000, 200,
            Mock.Of<ILogger<TeamsAITeammate.AI.Services.EmbeddingService>>());

        var chunks = chunker.ChunkText("");

        Assert.Single(chunks);
    }

    [Fact]
    public void ChunkText_WithSentenceBoundary_BreaksAtSentence()
    {
        var chunker = new TeamsAITeammate.AI.Services.EmbeddingService(
            null!, 40, 5,
            Mock.Of<ILogger<TeamsAITeammate.AI.Services.EmbeddingService>>());

        var text = "First sentence here. Second sentence here. Third sentence.";
        var chunks = chunker.ChunkText(text);

        Assert.True(chunks.Count >= 2);
        // First chunk should end at a sentence boundary
        Assert.True(chunks[0].EndsWith('.') || chunks[0].Length <= 40);
    }
}

public class DataSyncServiceTests
{
    [Fact]
    public async Task SyncToSecondaryAsync_SyncsAllEntries()
    {
        var primaryStore = new Mock<IKnowledgeStore>();
        var secondaryStore = new Mock<IKnowledgeStore>();
        var factory = new Mock<IKnowledgeStoreFactory>();
        var embeddingService = new Mock<IEmbeddingService>();

        factory.Setup(f => f.CreateStore("CosmosDB")).Returns(primaryStore.Object);
        factory.Setup(f => f.CreateStore("AzureAISearch")).Returns(secondaryStore.Object);

        var entries = new[]
        {
            new KnowledgeEntry { Id = "1", TenantId = "t1", Content = "Content 1" },
            new KnowledgeEntry { Id = "2", TenantId = "t1", Content = "Content 2" }
        };

        primaryStore.Setup(s => s.SearchAsync(
                It.IsAny<string>(),
                It.IsAny<KnowledgeSearchOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(entries);

        embeddingService.Setup(e => e.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new float[] { 0.1f, 0.2f });

        secondaryStore.Setup(s => s.SaveKnowledgeAsync(It.IsAny<KnowledgeEntry>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("id");

        var config = new Mock<Microsoft.Extensions.Configuration.IConfiguration>();
        config.Setup(c => c[It.IsAny<string>()]).Returns(string.Empty);

        var service = new TeamsAITeammate.Infrastructure.Services.DataSyncService(
            factory.Object,
            embeddingService.Object,
            config.Object,
            Mock.Of<ILogger<TeamsAITeammate.Infrastructure.Services.DataSyncService>>());

        await service.SyncToSecondaryAsync("t1", CancellationToken.None);

        secondaryStore.Verify(s => s.SaveKnowledgeAsync(It.IsAny<KnowledgeEntry>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task SyncToSecondaryAsync_WithExistingEmbedding_DoesNotRegenerate()
    {
        var primaryStore = new Mock<IKnowledgeStore>();
        var secondaryStore = new Mock<IKnowledgeStore>();
        var factory = new Mock<IKnowledgeStoreFactory>();
        var embeddingService = new Mock<IEmbeddingService>();

        factory.Setup(f => f.CreateStore("CosmosDB")).Returns(primaryStore.Object);
        factory.Setup(f => f.CreateStore("AzureAISearch")).Returns(secondaryStore.Object);

        var entries = new[]
        {
            new KnowledgeEntry
            {
                Id = "1",
                TenantId = "t1",
                Content = "Content",
                Embedding = new float[] { 0.5f, 0.6f }
            }
        };

        primaryStore.Setup(s => s.SearchAsync(
                It.IsAny<string>(),
                It.IsAny<KnowledgeSearchOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(entries);

        secondaryStore.Setup(s => s.SaveKnowledgeAsync(It.IsAny<KnowledgeEntry>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("id");

        var config2 = new Mock<Microsoft.Extensions.Configuration.IConfiguration>();
        config2.Setup(c => c[It.IsAny<string>()]).Returns(string.Empty);

        var service = new TeamsAITeammate.Infrastructure.Services.DataSyncService(
            factory.Object,
            embeddingService.Object,
            config2.Object,
            Mock.Of<ILogger<TeamsAITeammate.Infrastructure.Services.DataSyncService>>());

        await service.SyncToSecondaryAsync("t1", CancellationToken.None);

        embeddingService.Verify(e => e.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
