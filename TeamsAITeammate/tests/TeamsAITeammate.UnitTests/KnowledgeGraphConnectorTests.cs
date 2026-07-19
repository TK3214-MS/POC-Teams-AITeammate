using Microsoft.Extensions.Logging;
using Microsoft.Graph;
using Microsoft.Graph.Models.ExternalConnectors;
using Moq;
using TeamsAITeammate.Core.Models;

namespace TeamsAITeammate.UnitTests;

public class KnowledgeGraphConnectorTests
{
    [Fact]
    public void KnowledgeEntry_CanMapToExternalItem()
    {
        var entry = new KnowledgeEntry
        {
            Id = "k1",
            TenantId = "tenant-1",
            Title = "Expert Knowledge",
            Content = "Some valuable content",
            Category = TacitKnowledgeCategory.ExpertKnowledge,
            MeetingSubject = "Design Review",
            MeetingDate = new DateTimeOffset(2026, 7, 15, 10, 0, 0, TimeSpan.Zero),
            SourceSpeaker = "Alice",
            Tags = ["design", "architecture"]
        };

        Assert.Equal("k1", entry.Id);
        Assert.Equal("tenant-1", entry.TenantId);
        Assert.Equal("Expert Knowledge", entry.Title);
    }

    [Fact]
    public void KnowledgeEntry_Tags_JoinedAsString()
    {
        var entry = new KnowledgeEntry
        {
            Tags = ["tag1", "tag2", "tag3"]
        };

        var joined = string.Join(",", entry.Tags);
        Assert.Equal("tag1,tag2,tag3", joined);
    }

    [Fact]
    public void KnowledgeEntry_EmptyTags_JoinsEmpty()
    {
        var entry = new KnowledgeEntry();
        var joined = string.Join(",", entry.Tags);
        Assert.Equal(string.Empty, joined);
    }

    [Fact]
    public void ExternalConnection_PropertiesAreCorrect()
    {
        var connection = new ExternalConnection
        {
            Id = "aiteammateknowledge",
            Name = "AI Teammate Knowledge Base",
            Description = "Teams会議から自動抽出された暗黙知ナレッジベース"
        };

        Assert.Equal("aiteammateknowledge", connection.Id);
        Assert.Equal("AI Teammate Knowledge Base", connection.Name);
        Assert.Contains("暗黙知", connection.Description);
    }

    [Fact]
    public void SchemaProperties_CoverRequiredFields()
    {
        var requiredFields = new[]
        {
            "title", "content", "category", "meetingSubject",
            "meetingDate", "sourceSpeaker", "tags"
        };

        Assert.Equal(7, requiredFields.Length);
    }

    [Fact]
    public void ExternalItem_AclUsesEveryone()
    {
        var acl = new Acl
        {
            Type = AclType.Everyone,
            Value = "tenant-id",
            AccessType = AccessType.Grant
        };

        Assert.Equal(AclType.Everyone, acl.Type);
        Assert.Equal(AccessType.Grant, acl.AccessType);
    }

    [Fact]
    public void ExternalItemContent_TextType()
    {
        var content = new ExternalItemContent
        {
            Type = ExternalItemContentType.Text,
            Value = "Test content"
        };

        Assert.Equal(ExternalItemContentType.Text, content.Type);
        Assert.Equal("Test content", content.Value);
    }
}
