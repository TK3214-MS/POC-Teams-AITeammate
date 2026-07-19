using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace TeamsAITeammate.AI.Services;

public class ResilientChatClient : IChatClient
{
    private readonly IChatClient _primaryClient;
    private readonly IChatClient _fallbackClient;
    private readonly ILogger<ResilientChatClient> _logger;
    private readonly CircuitBreakerState _circuitBreaker = new();

    public ResilientChatClient(
        IChatClient primaryClient,
        IChatClient fallbackClient,
        ILogger<ResilientChatClient> logger)
    {
        _primaryClient = primaryClient;
        _fallbackClient = fallbackClient;
        _logger = logger;
    }

    public ChatClientMetadata Metadata => _primaryClient.GetService<ChatClientMetadata>() ?? new(nameof(ResilientChatClient));

    public async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> chatMessages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        if (_circuitBreaker.IsOpen)
        {
            _logger.LogWarning("Circuit breaker is open, using fallback model");
            return await ExecuteWithFallbackTelemetry(
                () => _fallbackClient.GetResponseAsync(chatMessages, options, cancellationToken),
                usedFallback: true);
        }

        try
        {
            var response = await _primaryClient.GetResponseAsync(chatMessages, options, cancellationToken);
            _circuitBreaker.RecordSuccess();
            return response;
        }
        catch (Exception ex) when (IsTransientError(ex))
        {
            _logger.LogWarning(ex, "Primary model failed with transient error, falling back");
            _circuitBreaker.RecordFailure();

            return await ExecuteWithFallbackTelemetry(
                () => _fallbackClient.GetResponseAsync(chatMessages, options, cancellationToken),
                usedFallback: true);
        }
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> chatMessages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (_circuitBreaker.IsOpen)
        {
            _logger.LogWarning("Circuit breaker is open, using fallback model for streaming");
            await foreach (var update in _fallbackClient.GetStreamingResponseAsync(chatMessages, options, cancellationToken))
            {
                yield return update;
            }
            yield break;
        }

        IAsyncEnumerable<ChatResponseUpdate> stream;
        try
        {
            stream = _primaryClient.GetStreamingResponseAsync(chatMessages, options, cancellationToken);
            // Try to get first element to verify connection works
        }
        catch (Exception ex) when (IsTransientError(ex))
        {
            _logger.LogWarning(ex, "Primary model streaming failed, falling back");
            _circuitBreaker.RecordFailure();
            stream = _fallbackClient.GetStreamingResponseAsync(chatMessages, options, cancellationToken);
        }

        await foreach (var update in stream)
        {
            yield return update;
        }
    }

    public void Dispose()
    {
        _primaryClient.Dispose();
        _fallbackClient.Dispose();
    }

    public object? GetService(Type serviceType, object? serviceKey = null) =>
        _primaryClient.GetService(serviceType, serviceKey);

    private async Task<ChatResponse> ExecuteWithFallbackTelemetry(
        Func<Task<ChatResponse>> action, bool usedFallback)
    {
        var response = await action();
        if (usedFallback)
        {
            _logger.LogInformation("Fallback model response received successfully");
        }
        return response;
    }

    internal static bool IsTransientError(Exception ex)
    {
        if (ex is HttpRequestException httpEx)
        {
            return httpEx.StatusCode is
                System.Net.HttpStatusCode.TooManyRequests or
                System.Net.HttpStatusCode.ServiceUnavailable or
                System.Net.HttpStatusCode.GatewayTimeout or
                System.Net.HttpStatusCode.BadGateway;
        }

        if (ex is TaskCanceledException or TimeoutException)
            return true;

        // Azure OpenAI SDK wraps errors
        if (ex.Message.Contains("429") || ex.Message.Contains("503") ||
            ex.Message.Contains("Rate limit", StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }

    internal class CircuitBreakerState
    {
        private int _failureCount;
        private DateTimeOffset _lastFailureTime = DateTimeOffset.MinValue;
        private const int FailureThreshold = 3;
        private static readonly TimeSpan RecoveryTimeout = TimeSpan.FromMinutes(1);

        public bool IsOpen
        {
            get
            {
                if (_failureCount < FailureThreshold) return false;
                if (DateTimeOffset.UtcNow - _lastFailureTime > RecoveryTimeout)
                {
                    // Half-open: allow a retry
                    return false;
                }
                return true;
            }
        }

        public void RecordFailure()
        {
            Interlocked.Increment(ref _failureCount);
            _lastFailureTime = DateTimeOffset.UtcNow;
        }

        public void RecordSuccess()
        {
            Interlocked.Exchange(ref _failureCount, 0);
        }
    }
}
