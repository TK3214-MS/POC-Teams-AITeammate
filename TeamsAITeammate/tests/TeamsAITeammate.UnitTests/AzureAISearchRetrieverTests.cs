using Azure.Search.Documents;
using Azure.Search.Documents.Models;
using Microsoft.Extensions.Logging;
using Moq;
using TeamsAITeammate.Core.Interfaces;
using TeamsAITeammate.Core.Models;
using TeamsAITeammate.Infrastructure.Services;

namespace TeamsAITeammate.UnitTests;

public class AzureAISearchRetrieverTests
{
    private readonly Mock<SearchClient> _mockSearchClient;
    private readonly Mock<IEmbeddingService> _mockEmbeddingService;
    private readonly AzureAISearchRetriever _retriever;

    public AzureAISearchRetrieverTests()
    {
        _mockSearchClient = new Mock<SearchClient>();
        _mockEmbeddingService = new Mock<IEmbeddingService>();
        var mockLogger = new Mock<ILogger<AzureAISearchRetriever>>();

        _retriever = new AzureAISearchRetriever(
            _mockSearchClient.Object,
            _mockEmbeddingService.Object,
            mockLogger.Object);
    }

    [Fact]
    public async Task RetrieveAsync_EmptyQuery_ReturnsEmpty()
    {
        var query = new RetrievalQuery { QueryText = "" };
        var result = await _retriever.RetrieveAsync(query, CancellationToken.None);
        Assert.Empty(result);
    }

    [Fact]
    public async Task RetrieveAsync_WhitespaceQuery_ReturnsEmpty()
    {
        var query = new RetrievalQuery { QueryText = "   " };
        var result = await _retriever.RetrieveAsync(query, CancellationToken.None);
        Assert.Empty(result);
    }

    [Fact]
    public async Task RetrieveAsync_HybridSearch_GeneratesEmbedding()
    {
        var embedding = new float[] { 0.1f, 0.2f, 0.3f };
        _mockEmbeddingService
            .Setup(e => e.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(embedding);

        SetupEmptySearchResponse();

        var query = new RetrievalQuery
        {
            QueryText = "test query",
            Strategy = RetrievalStrategy.HybridSearch
        };

        await _retriever.RetrieveAsync(query, CancellationToken.None);

        _mockEmbeddingService.Verify(
            e => e.GenerateEmbeddingAsync("test query", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RetrieveAsync_VectorOnly_GeneratesEmbedding()
    {
        var embedding = new float[] { 0.1f, 0.2f, 0.3f };
        _mockEmbeddingService
            .Setup(e => e.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(embedding);

        SetupEmptySearchResponse();

        var query = new RetrievalQuery
        {
            QueryText = "test",
            Strategy = RetrievalStrategy.VectorOnly
        };

        await _retriever.RetrieveAsync(query, CancellationToken.None);

        _mockEmbeddingService.Verify(
            e => e.GenerateEmbeddingAsync("test", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RetrieveAsync_KeywordOnly_DoesNotGenerateEmbedding()
    {
        SetupEmptySearchResponse();

        var query = new RetrievalQuery
        {
            QueryText = "test",
            Strategy = RetrievalStrategy.KeywordOnly
        };

        await _retriever.RetrieveAsync(query, CancellationToken.None);

        _mockEmbeddingService.Verify(
            e => e.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public void RetrievalQuery_DefaultValues_AreCorrect()
    {
        var query = new RetrievalQuery();

        Assert.Equal(RetrievalStrategy.HybridSearch, query.Strategy);
        Assert.Equal(10, query.MaxResults);
        Assert.Equal(0.7f, query.MinRelevanceScore);
        Assert.Null(query.CategoryFilter);
        Assert.Null(query.DateFrom);
        Assert.Null(query.DateTo);
        Assert.Null(query.TagFilter);
    }

    [Fact]
    public void RetrievalResult_CanBeCreated()
    {
        var result = new RetrievalResult
        {
            Entry = new KnowledgeEntry { Title = "Test" },
            RelevanceScore = 0.9f,
            MatchHighlight = "highlighted text",
            Source = RetrievalSource.HybridSearch
        };

        Assert.Equal("Test", result.Entry.Title);
        Assert.Equal(0.9f, result.RelevanceScore);
        Assert.Equal(RetrievalSource.HybridSearch, result.Source);
    }

    [Fact]
    public void RetrievalStrategy_HasExpectedValues()
    {
        Assert.Equal(4, Enum.GetValues<RetrievalStrategy>().Length);
        Assert.True(Enum.IsDefined(RetrievalStrategy.HybridSearch));
        Assert.True(Enum.IsDefined(RetrievalStrategy.VectorOnly));
        Assert.True(Enum.IsDefined(RetrievalStrategy.KeywordOnly));
        Assert.True(Enum.IsDefined(RetrievalStrategy.SemanticRanking));
    }

    [Fact]
    public void RetrievalSource_HasExpectedValues()
    {
        Assert.Equal(4, Enum.GetValues<RetrievalSource>().Length);
        Assert.True(Enum.IsDefined(RetrievalSource.VectorSearch));
        Assert.True(Enum.IsDefined(RetrievalSource.KeywordSearch));
        Assert.True(Enum.IsDefined(RetrievalSource.HybridSearch));
        Assert.True(Enum.IsDefined(RetrievalSource.SemanticRanking));
    }

    [Fact]
    public void RetrievalQuery_WithFilters_ArePreserved()
    {
        var query = new RetrievalQuery
        {
            QueryText = "search term",
            TenantId = "tenant-1",
            Strategy = RetrievalStrategy.SemanticRanking,
            MaxResults = 20,
            MinRelevanceScore = 0.8f,
            CategoryFilter = [TacitKnowledgeCategory.ExpertKnowledge],
            DateFrom = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            DateTo = new DateTimeOffset(2026, 6, 30, 0, 0, 0, TimeSpan.Zero),
            TagFilter = ["tag1", "tag2"]
        };

        Assert.Equal("search term", query.QueryText);
        Assert.Equal("tenant-1", query.TenantId);
        Assert.Equal(RetrievalStrategy.SemanticRanking, query.Strategy);
        Assert.Equal(20, query.MaxResults);
        Assert.Equal(0.8f, query.MinRelevanceScore);
        Assert.Single(query.CategoryFilter!);
        Assert.Equal(TacitKnowledgeCategory.ExpertKnowledge, query.CategoryFilter![0]);
        Assert.Equal(2, query.TagFilter!.Count);
    }

    private void SetupEmptySearchResponse()
    {
        var mockResponse = new Mock<Azure.Response<SearchResults<SearchDocument>>>();
        var searchResults = SearchModelFactory.SearchResults<SearchDocument>(
            Array.Empty<SearchResult<SearchDocument>>(), 0, null, null, null);
        mockResponse.Setup(r => r.Value).Returns(searchResults);

        _mockSearchClient
            .Setup(c => c.SearchAsync<SearchDocument>(
                It.IsAny<string>(),
                It.IsAny<SearchOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockResponse.Object);
    }
}
