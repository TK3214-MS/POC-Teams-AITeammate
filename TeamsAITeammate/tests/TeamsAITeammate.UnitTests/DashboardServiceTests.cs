using Microsoft.Extensions.Logging;
using Moq;
using TeamsAITeammate.Core.Interfaces;
using TeamsAITeammate.Core.Models;
using TeamsAITeammate.Infrastructure.Services;

namespace TeamsAITeammate.UnitTests;

public class DashboardServiceTests
{
    private readonly Mock<IKnowledgeRepository> _knowledgeRepo = new();
    private readonly Mock<IMeetingSessionRepository> _sessionRepo = new();
    private readonly DashboardService _service;

    public DashboardServiceTests()
    {
        _service = new DashboardService(
            _knowledgeRepo.Object,
            _sessionRepo.Object,
            Mock.Of<ILogger<DashboardService>>());
    }

    [Fact]
    public async Task GetStatsAsync_ReturnsCorrectCounts()
    {
        var entries = new List<KnowledgeEntry>
        {
            new() { TenantId = "t1", Category = TacitKnowledgeCategory.ExpertKnowledge },
            new() { TenantId = "t1", Category = TacitKnowledgeCategory.ExpertKnowledge },
            new() { TenantId = "t1", Category = TacitKnowledgeCategory.DecisionBackground },
        };
        var sessions = new List<MeetingSession>
        {
            new() { TenantId = "t1", Participants = [new Participant { UserId = "u1" }] },
            new() { TenantId = "t1", Participants = [new Participant { UserId = "u2" }] },
        };

        _knowledgeRepo.Setup(r => r.GetByTenantAsync("t1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(entries);
        _sessionRepo.Setup(r => r.GetByTenantAsync("t1", 50, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sessions);

        var stats = await _service.GetStatsAsync("t1");

        Assert.Equal("t1", stats.TenantId);
        Assert.Equal(3, stats.TotalKnowledgeEntries);
        Assert.Equal(2, stats.TotalMeetingSessions);
        Assert.Equal(2, stats.KnowledgeByCategory["ExpertKnowledge"]);
        Assert.Equal(1, stats.KnowledgeByCategory["DecisionBackground"]);
    }

    [Fact]
    public async Task GetStatsAsync_EmptyTenant_ReturnsZeroCounts()
    {
        _knowledgeRepo.Setup(r => r.GetByTenantAsync("empty", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<KnowledgeEntry>());
        _sessionRepo.Setup(r => r.GetByTenantAsync("empty", 50, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MeetingSession>());

        var stats = await _service.GetStatsAsync("empty");

        Assert.Equal(0, stats.TotalKnowledgeEntries);
        Assert.Equal(0, stats.TotalMeetingSessions);
        Assert.Empty(stats.KnowledgeByCategory);
    }
}
