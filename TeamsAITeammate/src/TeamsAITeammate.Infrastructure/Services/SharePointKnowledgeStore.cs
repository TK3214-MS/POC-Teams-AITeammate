using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using TeamsAITeammate.Core.Interfaces;
using CoreModels = TeamsAITeammate.Core.Models;

namespace TeamsAITeammate.Infrastructure.Services;

public class SharePointKnowledgeStore : IKnowledgeStore
{
    private readonly GraphServiceClient _graphClient;
    private readonly string _siteId;
    private readonly string _listName;
    private readonly string _documentLibraryName;
    private readonly ILogger<SharePointKnowledgeStore> _logger;

    public string ProviderName => "SharePoint";

    public SharePointKnowledgeStore(
        GraphClientService graphClientService,
        IConfiguration configuration,
        ILogger<SharePointKnowledgeStore> logger)
    {
        _graphClient = graphClientService.Client;
        _siteId = configuration["SharePoint:SiteId"] ?? string.Empty;
        _listName = configuration["SharePoint:KnowledgeListName"] ?? "Knowledge Entries";
        _documentLibraryName = configuration["SharePoint:DocumentLibraryName"] ?? "Knowledge Documents";
        _logger = logger;
    }

    internal SharePointKnowledgeStore(
        GraphServiceClient graphClient,
        string siteId,
        string listName,
        string documentLibraryName,
        ILogger<SharePointKnowledgeStore> logger)
    {
        _graphClient = graphClient;
        _siteId = siteId;
        _listName = listName;
        _documentLibraryName = documentLibraryName;
        _logger = logger;
    }

    public async Task<string> SaveKnowledgeAsync(CoreModels.KnowledgeEntry entry, CancellationToken ct)
    {
        var listItem = MapToListItem(entry);

        var created = await _graphClient.Sites[_siteId].Lists[_listName].Items
            .PostAsync(listItem, cancellationToken: ct);

        var id = created?.Id ?? entry.Id;
        _logger.LogInformation("Saved knowledge entry {Id} to SharePoint list", id);
        return id;
    }

    public async Task UpdateKnowledgeAsync(string id, CoreModels.KnowledgeEntry entry, CancellationToken ct)
    {
        var fields = MapToFieldValues(entry);

        await _graphClient.Sites[_siteId].Lists[_listName].Items[id].Fields
            .PatchAsync(new FieldValueSet { AdditionalData = fields }, cancellationToken: ct);

        _logger.LogInformation("Updated knowledge entry {Id} in SharePoint", id);
    }

    public async Task<CoreModels.KnowledgeEntry?> GetKnowledgeAsync(string id, CancellationToken ct)
    {
        try
        {
            var item = await _graphClient.Sites[_siteId].Lists[_listName].Items[id]
                .GetAsync(config =>
                {
                    config.QueryParameters.Expand = ["fields"];
                }, cancellationToken: ct);

            return item?.Fields is not null ? MapFromFields(id, item.Fields) : null;
        }
        catch (Microsoft.Graph.Models.ODataErrors.ODataError ex) when (ex.ResponseStatusCode == 404)
        {
            return null;
        }
    }

    public async Task<IReadOnlyList<CoreModels.KnowledgeEntry>> SearchAsync(
        string query, CoreModels.KnowledgeSearchOptions options, CancellationToken ct)
    {
        var filters = new List<string>();
        if (!string.IsNullOrEmpty(options.TenantId))
            filters.Add($"fields/TenantId eq '{options.TenantId}'");
        if (options.Status.HasValue)
            filters.Add($"fields/Status eq '{options.Status.Value}'");

        var filterStr = filters.Count > 0 ? string.Join(" and ", filters) : null;

        var items = await _graphClient.Sites[_siteId].Lists[_listName].Items
            .GetAsync(config =>
            {
                config.QueryParameters.Expand = ["fields"];
                if (filterStr is not null)
                    config.QueryParameters.Filter = filterStr;
                config.QueryParameters.Top = options.MaxResults;
            }, cancellationToken: ct);

        var results = new List<CoreModels.KnowledgeEntry>();
        if (items?.Value is not null)
        {
            foreach (var item in items.Value)
            {
                if (item.Fields is not null)
                {
                    var entry = MapFromFields(item.Id ?? string.Empty, item.Fields);
                    if (string.IsNullOrWhiteSpace(query) ||
                        entry.Title.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                        entry.Content.Contains(query, StringComparison.OrdinalIgnoreCase))
                    {
                        results.Add(entry);
                    }
                }
            }
        }
        return results;
    }

