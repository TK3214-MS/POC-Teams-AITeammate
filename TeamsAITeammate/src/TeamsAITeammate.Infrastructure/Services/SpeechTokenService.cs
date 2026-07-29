using Microsoft.Extensions.Configuration;
using TeamsAITeammate.Core.Interfaces;

namespace TeamsAITeammate.Infrastructure.Services;

public class SpeechTokenService : ISpeechTokenService
{
    private static readonly TimeSpan TokenLifetime = TimeSpan.FromMinutes(9);
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly string _endpoint;
    private readonly string _key;
    private readonly string _region;

    public SpeechTokenService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration)
    {
        _httpClientFactory = httpClientFactory;
        _endpoint = configuration["Speech:Endpoint"]
            ?? throw new InvalidOperationException("Speech:Endpoint is not configured.");
        _key = configuration["Speech:Key"]
            ?? throw new InvalidOperationException("Speech:Key is not configured.");
        _region = configuration["Speech:Region"]
            ?? throw new InvalidOperationException("Speech:Region is not configured.");
    }

    public async Task<SpeechAuthorization> GetAuthorizationAsync(CancellationToken ct = default)
    {
        var issueTokenUri = new Uri(new Uri(_endpoint.TrimEnd('/') + "/"), "sts/v1.0/issueToken");
        using var request = new HttpRequestMessage(HttpMethod.Post, issueTokenUri);
        request.Headers.Add("Ocp-Apim-Subscription-Key", _key);
        using var response = await _httpClientFactory.CreateClient().SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        var token = await response.Content.ReadAsStringAsync(ct);
        return new SpeechAuthorization(token, _region, DateTimeOffset.UtcNow.Add(TokenLifetime));
    }
}