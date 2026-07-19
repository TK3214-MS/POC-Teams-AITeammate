using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TeamsAITeammate.Core.Interfaces;
using TeamsAITeammate.Core.Models;

namespace TeamsAITeammate.Infrastructure.Services;

public class DataverseKnowledgeStore : IKnowledgeStore
{
    private readonly HttpClient _httpClient;
    private readonly string _environmentUrl;
    private readonly string _tablePrefix;
    private readonly ILogger<DataverseKnowledgeStore> _logger;

    public string ProviderName => "Dataverse";

    public DataverseKnowledgeStore(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<DataverseKnowledgeStore> logger)
    {
        _httpClient = httpClient;
        _environmentUrl = configuration["Dataverse:EnvironmentUrl"]?.TrimEnd('/') ?? string.Empty;
        _tablePrefix = configuration["Dataverse:TablePrefix"] ?? "cr_aiteammate";
        _logger = logger;
    }

    internal DataverseKnowledgeStore(
        HttpClient httpClient,
        string environmentUrl,
        string tablePrefix,
        ILogger<DataverseKnowledgeStore> logger)
    {
        _httpClient = httpClient;
        _environmentUrl = environmentUrl.TrimEnd('/');
        _tablePrefix = tablePrefix;
        _logger = logger;
    }

    private string KnowledgeTableUrl => $"{_environmentUrl}/api/data/v9.2/{_tablePrefix}_knowledges";

    public async Task<string> SaveKnowledgeAsync(KnowledgeEntry entry, CancellationToken ct)
    {
        var payload = MapToDataverse(entry);
        var response = await _httpClient.PostAsJsonAsync(KnowledgeTableUrl, payload, ct);
        response.EnsureSuccessStatusCode();

        var entityId = entry.Id;
        if (response.Headers.TryGetValues("OData-EntityId", out var values))
        {
            var entityUrl = values.First();
            var start = entityUrl.LastIndexOf('(') + 1;
            var end = entityUrl.LastIndexOf(')');
            if (start > 0 && end > start)
                entityId = entityUrl[start..end];
        }

        _logger.LogInformation("Saved knowledge entry {Id} to Dataverse", entityId);
        return entityId;
    }

    public async Task UpdateKnowledgeAsync(string id, KnowledgeEntry entry, CancellationToken ct)
    {
        var payload = MapToDataverse(entry);
        var url = $"{KnowledgeTableUrl}({id})";

        var request = new HttpRequestMessage(HttpMethod.Patch, url)
        {
            Content = JsonContent.Create(payload)
        };
        var response = await _httpClient.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
        _logger.LogInformation("Updated knowledge entry {Id} in Dataverse", id);
    }

    public async Task<KnowledgeEntry?> GetKnowledgeAsync(string id, CancellationToken ct)
    {
        var url = $"{KnowledgeTableUrl}({id})";
        var response = await _httpClient.GetAsync(url, ct);
        if (!response.IsSuccessStatusCode)
            return null;

        var data = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
        return MapFromDataverse(data);
    }

    public async Task<IReadOnlyList<KnowledgeEntry>> SearchAsync(
        string query, KnowledgeSearchOptions options, CancellationToken ct)
    {
        var filters = new List<string>();

        if (!string.IsNullOrEmpty(options.TenantId))
            filters.Add($"{_tablePrefix}_tenantid eq '{EscapeOData(options.TenantId)}'");

        if (!string.IsNullOrWhiteSpace(query))
            filters.Add($"contains({_tablePrefix}_title,'{EscapeOData(query)}')");

        if (options.Status.HasValue)
            filters.Add($"{_tablePrefix}_status eq {(int)options.Status.Value}");

        var filterStr = filters.Count > 0 ? "$filter=" + string.Join(" and ", filters) : string.Empty;
        var url = $"{KnowledgeTableUrl}?{filterStr}&$top={options.MaxResults}&$orderby={_tablePrefix}_createdat desc";

        var response = await _httpClient.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
        var results = new List<KnowledgeEntry>();

        if (json.TryGetProperty("value", out var valueArray))
        {
            foreach (var item in valueArray.EnumerateArray())
            {
                results.Add(MapFromDataverse(item));
            }
        }

        return results;
    }

    public async Task DeleteKnowledgeAsync(string id, CancellationToken ct)
    {
        var url = $"{KnowledgeTableUrl}({id})";
        var response = await _httpClient.DeleteAsync(url, ct);
        response.EnsureSuccessStatusCode();
        _logger.LogInformation("Deleted knowledge entry {Id} from Dataverse", id);
    }

