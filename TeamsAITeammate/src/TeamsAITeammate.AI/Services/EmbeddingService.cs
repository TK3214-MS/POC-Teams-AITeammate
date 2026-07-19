using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OpenAI.Embeddings;
using TeamsAITeammate.Core.Interfaces;

namespace TeamsAITeammate.AI.Services;

public class EmbeddingService : IEmbeddingService
{
    private readonly EmbeddingClient _embeddingClient;
    private readonly ILogger<EmbeddingService> _logger;
    private readonly int _chunkSize;
    private readonly int _chunkOverlap;

    public EmbeddingService(IConfiguration configuration, ILogger<EmbeddingService> logger)
    {
        _logger = logger;
        var endpoint = configuration["AzureOpenAI:Endpoint"]!;
        var model = configuration["KnowledgeBase:EmbeddingModel"] ?? "text-embedding-3-large";
        _chunkSize = configuration.GetValue("KnowledgeBase:ChunkSize", 1000);
        _chunkOverlap = configuration.GetValue("KnowledgeBase:ChunkOverlap", 200);

        var azureClient = new AzureOpenAIClient(new Uri(endpoint), new DefaultAzureCredential());
        _embeddingClient = azureClient.GetEmbeddingClient(model);
    }

    internal EmbeddingService(
        EmbeddingClient embeddingClient,
        int chunkSize,
        int chunkOverlap,
        ILogger<EmbeddingService> logger)
    {
        _embeddingClient = embeddingClient;
        _chunkSize = chunkSize;
        _chunkOverlap = chunkOverlap;
        _logger = logger;
    }

    public async Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(text))
            return [];

        var response = await _embeddingClient.GenerateEmbeddingAsync(text, cancellationToken: ct);
        var vector = response.Value.ToFloats();
        _logger.LogDebug("Generated embedding with {Dimensions} dimensions", vector.Length);
        return vector.ToArray();
    }

    public async Task<IReadOnlyList<float[]>> GenerateEmbeddingsAsync(
        IReadOnlyList<string> texts, CancellationToken ct)
    {
        if (texts.Count == 0)
            return [];

        var nonEmpty = texts.Where(t => !string.IsNullOrWhiteSpace(t)).ToList();
        if (nonEmpty.Count == 0)
            return texts.Select(_ => Array.Empty<float>()).ToList();

        var response = await _embeddingClient.GenerateEmbeddingsAsync(nonEmpty, cancellationToken: ct);

        var results = new List<float[]>();
        var embeddingIndex = 0;
        foreach (var text in texts)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                results.Add([]);
            }
            else
            {
                var vector = response.Value[embeddingIndex].ToFloats();
                results.Add(vector.ToArray());
                embeddingIndex++;
            }
        }

        _logger.LogDebug("Generated {Count} embeddings", results.Count);
        return results;
    }

    public IReadOnlyList<string> ChunkText(string text)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Length <= _chunkSize)
            return [text];

        var chunks = new List<string>();
        var position = 0;

        while (position < text.Length)
        {
            var end = Math.Min(position + _chunkSize, text.Length);
            var chunk = text[position..end];

            // Try to break at a sentence or word boundary
            if (end < text.Length)
            {
                var lastPeriod = chunk.LastIndexOfAny(['.', '。', '!', '?', '\n']);
                if (lastPeriod > _chunkSize / 2)
                {
                    chunk = chunk[..(lastPeriod + 1)];
                    end = position + lastPeriod + 1;
                }
            }

            chunks.Add(chunk.Trim());
            position = end - _chunkOverlap;
            if (position < 0) position = 0;
            if (end >= text.Length) break;
        }

        return chunks;
    }
}
