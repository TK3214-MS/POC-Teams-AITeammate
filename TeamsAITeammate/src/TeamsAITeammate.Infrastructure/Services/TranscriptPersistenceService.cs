using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Specialized;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TeamsAITeammate.Core.Interfaces;
using TeamsAITeammate.Core.Models;

namespace TeamsAITeammate.Infrastructure.Services;

public class TranscriptPersistenceService : ITranscriptPersistence
{
    private readonly BlobContainerClient _containerClient;
    private readonly ILogger<TranscriptPersistenceService> _logger;
    private readonly ConcurrentDictionary<string, PersistenceState> _states = new();
    private readonly TimeSpan _flushInterval = TimeSpan.FromSeconds(30);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public TranscriptPersistenceService(
        BlobServiceClient blobServiceClient,
        IConfiguration configuration,
        ILogger<TranscriptPersistenceService> logger)
    {
        var containerName = configuration["BlobStorage:TranscriptContainerName"] ?? "transcripts";
        _containerClient = blobServiceClient.GetBlobContainerClient(containerName);
        _logger = logger;
    }

    public async Task AppendSegmentAsync(
        string tenantId, string meetingId, string sessionId,
        TranscriptSegment segment, CancellationToken ct = default)
    {
        var state = _states.GetOrAdd(sessionId, _ => new PersistenceState
        {
            TenantId = tenantId,
            MeetingId = meetingId,
            SessionId = sessionId,
        });

        var jsonLine = JsonSerializer.Serialize(segment, JsonOptions) + "\n";

        lock (state.Buffer)
        {
            state.Buffer.Append(jsonLine);
        }

        if (DateTimeOffset.UtcNow - state.LastFlushed >= _flushInterval)
        {
            await FlushAsync(sessionId, ct);
        }
    }

    public async Task FlushAsync(string sessionId, CancellationToken ct = default)
    {
        if (!_states.TryGetValue(sessionId, out var state))
            return;

        string content;
        lock (state.Buffer)
        {
            if (state.Buffer.Length == 0)
                return;

            content = state.Buffer.ToString();
            state.Buffer.Clear();
        }

        var blobPath = BuildBlobPath(state);
        var blobClient = _containerClient.GetAppendBlobClient(blobPath);

        try
        {
            await blobClient.CreateIfNotExistsAsync(cancellationToken: ct);

            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));
            await blobClient.AppendBlockAsync(stream, cancellationToken: ct);

            state.LastFlushed = DateTimeOffset.UtcNow;
            _logger.LogDebug("Flushed {Length} bytes for session {SessionId}", content.Length, sessionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to flush transcript for session {SessionId}", sessionId);
            // Re-buffer the content for retry
            lock (state.Buffer)
            {
                state.Buffer.Insert(0, content);
            }
        }
    }

    public async Task FinalizeAsync(string sessionId, CancellationToken ct = default)
    {
        await FlushAsync(sessionId, ct);
        _states.TryRemove(sessionId, out _);
        _logger.LogInformation("Finalized transcript persistence for session {SessionId}", sessionId);
    }

    private static string BuildBlobPath(PersistenceState state)
    {
        var now = DateTimeOffset.UtcNow;
        return $"{state.TenantId}/{now.Year:D4}/{now.Month:D2}/{state.MeetingId}/{state.SessionId}.jsonl";
    }

    private sealed class PersistenceState
    {
        public string TenantId { get; init; } = string.Empty;
        public string MeetingId { get; init; } = string.Empty;
        public string SessionId { get; init; } = string.Empty;
        public StringBuilder Buffer { get; } = new();
        public DateTimeOffset LastFlushed { get; set; } = DateTimeOffset.UtcNow;
    }
}
