using Microsoft.Extensions.Logging;
using Moq;
using TeamsAITeammate.Core.Interfaces;
using TeamsAITeammate.Core.Models;
using TeamsAITeammate.Infrastructure.Services;

namespace TeamsAITeammate.UnitTests;

public class CardActionHandlerTests
{
    private readonly Mock<IKnowledgeRepository> _knowledge = new();
    private readonly CardActionHandler _handler;

    public CardActionHandlerTests()
    {
        _handler = new CardActionHandler(
            _knowledge.Object,
            Mock.Of<ILogger<CardActionHandler>>());
    }

    [Fact]
    public async Task HandleActionAsync_QuestionAnswer_WithText_SavesKnowledge()
    {
        var data = new Dictionary<string, object>
        {
            ["questionId"] = "q1",
            ["answerText"] = "The approach was chosen for performance reasons."
        };

        var result = await _handler.HandleActionAsync("questionAnswer", data, "session1", CancellationToken.None);

        Assert.True(result.Success);
        _knowledge.Verify(k => k.UpsertAsync(It.Is<KnowledgeEntry>(e =>
            e.SessionId == "session1" &&
            e.Content == "The approach was chosen for performance reasons." &&
            e.Type == KnowledgeType.TacitKnowledge),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleActionAsync_QuestionAnswer_EmptyText_ReturnsFalse()
    {
        var data = new Dictionary<string, object>
        {
            ["questionId"] = "q1",
            ["answerText"] = ""
        };

        var result = await _handler.HandleActionAsync("questionAnswer", data, "session1", CancellationToken.None);

        Assert.False(result.Success);
        _knowledge.Verify(k => k.UpsertAsync(It.IsAny<KnowledgeEntry>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleActionAsync_QuestionSkip_ReturnsSuccess()
    {
        var data = new Dictionary<string, object> { ["questionId"] = "q1" };

        var result = await _handler.HandleActionAsync("questionSkip", data, "session1", CancellationToken.None);

        Assert.True(result.Success);
        Assert.Contains("skipped", result.Message);
    }

    [Fact]
    public async Task HandleActionAsync_QuestionDefer_ReturnsSuccess()
    {
        var data = new Dictionary<string, object> { ["questionId"] = "q1" };

        var result = await _handler.HandleActionAsync("questionDefer", data, "session1", CancellationToken.None);

        Assert.True(result.Success);
        Assert.Contains("deferred", result.Message);
    }

    [Fact]
    public async Task HandleActionAsync_AgendaAccept_ReturnsSuccess()
    {
        var data = new Dictionary<string, object> { ["agendaId"] = "a1" };

        var result = await _handler.HandleActionAsync("agendaAccept", data, "session1", CancellationToken.None);

        Assert.True(result.Success);
        Assert.Contains("accepted", result.Message);
    }

    [Fact]
    public async Task HandleActionAsync_AgendaSkipAll_ReturnsSuccess()
    {
        var result = await _handler.HandleActionAsync("agendaSkipAll", new Dictionary<string, object>(), "session1", CancellationToken.None);

        Assert.True(result.Success);
    }

    [Fact]
    public async Task HandleActionAsync_KnowledgeConfirm_SavesKnowledge()
    {
        var data = new Dictionary<string, object>
        {
            ["candidateId"] = "k1",
            ["content"] = "Important insight"
        };

        var result = await _handler.HandleActionAsync("knowledgeConfirm", data, "session1", CancellationToken.None);

        Assert.True(result.Success);
        _knowledge.Verify(k => k.UpsertAsync(It.Is<KnowledgeEntry>(e =>
            e.Id == "k1" &&
            e.SessionId == "session1" &&
            e.ConfidenceScore == 1.0),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleActionAsync_KnowledgeEdit_WithCorrection_Updates()
    {
        var data = new Dictionary<string, object>
        {
            ["candidateId"] = "k1",
            ["correctionText"] = "Corrected insight text"
        };

        var result = await _handler.HandleActionAsync("knowledgeEdit", data, "session1", CancellationToken.None);

        Assert.True(result.Success);
        _knowledge.Verify(k => k.UpsertAsync(It.Is<KnowledgeEntry>(e =>
            e.Content == "Corrected insight text" &&
            e.UpdatedAt != null),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleActionAsync_KnowledgeEdit_EmptyCorrection_ReturnsFalse()
    {
        var data = new Dictionary<string, object>
        {
            ["candidateId"] = "k1",
            ["correctionText"] = ""
        };

        var result = await _handler.HandleActionAsync("knowledgeEdit", data, "session1", CancellationToken.None);

        Assert.False(result.Success);
    }

    [Fact]
    public async Task HandleActionAsync_KnowledgeReject_ReturnsSuccess()
    {
        var data = new Dictionary<string, object> { ["candidateId"] = "k1" };

        var result = await _handler.HandleActionAsync("knowledgeReject", data, "session1", CancellationToken.None);

        Assert.True(result.Success);
        Assert.Contains("rejected", result.Message);
    }

    [Fact]
    public async Task HandleActionAsync_SettingsUpdate_ReturnsSuccess()
    {
        var data = new Dictionary<string, object>
        {
            ["frequency"] = "high",
            ["proactive"] = "true",
            ["maxInterventions"] = "30"
        };

        var result = await _handler.HandleActionAsync("settingsUpdate", data, "session1", CancellationToken.None);

        Assert.True(result.Success);
    }

    [Fact]
    public async Task HandleActionAsync_SettingsCancel_ReturnsSuccess()
    {
        var result = await _handler.HandleActionAsync("settingsCancel", new Dictionary<string, object>(), "session1", CancellationToken.None);

        Assert.True(result.Success);
    }

    [Fact]
    public async Task HandleActionAsync_UnknownVerb_ReturnsFailure()
    {
        var result = await _handler.HandleActionAsync("unknownAction", new Dictionary<string, object>(), "session1", CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("Unknown action", result.Message);
    }
}
