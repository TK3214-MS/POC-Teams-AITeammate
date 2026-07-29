using Microsoft.Extensions.Configuration;
using Moq;
using Moq.Protected;
using TeamsAITeammate.Infrastructure.Services;

namespace TeamsAITeammate.UnitTests;

public class SpeechTokenServiceTests
{
    [Fact]
    public async Task GetAuthorizationAsync_UsesKeyOnlyForTokenExchange()
    {
        HttpRequestMessage? capturedRequest = null;
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((request, _) => capturedRequest = request)
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = System.Net.HttpStatusCode.OK,
                Content = new StringContent("short-lived-token"),
            });
        var client = new HttpClient(handler.Object);
        var clientFactory = new Mock<IHttpClientFactory>();
        clientFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(client);
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Speech:Endpoint"] = "https://speech-test.cognitiveservices.azure.com/",
                ["Speech:Key"] = "server-only-key",
                ["Speech:Region"] = "japaneast",
            })
            .Build();
        var service = new SpeechTokenService(clientFactory.Object, configuration);

        var authorization = await service.GetAuthorizationAsync();

        Assert.Equal("short-lived-token", authorization.Token);
        Assert.Equal("japaneast", authorization.Region);
        Assert.Equal(
            "https://speech-test.cognitiveservices.azure.com/sts/v1.0/issueToken",
            capturedRequest?.RequestUri?.ToString());
        Assert.Equal(
            "server-only-key",
            capturedRequest?.Headers.GetValues("Ocp-Apim-Subscription-Key").Single());
        Assert.InRange(
            authorization.ExpiresAt,
            DateTimeOffset.UtcNow.AddMinutes(8),
            DateTimeOffset.UtcNow.AddMinutes(10));
    }
}