    public async Task DeleteKnowledgeAsync(string id, CancellationToken ct)
    {
        await _graphClient.Sites[_siteId].Lists[_listName].Items[id]
            .DeleteAsync(cancellationToken: ct);
        _logger.LogInformation("Deleted knowledge entry {Id} from SharePoint", id);
    }

    public async Task<IReadOnlyList<CoreModels.KnowledgeEntry>> GetBySessionAsync(string sessionId, CancellationToken ct)
    {
        var items = await _graphClient.Sites[_siteId].Lists[_listName].Items
            .GetAsync(config =>
            {
                config.QueryParameters.Expand = ["fields"];
                config.QueryParameters.Filter = $"fields/SessionId eq '{sessionId}'";
            }, cancellationToken: ct);

        var results = new List<CoreModels.KnowledgeEntry>();
        if (items?.Value is not null)
        {
            foreach (var item in items.Value)
            {
                if (item.Fields is not null)
                    results.Add(MapFromFields(item.Id ?? string.Empty, item.Fields));
            }
        }
        return results;
    }

    public Task<CoreModels.KnowledgeStoreStats> GetStatsAsync(string tenantId, CancellationToken ct)
    {
        // SharePoint doesn't support aggregation natively
        return Task.FromResult(new CoreModels.KnowledgeStoreStats { TenantId = tenantId });
    }

    private static ListItem MapToListItem(CoreModels.KnowledgeEntry entry)
    {
        return new ListItem
        {
            Fields = new FieldValueSet
            {
                AdditionalData = MapToFieldValues(entry)
            }
        };
    }

    private static Dictionary<string, object> MapToFieldValues(CoreModels.KnowledgeEntry entry)
    {
        return new Dictionary<string, object>
        {
            ["Title"] = entry.Title,
            ["TenantId"] = entry.TenantId,
            ["SessionId"] = entry.SessionId,
            ["MeetingId"] = entry.MeetingId,
            ["Content"] = entry.Content,
            ["Summary"] = entry.Summary,
            ["Category"] = entry.Category.ToString(),
            ["Status"] = entry.Status.ToString(),
            ["SourceSpeaker"] = entry.SourceSpeaker,
            ["MeetingSubject"] = entry.MeetingSubject,
            ["MeetingDate"] = entry.MeetingDate,
            ["Confidence"] = entry.ConfidenceScore,
            ["Tags"] = string.Join(",", entry.Tags),
            ["Language"] = entry.Language
        };
    }

    private static CoreModels.KnowledgeEntry MapFromFields(string id, FieldValueSet fields)
    {
        var data = fields.AdditionalData;

        return new CoreModels.KnowledgeEntry
        {
            Id = id,
            Title = GetFieldString(data, "Title"),
            TenantId = GetFieldString(data, "TenantId"),
            SessionId = GetFieldString(data, "SessionId"),
            MeetingId = GetFieldString(data, "MeetingId"),
            Content = GetFieldString(data, "Content"),
            Summary = GetFieldString(data, "Summary"),
            Category = Enum.TryParse<CoreModels.TacitKnowledgeCategory>(
                GetFieldString(data, "Category"), out var cat) ? cat : default,
            Status = Enum.TryParse<CoreModels.KnowledgeStatus>(
                GetFieldString(data, "Status"), out var status) ? status : CoreModels.KnowledgeStatus.Draft,
            SourceSpeaker = GetFieldString(data, "SourceSpeaker"),
            MeetingSubject = GetFieldString(data, "MeetingSubject"),
            ConfidenceScore = data.TryGetValue("Confidence", out var conf) && conf is double c ? c : 0,
            Tags = GetFieldString(data, "Tags")
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList(),
            Language = GetFieldString(data, "Language")
        };
    }

    private static string GetFieldString(IDictionary<string, object> data, string key)
    {
        return data.TryGetValue(key, out var value) ? value?.ToString() ?? string.Empty : string.Empty;
    }
}
