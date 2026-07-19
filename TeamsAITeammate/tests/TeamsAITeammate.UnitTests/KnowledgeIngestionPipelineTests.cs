using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Moq;
using TeamsAITeammate.AI.Services;
using TeamsAITeammate.Core.Interfaces;
using TeamsAITeammate.Core.Models;

namespace TeamsAITeammate.UnitTests;

public class KnowledgeIngestionPipelineTests
{
    private readonly Mock<IKnowledgeStoreFactory> _storeFactory = new();
    private readonly Mock<IKnowledgeStore> _store = new();
    private readonly Mock<IEmbeddingService> _embeddingService = new();
    private readonly Mock<IChatClient> _chatClient = new();
    private readonly KnowledgeIngestionPipeline _pipeline;

    public KnowledgeIngestionPipelineTests()
    {
        _storeFactory.Setup(f => f.CreateStore(It.IsAny<string>())).Returns(_store.Object);
        _storeFactory.Setup(f => f.GetAvailableProviders()).Returns(new[] { "CosmosDB" });
        _store.Setup(s => s.ProviderName).Returns("CosmosDB");

        // Return empty search results (no duplicates)
        _store.Setup(s => s.SearchAsync(It.IsAny<string>(), It.IsAny<KnowledgeSearchOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<KnowledgeEntry>());

        // Return mock embedding
        _embeddingService.Setup(e => e.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new float[] { 0.1f, 0.2f, 0.3f });

        // Return mock LLM response for title/summary
        SetupChatResponse("""{"title": "Test Title", "summary": "Test summary of the knowledge"}""");

        // Save returns the entry ID
        _store.Setup(s => s.SaveKnowledgeAsync(It.IsAny<KnowledgeEntry>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((KnowledgeEntry e, CancellationToken _) => e.Id);

        _pipeline = new KnowledgeIngestionPipeline(
            _storeFactory.Object,
            _embeddingService.Object,
            _chatClient.Object,
            Mock.Of<ILogger<KnowledgeIngestionPipeline>>());
    }

    [Fact]
    public async Task IngestAsync_NewCandidate_SavesKnowledgeEntry()
    {
        var candidate = CreateCandidate();
        var context = CreateContext();

        var result = await _pipeline.IngestAsync(candidate, context, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("tenant-1", result.TenantId);
        Assert.Equal("session-1", result.SessionId);
        Assert.Equal("meeting-1", result.MeetingId);
        Assert.Equal(KnowledgeStatus.Draft, result.Status);
        Assert.Equal(KnowledgeType.TacitKnowledge, result.Type);
        Assert.NotNull(result.Embedding);

        _store.Verify(s => s.SaveKnowledgeAsync(It.IsAny<KnowledgeEntry>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task IngestAsync_GeneratesEmbedding()
    {
        var candidate = CreateCandidate();
        var context = CreateContext();

        var result = await _pipeline.IngestAsync(candidate, context, CancellationToken.None);

        _embeddingService.Verify(e => e.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        Assert.NotNull(result.Embedding);
        Assert.Equal(3, result.Embedding!.Length);
    }

    [Fact]
    public async Task IngestAsync_DuplicateDetected_ReturnsExisting()
    {
        var existingEntry = new KnowledgeEntry
        {
            Id = "existing-1",
            Content = "Important technical decision about the architecture",
            TenantId = "tenant-1"
        };

        _store.Setup(s => s.SearchAsync(It.IsAny<string>(), It.IsAny<KnowledgeSearchOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { existingEntry });

        var candidate = CreateCandidate("Important technical decision about the architecture");
        var context = CreateContext();

        var result = await _pipeline.IngestAsync(candidate, context, CancellationToken.None);

        Assert.Equal("existing-1", result.Id);
        _store.Verify(s => s.SaveKnowledgeAsync(It.IsAny<KnowledgeEntry>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task IngestAsync_SetsCorrectCategory()
    {
        var candidate = CreateCandidate();
        candidate = candidate with { Category = TacitKnowledgeCategory.ExpertKnowledge };
        var context = CreateContext();

        var result = await _pipeline.IngestAsync(candidate, context, CancellationToken.None);

        Assert.Equal(TacitKnowledgeCategory.ExpertKnowledge, result.Category);
    }

    [Fact]
    public async Task IngestAsync_SetsMetadata()
    {
        var candidate = CreateCandidate();
        var context = CreateContext();

        var result = await _pipeline.IngestAsync(candidate, context, CancellationToken.None);

        Assert.Equal("Test Meeting", result.MeetingSubject);
        Assert.Equal("ja", result.Language);
        Assert.Equal("speaker-1", result.SourceSpeaker);
    }

    [Fact]
    public async Task IngestAsync_UsesCorrectDataStoreProvider()
    {
        var candidate = CreateCandidate();
        var context = CreateContext() with { DataStoreProvider = "AzureAISearch" };

        await _pipeline.IngestAsync(candidate, context, CancellationToken.None);

        _storeFactory.Verify(f => f.CreateStore("AzureAISearch"), Times.AtLeastOnce);
    }

    [Fact]
    public async Task IngestAsync_LLMFailure_UsesDefaults()
    {
        // Setup LLM to return invalid JSON
        SetupChatResponse("This is not valid JSON");

        var candidate = CreateCandidate("Short content here");
        var context = CreateContext();

        var result = await _pipeline.IngestAsync(candidate, context, CancellationToken.None);

        // Should still save successfully with fallback title/tags
        Assert.NotNull(result);
        _store.Verify(s => s.SaveKnowledgeAsync(It.IsAny<KnowledgeEntry>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateStatusAsync_Confirmed_UpdatesStatus()
    {
        var existing = new KnowledgeEntry
        {
            Id = "k1",
            TenantId = "tenant-1",
            Content = "Original content",
            Title = "Original title",
            Status = KnowledgeStatus.Draft
        };

        _store.Setup(s => s.GetKnowledgeAsync("k1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var result = await _pipeline.UpdateStatusAsync(
            "k1", KnowledgeStatus.Confirmed, "user-1", null, CancellationToken.None);

        Assert.Equal(KnowledgeStatus.Confirmed, result.Status);
        Assert.Equal("user-1", result.ValidatedBy);
        Assert.NotNull(result.ValidatedAt);
        Assert.Equal("Original content", result.Content);

        _store.Verify(s => s.UpdateKnowledgeAsync("k1", It.IsAny<KnowledgeEntry>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateStatusAsync_Edited_RegeneratesEmbedding()
    {
        var existing = new KnowledgeEntry
        {
            Id = "k1",
            TenantId = "tenant-1",
            Content = "Original content",
            Title = "Title",
            Summary = "Summary",
            Status = KnowledgeStatus.Draft
        };

        _store.Setup(s => s.GetKnowledgeAsync("k1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var result = await _pipeline.UpdateStatusAsync(
            "k1", KnowledgeStatus.Edited, "user-1", "Corrected content", CancellationToken.None);

        Assert.Equal(KnowledgeStatus.Edited, result.Status);
        Assert.Equal("Corrected content", result.Content);
        Assert.NotNull(result.Embedding);

        _embeddingService.Verify(e => e.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateStatusAsync_Rejected_DoesNotRegenerateEmbedding()
    {
        var existing = new KnowledgeEntry
        {
            Id = "k1",
            TenantId = "tenant-1",
            Content = "Original content",
            Status = KnowledgeStatus.Draft
        };

        _store.Setup(s => s.GetKnowledgeAsync("k1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var result = await _pipeline.UpdateStatusAsync(
            "k1", KnowledgeStatus.Rejected, "user-1", null, CancellationToken.None);

        Assert.Equal(KnowledgeStatus.Rejected, result.Status);
        _embeddingService.Verify(e => e.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateStatusAsync_NotFound_ThrowsException()
    {
        _store.Setup(s => s.GetKnowledgeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((KnowledgeEntry?)null);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _pipeline.UpdateStatusAsync("missing", KnowledgeStatus.Confirmed, null, null, CancellationToken.None));
    }

    [Theory]
    [InlineData("{\"title\": \"T\", \"summary\": \"S\"}", "T", "S")]
    [InlineData("```json\n{\"title\": \"T2\", \"summary\": \"S2\"}\n```", "T2", "S2")]
    public void ExtractJson_ParsesVariousFormats(string input, string expectedTitle, string expectedSummary)
    {
        var json = KnowledgeIngestionPipeline.ExtractJson(input);
        Assert.Contains(expectedTitle, json);
        Assert.Contains(expectedSummary, json);
    }

    private void SetupChatResponse(string responseText)
    {
        var chatMessage = new ChatMessage(ChatRole.Assistant, responseText);
        var chatResponse = new ChatResponse([chatMessage]);

        // GetResponseAsync(string, ...) is an extension method that internally calls the
        // IList<ChatMessage> overload, so we mock the actual interface method
        _chatClient.Setup(c => c.GetResponseAsync(
                It.IsAny<IList<ChatMessage>>(),
                It.IsAny<ChatOptions?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(chatResponse);
    }

    private static TacitKnowledgeCandidate CreateCandidate(string? content = null)
    {
        return new TacitKnowledgeCandidate
        {
            Category = TacitKnowledgeCategory.DecisionBackground,
            Content = content ?? "This decision was made because of historical performance issues in the legacy system.",
            Context = "Discussion about architecture choices",
            SourceSpeaker = "speaker-1",
            Confidence = 0.9f,
            RelatedTopics = ["architecture", "performance"]
        };
    }

    private static IngestionContext CreateContext()
    {
        return new IngestionContext
        {
            TenantId = "tenant-1",
            SessionId = "session-1",
            MeetingId = "meeting-1",
            MeetingSubject = "Test Meeting",
            MeetingDate = DateTimeOffset.UtcNow,
            Participants = ["Alice", "Bob"],
            Language = "ja",
            DataStoreProvider = "CosmosDB"
        };
    }
}
