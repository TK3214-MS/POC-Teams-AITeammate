using Microsoft.AspNetCore.Mvc;
using TeamsAITeammate.Core.Interfaces;
using TeamsAITeammate.Core.Models;

namespace TeamsAITeammate.Agent.Controllers;

[ApiController]
[Route("api/copilot")]
public class CopilotIntegrationController : ControllerBase
{
    private readonly IKnowledgeRetriever _retriever;
    private readonly IKnowledgeStoreFactory _storeFactory;
    private readonly ILogger<CopilotIntegrationController> _logger;

    public CopilotIntegrationController(
        IKnowledgeRetriever retriever,
        IKnowledgeStoreFactory storeFactory,
        ILogger<CopilotIntegrationController> logger)
    {
        _retriever = retriever;
        _storeFactory = storeFactory;
        _logger = logger;
    }

    [HttpPost("search")]
    public async Task<ActionResult<CopilotSearchResponse>> SearchKnowledge(
        [FromBody] CopilotSearchRequest request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Query))
            return BadRequest("Query is required");

        var tenantId = GetTenantId();
        if (string.IsNullOrEmpty(tenantId))
            return Unauthorized("Tenant ID could not be determined");

        var query = new RetrievalQuery
        {
            QueryText = request.Query,
            TenantId = tenantId,
            Strategy = RetrievalStrategy.HybridSearch,
            MaxResults = request.MaxResults,
            MinRelevanceScore = 0.5f,
            CategoryFilter = !string.IsNullOrEmpty(request.Category)
                && Enum.TryParse<TacitKnowledgeCategory>(request.Category, out var cat)
                    ? [cat]
                    : null,
            DateFrom = request.FromDate,
            DateTo = request.ToDate
        };

        var results = await _retriever.RetrieveAsync(query, ct);

        var response = new CopilotSearchResponse
        {
            Query = request.Query,
            TotalCount = results.Count,
            Results = results.Select(r => new CopilotSearchResult
            {
                Id = r.Entry.Id,
                Title = r.Entry.Title,
                Summary = r.Entry.Summary,
                Category = r.Entry.Category.ToString(),
                MeetingSubject = r.Entry.MeetingSubject,
                MeetingDate = r.Entry.MeetingDate,
                SourceSpeaker = r.Entry.SourceSpeaker,
                Tags = r.Entry.Tags,
                RelevanceScore = r.RelevanceScore,
                Highlight = r.MatchHighlight
            }).ToList()
        };

        _logger.LogInformation(
            "Copilot search for '{Query}' returned {Count} results",
            request.Query, results.Count);

        return Ok(response);
    }

    [HttpGet("knowledge/{id}")]
    public async Task<ActionResult<KnowledgeEntry>> GetKnowledge(string id, CancellationToken ct)
    {
        var tenantId = GetTenantId();
        if (string.IsNullOrEmpty(tenantId))
            return Unauthorized("Tenant ID could not be determined");

        var store = _storeFactory.CreateStore("CosmosDB");
        var entry = await store.GetKnowledgeAsync(id, ct);

        if (entry is null)
            return NotFound();

        if (entry.TenantId != tenantId)
            return Forbid();

        return Ok(entry);
    }

    [HttpGet("stats")]
    public async Task<ActionResult<KnowledgeStoreStats>> GetStats(CancellationToken ct)
    {
        var tenantId = GetTenantId();
        if (string.IsNullOrEmpty(tenantId))
            return Unauthorized("Tenant ID could not be determined");

        var store = _storeFactory.CreateStore("CosmosDB");
        var stats = await store.GetStatsAsync(tenantId, ct);
        return Ok(stats);
    }

    private string? GetTenantId()
    {
        // Extract tenant ID from the authenticated user's claims
        return User?.FindFirst("http://schemas.microsoft.com/identity/claims/tenantid")?.Value
            ?? User?.FindFirst("tid")?.Value;
    }
}
