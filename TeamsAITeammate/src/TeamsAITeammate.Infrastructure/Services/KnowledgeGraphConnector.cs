using Microsoft.Extensions.Logging;
using Microsoft.Graph;
using Microsoft.Graph.Models.ExternalConnectors;
using TeamsAITeammate.Core.Models;

namespace TeamsAITeammate.Infrastructure.Services;

public class KnowledgeGraphConnector
{
    private readonly GraphServiceClient _graphClient;
    private readonly ILogger<KnowledgeGraphConnector> _logger;

    private const string ConnectionId = "aiteammateknowledge";
    private const string ConnectionName = "AI Teammate Knowledge Base";
    private const string ConnectionDescription =
        "Teams会議から自動抽出された暗黙知ナレッジベース";

    public KnowledgeGraphConnector(
        GraphClientService graphClientService,
        ILogger<KnowledgeGraphConnector> logger)
    {
        _graphClient = graphClientService.Client;
        _logger = logger;
    }

    internal KnowledgeGraphConnector(
        GraphServiceClient graphClient,
        ILogger<KnowledgeGraphConnector> logger)
    {
        _graphClient = graphClient;
        _logger = logger;
    }

    public async Task CreateConnectionAsync(CancellationToken ct)
    {
        var connection = new ExternalConnection
        {
            Id = ConnectionId,
            Name = ConnectionName,
            Description = ConnectionDescription,
            Configuration = new Configuration
            {
                AuthorizedAppIds = []
            }
        };

        await _graphClient.External.Connections.PostAsync(connection, cancellationToken: ct);
        _logger.LogInformation("Created Graph Connector connection '{Id}'", ConnectionId);
    }

    public async Task CreateSchemaAsync(CancellationToken ct)
    {
        var schema = new Schema
        {
            BaseType = "microsoft.graph.externalItem",
            Properties =
            [
                new Property
                {
                    Name = "title",
                    Type = PropertyType.String,
                    IsSearchable = true,
                    IsRetrievable = true,
                    IsQueryable = true,
                    Labels = [Label.Title]
                },
                new Property
                {
                    Name = "content",
                    Type = PropertyType.String,
                    IsSearchable = true,
                    IsRetrievable = true
                },
                new Property
                {
                    Name = "category",
                    Type = PropertyType.String,
                    IsRetrievable = true,
                    IsQueryable = true,
                    IsRefinable = true
                },
                new Property
                {
                    Name = "meetingSubject",
                    Type = PropertyType.String,
                    IsSearchable = true,
                    IsRetrievable = true,
                    IsQueryable = true
                },
                new Property
                {
                    Name = "meetingDate",
                    Type = PropertyType.DateTime,
                    IsRetrievable = true,
                    IsQueryable = true,
                    IsRefinable = true
                },
                new Property
                {
                    Name = "sourceSpeaker",
                    Type = PropertyType.String,
                    IsSearchable = true,
                    IsRetrievable = true,
                    IsQueryable = true
                },
                new Property
                {
                    Name = "tags",
                    Type = PropertyType.String,
                    IsSearchable = true,
                    IsRetrievable = true,
                    IsQueryable = true
                }
            ]
        };

        await _graphClient.External.Connections[ConnectionId].Schema
            .PatchAsync(schema, cancellationToken: ct);

        _logger.LogInformation("Created schema for connection '{Id}'", ConnectionId);
    }

    public async Task IngestItemAsync(KnowledgeEntry entry, CancellationToken ct)
    {
        var externalItem = new ExternalItem
        {
            Id = entry.Id,
            Content = new ExternalItemContent
            {
                Type = ExternalItemContentType.Text,
                Value = entry.Content
            },
            Properties = new Properties
            {
                AdditionalData = new Dictionary<string, object>
                {
                    ["title"] = entry.Title,
                    ["content"] = entry.Content,
                    ["category"] = entry.Category.ToString(),
                    ["meetingSubject"] = entry.MeetingSubject,
                    ["meetingDate"] = entry.MeetingDate,
                    ["sourceSpeaker"] = entry.SourceSpeaker,
                    ["tags"] = string.Join(",", entry.Tags)
                }
            },
            Acl =
            [
                new Acl
                {
                    Type = AclType.Everyone,
                    Value = entry.TenantId,
                    AccessType = AccessType.Grant
                }
            ]
        };

        await _graphClient.External.Connections[ConnectionId].Items[entry.Id]
            .PutAsync(externalItem, cancellationToken: ct);

        _logger.LogDebug("Ingested knowledge entry {Id} to Graph Connector", entry.Id);
    }

    public async Task DeleteItemAsync(string itemId, CancellationToken ct)
    {
        await _graphClient.External.Connections[ConnectionId].Items[itemId]
            .DeleteAsync(cancellationToken: ct);

        _logger.LogDebug("Deleted knowledge entry {Id} from Graph Connector", itemId);
    }
}
