using TeamsAITeammate.Core.Models;

namespace TeamsAITeammate.UnitTests;

public class KnowledgeStoreModelsTests
{
    [Fact]
    public void KnowledgeEntry_NewEntry_HasDraftStatus()
    {
        var entry = new KnowledgeEntry();

        Assert.Equal(KnowledgeStatus.Draft, entry.Status);
    }

    [Fact]
    public void KnowledgeEntry_NewEntry_HasEmptyDefaults()
    {
        var entry = new KnowledgeEntry();

        Assert.NotNull(entry.Id);
        Assert.Empty(entry.TenantId);
        Assert.Empty(entry.MeetingId);
        Assert.Empty(entry.SessionId);
        Assert.Empty(entry.Title);
        Assert.Empty(entry.Content);
        Assert.Empty(entry.Summary);
        Assert.Empty(entry.SourceSpeaker);
        Assert.Empty(entry.SourceTranscriptSegmentId);
        Assert.Empty(entry.MeetingSubject);
        Assert.Empty(entry.Language);
        Assert.Empty(entry.Tags);
        Assert.Empty(entry.RelatedTopics);
        Assert.Empty(entry.Participants);
        Assert.Null(entry.ValidatedBy);
        Assert.Null(entry.ValidatedAt);
        Assert.Null(entry.Embedding);
        Assert.Null(entry.UpdatedAt);
        Assert.Equal(0, entry.ConfidenceScore);
    }

    [Fact]
    public void KnowledgeStatus_ContainsAllExpectedValues()
    {
        var values = Enum.GetValues<KnowledgeStatus>();

        Assert.Contains(KnowledgeStatus.Draft, values);
        Assert.Contains(KnowledgeStatus.Confirmed, values);
        Assert.Contains(KnowledgeStatus.Edited, values);
        Assert.Contains(KnowledgeStatus.Rejected, values);
        Assert.Contains(KnowledgeStatus.Archived, values);
        Assert.Equal(5, values.Length);
    }

    [Fact]
    public void KnowledgeEntry_WithEmbedding_RetainsVector()
    {
        var vector = new float[] { 0.1f, 0.2f, 0.3f };
        var entry = new KnowledgeEntry { Embedding = vector };

        Assert.Equal(3, entry.Embedding!.Length);
        Assert.Equal(0.1f, entry.Embedding[0]);
    }

    [Fact]
    public void KnowledgeEntry_WithRecord_CreatesModifiedCopy()
    {
        var original = new KnowledgeEntry
        {
            TenantId = "tenant-1",
            Status = KnowledgeStatus.Draft,
            Content = "Original"
        };

        var updated = original with
        {
            Status = KnowledgeStatus.Confirmed,
            ValidatedBy = "user-1",
            ValidatedAt = DateTimeOffset.UtcNow
        };

        Assert.Equal(KnowledgeStatus.Draft, original.Status);
        Assert.Equal(KnowledgeStatus.Confirmed, updated.Status);
        Assert.Equal("user-1", updated.ValidatedBy);
        Assert.Equal("tenant-1", updated.TenantId);
        Assert.Equal("Original", updated.Content);
    }

    [Fact]
    public void KnowledgeSearchOptions_DefaultValues()
    {
        var options = new KnowledgeSearchOptions();

        Assert.Equal(10, options.MaxResults);
        Assert.Null(options.TenantId);
        Assert.Null(options.Category);
        Assert.Null(options.Status);
        Assert.False(options.UseVectorSearch);
        Assert.Equal(0.0f, options.MinRelevanceScore);
    }

    [Fact]
    public void KnowledgeSearchOptions_WithFilters()
    {
        var options = new KnowledgeSearchOptions
        {
            TenantId = "tenant-1",
            Category = TacitKnowledgeCategory.ExpertKnowledge,
            Status = KnowledgeStatus.Confirmed,
            MaxResults = 20,
            UseVectorSearch = true,
            QueryVector = new float[] { 0.1f, 0.2f },
            MinRelevanceScore = 0.7f
        };

        Assert.Equal("tenant-1", options.TenantId);
        Assert.Equal(TacitKnowledgeCategory.ExpertKnowledge, options.Category);
        Assert.Equal(KnowledgeStatus.Confirmed, options.Status);
        Assert.Equal(20, options.MaxResults);
        Assert.True(options.UseVectorSearch);
        Assert.NotNull(options.QueryVector);
        Assert.Equal(0.7f, options.MinRelevanceScore);
    }

    [Fact]
    public void KnowledgeStoreStats_DefaultValues()
    {
        var stats = new KnowledgeStoreStats();

        Assert.Empty(stats.TenantId);
        Assert.Equal(0, stats.TotalEntries);
        Assert.Equal(0, stats.DraftCount);
        Assert.Equal(0, stats.ConfirmedCount);
        Assert.Equal(0, stats.RejectedCount);
        Assert.Equal(0, stats.ArchivedCount);
        Assert.Empty(stats.EntriesByCategory);
    }

    [Fact]
    public void IngestionContext_DefaultValues()
    {
        var context = new IngestionContext();

        Assert.Empty(context.TenantId);
        Assert.Empty(context.SessionId);
        Assert.Empty(context.MeetingId);
        Assert.Empty(context.MeetingSubject);
        Assert.Empty(context.Language);
        Assert.Equal("CosmosDB", context.DataStoreProvider);
    }

    [Fact]
    public void IngestionContext_WithValues()
    {
        var context = new IngestionContext
        {
            TenantId = "tenant-1",
            SessionId = "session-1",
            MeetingId = "meeting-1",
            MeetingSubject = "Design Review",
            MeetingDate = new DateTimeOffset(2025, 6, 15, 10, 0, 0, TimeSpan.Zero),
            Participants = ["Alice", "Bob", "Charlie"],
            Language = "ja",
            DataStoreProvider = "Dataverse"
        };

        Assert.Equal("tenant-1", context.TenantId);
        Assert.Equal(3, context.Participants.Count);
        Assert.Equal("Dataverse", context.DataStoreProvider);
    }
}
