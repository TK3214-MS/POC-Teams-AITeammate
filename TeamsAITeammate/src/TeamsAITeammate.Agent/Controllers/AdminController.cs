using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TeamsAITeammate.Core.Interfaces;
using TeamsAITeammate.Core.Models;

namespace TeamsAITeammate.Agent.Controllers;

[ApiController]
[Route("api/admin")]
public class AdminController : ControllerBase
{
    private readonly IAgentSettingsRepository _settingsRepo;
    private readonly IKnowledgeRepository _knowledgeRepo;
    private readonly ITenantUserRepository _userRepo;
    private readonly IDashboardService _dashboardService;
    private readonly IAuditLogService _auditLog;
    private readonly ILogger<AdminController> _logger;

    public AdminController(
        IAgentSettingsRepository settingsRepo,
        IKnowledgeRepository knowledgeRepo,
        ITenantUserRepository userRepo,
        IDashboardService dashboardService,
        IAuditLogService auditLog,
        ILogger<AdminController> logger)
    {
        _settingsRepo = settingsRepo;
        _knowledgeRepo = knowledgeRepo;
        _userRepo = userRepo;
        _dashboardService = dashboardService;
        _auditLog = auditLog;
        _logger = logger;
    }

    private string GetTenantId() =>
        HttpContext.User.FindFirst("http://schemas.microsoft.com/identity/claims/tenantid")?.Value
        ?? HttpContext.Request.Headers["X-Tenant-Id"].FirstOrDefault()
        ?? "default";

    private string GetUserId() =>
        HttpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "anonymous";

    // ---- Dashboard ----

    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboard(CancellationToken ct)
    {
        var tenantId = GetTenantId();
        var stats = await _dashboardService.GetStatsAsync(tenantId, ct);
        return Ok(stats);
    }

    // ---- Agent Settings ----

    [HttpGet("settings")]
    public async Task<IActionResult> GetSettings(CancellationToken ct)
    {
        var tenantId = GetTenantId();
        var settings = await _settingsRepo.GetAsync(tenantId, ct);
        return Ok(settings ?? new AgentSettings { TenantId = tenantId });
    }

    [HttpPut("settings")]
    public async Task<IActionResult> UpdateSettings([FromBody] AgentSettings settings, CancellationToken ct)
    {
        var tenantId = GetTenantId();
        var userId = GetUserId();

        var updated = settings with
        {
            TenantId = tenantId,
            UpdatedAt = DateTimeOffset.UtcNow,
            UpdatedBy = userId
        };

        await _settingsRepo.SaveAsync(updated, ct);
        await _auditLog.LogAsync(tenantId, userId, "UpdateSettings", "AgentSettings", tenantId, null, ct);

        return Ok(updated);
    }

    // ---- Knowledge Management ----

    [HttpGet("knowledge")]
    public async Task<IActionResult> GetKnowledge(
        [FromQuery] string? query, [FromQuery] int limit = 50, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();

        var entries = string.IsNullOrWhiteSpace(query)
            ? await _knowledgeRepo.GetByTenantAsync(tenantId, ct)
            : await _knowledgeRepo.SearchAsync(tenantId, query, limit, ct);

        return Ok(entries);
    }

    [HttpGet("knowledge/{id}")]
    public async Task<IActionResult> GetKnowledgeEntry(string id, CancellationToken ct)
    {
        var tenantId = GetTenantId();
        var entry = await _knowledgeRepo.GetByIdAsync(id, tenantId, ct);
        return entry is null ? NotFound() : Ok(entry);
    }

    [HttpPut("knowledge/{id}")]
    public async Task<IActionResult> UpdateKnowledgeEntry(string id, [FromBody] KnowledgeEntry entry, CancellationToken ct)
    {
        var tenantId = GetTenantId();
        var userId = GetUserId();

        var existing = await _knowledgeRepo.GetByIdAsync(id, tenantId, ct);
        if (existing is null) return NotFound();

        var updated = entry with { Id = id, TenantId = tenantId, UpdatedAt = DateTimeOffset.UtcNow };
        await _knowledgeRepo.UpsertAsync(updated, ct);
        await _auditLog.LogAsync(tenantId, userId, "UpdateKnowledge", "KnowledgeEntry", id, null, ct);

        return Ok(updated);
    }

    [HttpPost("knowledge")]
    public async Task<IActionResult> CreateKnowledgeEntry([FromBody] KnowledgeEntry entry, CancellationToken ct)
    {
        var tenantId = GetTenantId();
        var userId = GetUserId();

        var created = entry with { TenantId = tenantId };
        await _knowledgeRepo.UpsertAsync(created, ct);
        await _auditLog.LogAsync(tenantId, userId, "CreateKnowledge", "KnowledgeEntry", created.Id, null, ct);

        return Created($"/api/admin/knowledge/{created.Id}", created);
    }

    [HttpDelete("knowledge/{id}")]
    public async Task<IActionResult> DeleteKnowledgeEntry(string id, CancellationToken ct)
    {
        var tenantId = GetTenantId();
        var userId = GetUserId();

        await _knowledgeRepo.DeleteAsync(id, tenantId, ct);
        await _auditLog.LogAsync(tenantId, userId, "DeleteKnowledge", "KnowledgeEntry", id, null, ct);

        return NoContent();
    }

    // ---- User Management ----

    [HttpGet("users")]
    public async Task<IActionResult> GetUsers(CancellationToken ct)
    {
        var tenantId = GetTenantId();
        var users = await _userRepo.GetUsersAsync(tenantId, ct);
        return Ok(users);
    }

    [HttpPut("users/{userId}/role")]
    public async Task<IActionResult> UpdateUserRole(string userId, [FromBody] UserRoleUpdate update, CancellationToken ct)
    {
        var tenantId = GetTenantId();
        var currentUserId = GetUserId();

        var user = await _userRepo.GetUserAsync(tenantId, userId, ct);
        if (user is null) return NotFound();

        var updated = user with { Role = update.Role };
        await _userRepo.SaveUserAsync(updated, ct);
        await _auditLog.LogAsync(tenantId, currentUserId, "UpdateUserRole", "TenantUser", userId,
            $"Role changed to {update.Role}", ct);

        return Ok(updated);
    }

    // ---- Audit Logs ----

    [HttpGet("audit-logs")]
    public async Task<IActionResult> GetAuditLogs(
        [FromQuery] DateTimeOffset? from, [FromQuery] DateTimeOffset? to,
        [FromQuery] int limit = 100, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var logs = await _auditLog.GetLogsAsync(tenantId, from, to, limit, ct);
        return Ok(logs);
    }
}

public record UserRoleUpdate
{
    public UserRole Role { get; init; }
}
