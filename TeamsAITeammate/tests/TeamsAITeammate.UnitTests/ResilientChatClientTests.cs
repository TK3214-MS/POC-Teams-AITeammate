using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Moq;
using TeamsAITeammate.AI.Services;

namespace TeamsAITeammate.UnitTests;

public class ResilientChatClientTests
{
    private readonly Mock<IChatClient> _mockPrimary;
    private readonly Mock<IChatClient> _mockFallback;
    private readonly ResilientChatClient _client;

    public ResilientChatClientTests()
    {
        _mockPrimary = new Mock<IChatClient>();
        _mockFallback = new Mock<IChatClient>();
        _client = new ResilientChatClient(
            _mockPrimary.Object,
            _mockFallback.Object,
            new Mock<ILogger<ResilientChatClient>>().Object);
    }

    [Fact]
    public async Task GetResponseAsync_PrimarySucceeds_ReturnsPrimaryResponse()
    {
        var expected = new ChatResponse(new ChatMessage(ChatRole.Assistant, "Primary response"));
        _mockPrimary.Setup(c => c.GetResponseAsync(
            It.IsAny<IEnumerable<ChatMessage>>(),
            It.IsAny<ChatOptions>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var messages = new[] { new ChatMessage(ChatRole.User, "Hello") };
        var result = await _client.GetResponseAsync(messages);

        Assert.Equal("Primary response", result.Text);
        _mockFallback.Verify(c => c.GetResponseAsync(
            It.IsAny<IEnumerable<ChatMessage>>(),
            It.IsAny<ChatOptions>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetResponseAsync_PrimaryThrows429_UsesFallback()
    {
        _mockPrimary.Setup(c => c.GetResponseAsync(
            It.IsAny<IEnumerable<ChatMessage>>(),
            It.IsAny<ChatOptions>(),
            It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Rate limited", null, System.Net.HttpStatusCode.TooManyRequests));

        var fallbackResponse = new ChatResponse(new ChatMessage(ChatRole.Assistant, "Fallback response"));
        _mockFallback.Setup(c => c.GetResponseAsync(
            It.IsAny<IEnumerable<ChatMessage>>(),
            It.IsAny<ChatOptions>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(fallbackResponse);

        var messages = new[] { new ChatMessage(ChatRole.User, "Hello") };
        var result = await _client.GetResponseAsync(messages);

        Assert.Equal("Fallback response", result.Text);
    }

    [Fact]
    public async Task GetResponseAsync_PrimaryThrows503_UsesFallback()
    {
        _mockPrimary.Setup(c => c.GetResponseAsync(
            It.IsAny<IEnumerable<ChatMessage>>(),
            It.IsAny<ChatOptions>(),
            It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Unavailable", null, System.Net.HttpStatusCode.ServiceUnavailable));

        var fallbackResponse = new ChatResponse(new ChatMessage(ChatRole.Assistant, "Fallback"));
        _mockFallback.Setup(c => c.GetResponseAsync(
            It.IsAny<IEnumerable<ChatMessage>>(),
            It.IsAny<ChatOptions>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(fallbackResponse);

        var messages = new[] { new ChatMessage(ChatRole.User, "Hello") };
        var result = await _client.GetResponseAsync(messages);

        Assert.Equal("Fallback", result.Text);
    }

    [Fact]
    public async Task GetResponseAsync_PrimaryThrowsNonTransient_Throws()
    {
        _mockPrimary.Setup(c => c.GetResponseAsync(
            It.IsAny<IEnumerable<ChatMessage>>(),
            It.IsAny<ChatOptions>(),
            It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Bad config"));

        var messages = new[] { new ChatMessage(ChatRole.User, "Hello") };
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _client.GetResponseAsync(messages));

        _mockFallback.Verify(c => c.GetResponseAsync(
            It.IsAny<IEnumerable<ChatMessage>>(),
            It.IsAny<ChatOptions>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void IsTransientError_429_ReturnsTrue()
    {
        var ex = new HttpRequestException("", null, System.Net.HttpStatusCode.TooManyRequests);
        Assert.True(ResilientChatClient.IsTransientError(ex));
    }

    [Fact]
    public void IsTransientError_503_ReturnsTrue()
    {
        var ex = new HttpRequestException("", null, System.Net.HttpStatusCode.ServiceUnavailable);
        Assert.True(ResilientChatClient.IsTransientError(ex));
    }

    [Fact]
    public void IsTransientError_502_ReturnsTrue()
    {
        var ex = new HttpRequestException("", null, System.Net.HttpStatusCode.BadGateway);
        Assert.True(ResilientChatClient.IsTransientError(ex));
    }

    [Fact]
    public void IsTransientError_504_ReturnsTrue()
    {
        var ex = new HttpRequestException("", null, System.Net.HttpStatusCode.GatewayTimeout);
        Assert.True(ResilientChatClient.IsTransientError(ex));
    }

    [Fact]
    public void IsTransientError_TimeoutException_ReturnsTrue()
    {
        Assert.True(ResilientChatClient.IsTransientError(new TimeoutException()));
    }

    [Fact]
    public void IsTransientError_TaskCanceled_ReturnsTrue()
    {
        Assert.True(ResilientChatClient.IsTransientError(new TaskCanceledException()));
    }

    [Fact]
    public void IsTransientError_RateLimitMessage_ReturnsTrue()
    {
        var ex = new Exception("Rate limit exceeded");
        Assert.True(ResilientChatClient.IsTransientError(ex));
    }

    [Fact]
    public void IsTransientError_GenericException_ReturnsFalse()
    {
        Assert.False(ResilientChatClient.IsTransientError(new InvalidOperationException("bad")));
    }

    [Fact]
    public void IsTransientError_400_ReturnsFalse()
    {
        var ex = new HttpRequestException("", null, System.Net.HttpStatusCode.BadRequest);
        Assert.False(ResilientChatClient.IsTransientError(ex));
    }

    [Fact]
    public void CircuitBreaker_OpensAfterThreshold()
    {
        var cb = new ResilientChatClient.CircuitBreakerState();

        Assert.False(cb.IsOpen);

        cb.RecordFailure();
        Assert.False(cb.IsOpen);

        cb.RecordFailure();
        Assert.False(cb.IsOpen);

        cb.RecordFailure(); // 3rd failure = threshold
        Assert.True(cb.IsOpen);
    }

    [Fact]
    public void CircuitBreaker_ResetsOnSuccess()
    {
        var cb = new ResilientChatClient.CircuitBreakerState();

        cb.RecordFailure();
        cb.RecordFailure();
        cb.RecordSuccess(); // Reset

        Assert.False(cb.IsOpen);

        cb.RecordFailure(); // 1st failure after reset
        Assert.False(cb.IsOpen);
    }

    [Fact]
    public void Dispose_DisposeBothClients()
    {
        _client.Dispose();

        _mockPrimary.Verify(c => c.Dispose(), Times.Once);
        _mockFallback.Verify(c => c.Dispose(), Times.Once);
    }
}
