using System.Net;
using cisharpai.AzureOpenAi;

namespace cisharpai.test.AzureOpenAi;

public sealed class AzureOpenAiAuthenticationHandlerTests
{
    [Test]
    public async Task SendAsync_SetsApiKeyHeader_WhenUsingApiKey()
    {
        var options = new AzureOpenAiClientOptions { ApiKey = "my-azure-key" };
        var innerHandler = new MockHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));

        var authHandler = new AzureOpenAiAuthenticationHandler(options) { InnerHandler = innerHandler };
        using var client = new HttpClient(authHandler) { BaseAddress = new Uri("https://myresource.openai.azure.com") };

        await client.GetAsync("/openai/deployments/gpt-4/chat/completions");

        Assert.That(innerHandler.LastRequest!.Headers.Contains("api-key"), Is.True);
        Assert.That(
            innerHandler.LastRequest.Headers.GetValues("api-key").First(),
            Is.EqualTo("my-azure-key"));
        Assert.That(innerHandler.LastRequest.Headers.Authorization, Is.Null);
    }

    [Test]
    public async Task SendAsync_SetsBearerToken_WhenUsingTokenCredential()
    {
        var options = new AzureOpenAiClientOptions();
        var credential = new FakeTokenCredential("fake-token-value");
        var innerHandler = new MockHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));

        var authHandler = new AzureOpenAiAuthenticationHandler(options, credential) { InnerHandler = innerHandler };
        using var client = new HttpClient(authHandler) { BaseAddress = new Uri("https://myresource.openai.azure.com") };

        await client.GetAsync("/openai/deployments/gpt-4/chat/completions");

        Assert.That(innerHandler.LastRequest!.Headers.Authorization, Is.Not.Null);
        Assert.That(innerHandler.LastRequest.Headers.Authorization!.Scheme, Is.EqualTo("Bearer"));
        Assert.That(innerHandler.LastRequest.Headers.Authorization.Parameter, Is.EqualTo("fake-token-value"));
        Assert.That(innerHandler.LastRequest.Headers.Contains("api-key"), Is.False);
    }
}
