using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using System.Security.Claims;
using TeamsAITeammate.Agent.Controllers;
using TeamsAITeammate.Core.Interfaces;
using TeamsAITeammate.Core.Models;

namespace TeamsAITeammate.UnitTests;

public class CopilotIntegrationControllerTests
{
    private readonly Mock<IKnowledgeRetriever> _mockRetriever;
    private readonly Mock<IKnowledgeStoreFactory> _mockStoreFactory;
    private readonly CopilotIntegrationController _controller;

    public CopilotIntegrationControllerTests()
    {
        _mockRetriever = new Mock<IKnowledgeRetriever>();
        _mockStoreFactory = new Mock<IKnowledgeStoreFactory>();
        var mockLogger = new Mock<ILogger<CopilotIntegrationController>>();

        _controller = new CopilotIntegrationController(
            _mockRetriever.Object,
            _mockStoreFactory.Object,
            mockLogger.Object);

        SetupAuthentication("test-tenant-id");
    }

    [Fact]
    public async Task SearchKnowledge_EmptyQuery_ReturnsBadRequest()
    {
        var request = new CopilotSearchRequest { Query = "" };
        var result = await _controller.SearchKnowledge(request, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task SearchKnowledge_NoTenant_ReturnsUnauthorized()
    {
        SetupAuthentication(null);
        var request = new CopilotSearchRequest { Query = "test" };
        var result = await _controller.SearchKnowledge(request, CancellationToken.None);

        Assert.IsType<UnauthorizedObjectResult>(result.Result);
    }

    [Fact]
    public async Task SearchKnowledge_ValidQuery_ReturnsResults()
    {
        var retrievalResults = new List<RetrievalResult>
        {
            new()
            {
                Entry = new KnowledgeEntry
                {
                    Id = "k1",
                    Title = "Test Knowledge",
                    Summary = "Summary",
                    Content = "Full content",
                    Category = TacitKnowledgeCategory.ExpertKnowledge,
                    MeetingSubject = "Meeting 1",
                    MeetingDate = DateTimeOffset.UtcNow,
                    SourceSpeaker = "Alice",
                    Tags = ["tag1"]
                },
                RelevanceScore = 0.9f,
                MatchHighlight = "highlighted",
                Source = RetrievalSource.HybridSearch
            }
        };

        _mockRetriever
            .Setup(r => r.RetrieveAsync(It.IsAny<RetrievalQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(retrievalResults);

        var request = new CopilotSearchRequest { Query = "expert knowledge", MaxResults = 5 };
        var result = await _controller.SearchKnowledge(request, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<CopilotSearchResponse>(okResult.Value);
        Assert.Single(response.Results);
        Assert.Equal("Test Knowledge", response.Results[0].Title);
        Assert.Equal(0.9f, response.Results[0].RelevanceScore);
    }

    [Fact]
    public async Task SearchKnowledge_WithCategoryFilter_PassesToRetriever()
    {
        _mockRetriever
            .Setup(r => r.RetrieveAsync(It.IsAny<RetrievalQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<RetrievalResult>());

        var request = new CopilotSearchRequest
        {
            Query = "test",
            Category = "ExpertKnowledge"
        };

        await _controller.SearchKnowledge(request, CancellationToken.None);

        _mockRetriever.Verify(r => r.RetrieveAsync(
            It.Is<RetrievalQuery>(q =>
                q.CategoryFilter != null &&
                q.CategoryFilter.Contains(TacitKnowledgeCategory.ExpertKnowledge)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SearchKnowledge_InvalidCategory_PassesNullFilter()
    {
        _mockRetriever
            .Setup(r => r.RetrieveAsync(It.IsAny<RetrievalQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<RetrievalResult>());

        var request = new CopilotSearchRequest
        {
            Query = "test",
            Category = "NonExistentCategory"
        };

        await _controller.SearchKnowledge(request, CancellationToken.None);

        _mockRetriever.Verify(r => r.RetrieveAsync(
            It.Is<RetrievalQuery>(q => q.CategoryFilter == null),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SearchKnowledge_SetsCorrectTenantId()
    {
        _mockRetriever
            .Setup(r => r.RetrieveAsync(It.IsAny<RetrievalQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<RetrievalResult>());

        var request = new CopilotSearchRequest { Query = "test" };
        await _controller.SearchKnowledge(request, CancellationToken.None);

        _mockRetriever.Verify(r => r.RetrieveAsync(
            It.Is<RetrievalQuery>(q => q.TenantId == "test-tenant-id"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetKnowledge_NotFound_ReturnsNotFound()
    {
        var mockStore = new Mock<IKnowledgeStore>();
        mockStore.Setup(s => s.GetKnowledgeAsync("unknown", It.IsAny<CancellationToken>()))
            .ReturnsAsync((KnowledgeEntry?)null);
        _mockStoreFactory.Setup(f => f.CreateStore("CosmosDB")).Returns(mockStore.Object);

        var result = await _controller.GetKnowledge("unknown", CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task GetKnowledge_DifferentTenant_ReturnsForbid()
    {
        var entry = new KnowledgeEntry { Id = "k1", TenantId = "other-tenant" };
        var mockStore = new Mock<IKnowledgeStore>();
        mockStore.Setup(s => s.GetKnowledgeAsync("k1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(entry);
        _mockStoreFactory.Setup(f => f.CreateStore("CosmosDB")).Returns(mockStore.Object);

        var result = await _controller.GetKnowledge("k1", CancellationToken.None);

        Assert.IsType<ForbidResult>(result.Result);
    }

    [Fact]
    public async Task GetKnowledge_ValidRequest_ReturnsEntry()
    {
        var entry = new KnowledgeEntry
        {
            Id = "k1",
            TenantId = "test-tenant-id",
            Title = "Test Entry"
        };

        var mockStore = new Mock<IKnowledgeStore>();
        mockStore.Setup(s => s.GetKnowledgeAsync("k1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(entry);
        _mockStoreFactory.Setup(f => f.CreateStore("CosmosDB")).Returns(mockStore.Object);

        var result = await _controller.GetKnowledge("k1", CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var returned = Assert.IsType<KnowledgeEntry>(okResult.Value);
        Assert.Equal("Test Entry", returned.Title);
    }

    [Fact]
    public async Task GetKnowledge_NoTenant_ReturnsUnauthorized()
    {
        SetupAuthentication(null);
        var result = await _controller.GetKnowledge("k1", CancellationToken.None);

        Assert.IsType<UnauthorizedObjectResult>(result.Result);
    }

    [Fact]
    public async Task GetStats_ValidRequest_ReturnsStats()
    {
        var stats = new KnowledgeStoreStats
        {
            TenantId = "test-tenant-id",
            TotalEntries = 42,
            ConfirmedCount = 30
        };

        var mockStore = new Mock<IKnowledgeStore>();
        mockStore.Setup(s => s.GetStatsAsync("test-tenant-id", It.IsAny<CancellationToken>()))
            .ReturnsAsync(stats);
        _mockStoreFactory.Setup(f => f.CreateStore("CosmosDB")).Returns(mockStore.Object);

        var result = await _controller.GetStats(CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var returned = Assert.IsType<KnowledgeStoreStats>(okResult.Value);
        Assert.Equal(42, returned.TotalEntries);
    }

    [Fact]
    public async Task GetStats_NoTenant_ReturnsUnauthorized()
    {
        SetupAuthentication(null);
        var result = await _controller.GetStats(CancellationToken.None);

        Assert.IsType<UnauthorizedObjectResult>(result.Result);
    }

    [Fact]
    public void CopilotSearchRequest_DefaultValues()
    {
        var request = new CopilotSearchRequest();
        Assert.Equal(5, request.MaxResults);
        Assert.Null(request.Category);
        Assert.Null(request.Language);
    }

    [Fact]
    public void CopilotSearchResponse_CanBeCreated()
    {
        var response = new CopilotSearchResponse
        {
            Query = "test",
            TotalCount = 2,
            Results =
            [
                new CopilotSearchResult { Title = "R1" },
                new CopilotSearchResult { Title = "R2" }
            ]
        };

        Assert.Equal(2, response.TotalCount);
        Assert.Equal(2, response.Results.Count);
    }

    private void SetupAuthentication(string? tenantId)
    {
        var claims = new List<Claim>();
        if (tenantId is not null)
            claims.Add(new Claim("tid", tenantId));

        var identity = new ClaimsIdentity(claims, "test");
        var principal = new ClaimsPrincipal(identity);

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };
    }
}
