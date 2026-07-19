using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using System.Security.Claims;
using TeamsAITeammate.Agent.Controllers;
using TeamsAITeammate.Core.Interfaces;
using TeamsAITeammate.Core.Models;

namespace TeamsAITeammate.UnitTests;

public class AdminControllerTests
{
    private readonly Mock<IAgentSettingsRepository> _settingsRepo = new();
    private readonly Mock<IKnowledgeRepository> _knowledgeRepo = new();
    private readonly Mock<ITenantUserRepository> _userRepo = new();
    private readonly Mock<IDashboardService> _dashboardService = new();
    private readonly Mock<IAuditLogService> _auditLog = new();
    private readonly AdminController _controller;

    public AdminControllerTests()
    {
        _controller = new AdminController(
            _settingsRepo.Object,
            _knowledgeRepo.Object,
            _userRepo.Object,
            _dashboardService.Object,
            _auditLog.Object,
            Mock.Of<ILogger<AdminController>>());

        // Set up HttpContext with tenant claim
        var claims = new List<Claim>
        {
            new("http://schemas.microsoft.com/identity/claims/tenantid", "test-tenant"),
            new(ClaimTypes.NameIdentifier, "test-user")
        };
        var identity = new ClaimsIdentity(claims, "test");
        var user = new ClaimsPrincipal(identity);
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = user }
        };
    }

    [Fact]
    public async Task GetDashboard_ReturnsDashboardStats()
    {
        var stats = new DashboardStats { TenantId = "test-tenant", TotalKnowledgeEntries = 10 };
        _dashboardService.Setup(s => s.GetStatsAsync("test-tenant", It.IsAny<CancellationToken>()))
            .ReturnsAsync(stats);

        var result = await _controller.GetDashboard(CancellationToken.None) as OkObjectResult;

        Assert.NotNull(result);
        var dashboard = result.Value as DashboardStats;
        Assert.Equal(10, dashboard!.TotalKnowledgeEntries);
    }

    [Fact]
    public async Task GetSettings_WhenNoSettings_ReturnsDefault()
    {
        _settingsRepo.Setup(r => r.GetAsync("test-tenant", It.IsAny<CancellationToken>()))
            .ReturnsAsync((AgentSettings?)null);

        var result = await _controller.GetSettings(CancellationToken.None) as OkObjectResult;

        Assert.NotNull(result);
        var settings = result.Value as AgentSettings;
        Assert.Equal("test-tenant", settings!.TenantId);
    }

    [Fact]
    public async Task GetSettings_WhenExists_ReturnsStored()
    {
        var existing = new AgentSettings
        {
            TenantId = "test-tenant",
            Intervention = new InterventionConfig { Frequency = "high" }
        };
        _settingsRepo.Setup(r => r.GetAsync("test-tenant", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var result = await _controller.GetSettings(CancellationToken.None) as OkObjectResult;

        Assert.NotNull(result);
        var settings = result.Value as AgentSettings;
        Assert.Equal("high", settings!.Intervention.Frequency);
    }

    [Fact]
    public async Task UpdateSettings_SavesAndReturnsUpdated()
    {
        var input = new AgentSettings
        {
            Intervention = new InterventionConfig { Frequency = "low" }
        };

        var result = await _controller.UpdateSettings(input, CancellationToken.None) as OkObjectResult;

        Assert.NotNull(result);
        _settingsRepo.Verify(r => r.SaveAsync(It.Is<AgentSettings>(s =>
            s.TenantId == "test-tenant" && s.Intervention.Frequency == "low"),
            It.IsAny<CancellationToken>()), Times.Once);
        _auditLog.Verify(a => a.LogAsync("test-tenant", "test-user", "UpdateSettings",
            "AgentSettings", "test-tenant", null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetKnowledge_WithoutQuery_ReturnsByTenant()
    {
        var entries = new List<KnowledgeEntry>
        {
            new() { TenantId = "test-tenant", Title = "Entry 1" }
        };
        _knowledgeRepo.Setup(r => r.GetByTenantAsync("test-tenant", It.IsAny<CancellationToken>()))
            .ReturnsAsync(entries);

        var result = await _controller.GetKnowledge(null, 50, CancellationToken.None) as OkObjectResult;

        Assert.NotNull(result);
        var list = result.Value as IReadOnlyList<KnowledgeEntry>;
        Assert.Single(list!);
    }

    [Fact]
    public async Task GetKnowledge_WithQuery_ReturnsSearchResults()
    {
        var entries = new List<KnowledgeEntry>
        {
            new() { TenantId = "test-tenant", Title = "Searched" }
        };
        _knowledgeRepo.Setup(r => r.SearchAsync("test-tenant", "test", 50, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entries);

        var result = await _controller.GetKnowledge("test", 50, CancellationToken.None) as OkObjectResult;

        Assert.NotNull(result);
    }

    [Fact]
    public async Task GetKnowledgeEntry_WhenNotFound_Returns404()
    {
        _knowledgeRepo.Setup(r => r.GetByIdAsync("missing", "test-tenant", It.IsAny<CancellationToken>()))
            .ReturnsAsync((KnowledgeEntry?)null);

        var result = await _controller.GetKnowledgeEntry("missing", CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task GetKnowledgeEntry_WhenFound_ReturnsEntry()
    {
        var entry = new KnowledgeEntry { Id = "id-1", TenantId = "test-tenant" };
        _knowledgeRepo.Setup(r => r.GetByIdAsync("id-1", "test-tenant", It.IsAny<CancellationToken>()))
            .ReturnsAsync(entry);

        var result = await _controller.GetKnowledgeEntry("id-1", CancellationToken.None) as OkObjectResult;

        Assert.NotNull(result);
    }

    [Fact]
    public async Task CreateKnowledgeEntry_ReturnsCreated()
    {
        var entry = new KnowledgeEntry { Title = "New Entry" };

        var result = await _controller.CreateKnowledgeEntry(entry, CancellationToken.None) as CreatedResult;

        Assert.NotNull(result);
        _knowledgeRepo.Verify(r => r.UpsertAsync(It.Is<KnowledgeEntry>(e =>
            e.TenantId == "test-tenant"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteKnowledgeEntry_ReturnsNoContent()
    {
        var result = await _controller.DeleteKnowledgeEntry("id-1", CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
        _knowledgeRepo.Verify(r => r.DeleteAsync("id-1", "test-tenant", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetUsers_ReturnsUserList()
    {
        var users = new List<TenantUser>
        {
            new() { UserId = "u1", TenantId = "test-tenant", DisplayName = "User 1" }
        };
        _userRepo.Setup(r => r.GetUsersAsync("test-tenant", It.IsAny<CancellationToken>()))
            .ReturnsAsync(users);

        var result = await _controller.GetUsers(CancellationToken.None) as OkObjectResult;

        Assert.NotNull(result);
    }

    [Fact]
    public async Task UpdateUserRole_WhenNotFound_Returns404()
    {
        _userRepo.Setup(r => r.GetUserAsync("test-tenant", "missing", It.IsAny<CancellationToken>()))
            .ReturnsAsync((TenantUser?)null);

        var result = await _controller.UpdateUserRole("missing",
            new UserRoleUpdate { Role = UserRole.Admin }, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task UpdateUserRole_WhenFound_UpdatesAndReturns()
    {
        var user = new TenantUser { UserId = "u1", TenantId = "test-tenant", Role = UserRole.User };
        _userRepo.Setup(r => r.GetUserAsync("test-tenant", "u1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var result = await _controller.UpdateUserRole("u1",
            new UserRoleUpdate { Role = UserRole.Admin }, CancellationToken.None) as OkObjectResult;

        Assert.NotNull(result);
        _userRepo.Verify(r => r.SaveUserAsync(It.Is<TenantUser>(u =>
            u.Role == UserRole.Admin), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetAuditLogs_ReturnsLogs()
    {
        var logs = new List<AuditLogEntry>
        {
            new() { TenantId = "test-tenant", Action = "Test" }
        };
        _auditLog.Setup(a => a.GetLogsAsync("test-tenant", null, null, 100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(logs);

        var result = await _controller.GetAuditLogs(null, null, 100, CancellationToken.None) as OkObjectResult;

        Assert.NotNull(result);
    }
}