    public async Task<IReadOnlyList<KnowledgeEntry>> GetBySessionAsync(string sessionId, CancellationToken ct)
    {
        var url = $"{KnowledgeTableUrl}?$filter={_tablePrefix}_sessionid eq '{EscapeOData(sessionId)}'&$orderby={_tablePrefix}_createdat desc";
        var response = await _httpClient.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
        var results = new List<KnowledgeEntry>();

        if (json.TryGetProperty("value", out var valueArray))
        {
            foreach (var item in valueArray.EnumerateArray())
            {
                results.Add(MapFromDataverse(item));
            }
        }

        return results;
    }

    public async Task<KnowledgeStoreStats> GetStatsAsync(string tenantId, CancellationToken ct)
    {
        var url = $"{KnowledgeTableUrl}?$filter={_tablePrefix}_tenantid eq '{EscapeOData(tenantId)}'&$select={_tablePrefix}_status&$count=true";
        var response = await _httpClient.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
        var statusCounts = new Dictionary<string, int>();
        var total = 0;

        if (json.TryGetProperty("value", out var valueArray))
        {
            foreach (var item in valueArray.EnumerateArray())
            {
                total++;
                var statusVal = item.TryGetProperty($"{_tablePrefix}_status", out var s) ? s.GetInt32() : 0;
                var statusName = ((KnowledgeStatus)statusVal).ToString();
                statusCounts[statusName] = statusCounts.GetValueOrDefault(statusName) + 1;
            }
        }

        return new KnowledgeStoreStats
        {
            TenantId = tenantId,
            TotalEntries = total,
            DraftCount = statusCounts.GetValueOrDefault("Draft"),
            ConfirmedCount = statusCounts.GetValueOrDefault("Confirmed"),
            RejectedCount = statusCounts.GetValueOrDefault("Rejected"),
            ArchivedCount = statusCounts.GetValueOrDefault("Archived"),
            EntriesByCategory = statusCounts
        };
    }

    private Dictionary<string, object?> MapToDataverse(KnowledgeEntry entry)
    {
        return new Dictionary<string, object?>
        {
            [$"{_tablePrefix}_knowledgeid"] = entry.Id,
            [$"{_tablePrefix}_tenantid"] = entry.TenantId,
            [$"{_tablePrefix}_sessionid"] = entry.SessionId,
            [$"{_tablePrefix}_meetingid"] = entry.MeetingId,
            [$"{_tablePrefix}_title"] = entry.Title,
            [$"{_tablePrefix}_content"] = entry.Content,
            [$"{_tablePrefix}_summary"] = entry.Summary,
            [$"{_tablePrefix}_category"] = (int)entry.Category,
            [$"{_tablePrefix}_status"] = (int)entry.Status,
            [$"{_tablePrefix}_meetingsubject"] = entry.MeetingSubject,
            [$"{_tablePrefix}_meetingdate"] = entry.MeetingDate,
            [$"{_tablePrefix}_sourcespeaker"] = entry.SourceSpeaker,
            [$"{_tablePrefix}_confidence"] = entry.ConfidenceScore,
            [$"{_tablePrefix}_tags"] = string.Join(",", entry.Tags),
            [$"{_tablePrefix}_language"] = entry.Language,
            [$"{_tablePrefix}_createdat"] = entry.CreatedAt,
            [$"{_tablePrefix}_updatedat"] = entry.UpdatedAt
        };
    }

    private KnowledgeEntry MapFromDataverse(JsonElement item)
    {
        return new KnowledgeEntry
        {
            Id = GetString(item, $"{_tablePrefix}_knowledgeid"),
            TenantId = GetString(item, $"{_tablePrefix}_tenantid"),
            SessionId = GetString(item, $"{_tablePrefix}_sessionid"),
            MeetingId = GetString(item, $"{_tablePrefix}_meetingid"),
            Title = GetString(item, $"{_tablePrefix}_title"),
            Content = GetString(item, $"{_tablePrefix}_content"),
            Summary = GetString(item, $"{_tablePrefix}_summary"),
            Category = Enum.TryParse<TacitKnowledgeCategory>(
                GetString(item, $"{_tablePrefix}_category"), out var cat) ? cat : default,
            Status = item.TryGetProperty($"{_tablePrefix}_status", out var s)
                ? (KnowledgeStatus)s.GetInt32() : KnowledgeStatus.Draft,
            MeetingSubject = GetString(item, $"{_tablePrefix}_meetingsubject"),
            SourceSpeaker = GetString(item, $"{_tablePrefix}_sourcespeaker"),
            ConfidenceScore = item.TryGetProperty($"{_tablePrefix}_confidence", out var c) ? c.GetDouble() : 0,
            Tags = GetString(item, $"{_tablePrefix}_tags")
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList(),
            Language = GetString(item, $"{_tablePrefix}_language")
        };
    }

    private static string GetString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var val) ? val.GetString() ?? string.Empty : string.Empty;
    }

    private static string EscapeOData(string value) => value.Replace("'", "''");
}
