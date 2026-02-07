using System.Net;
using Cisharpai.Azure.AzureOpenAi;
using Cisharpai.Azure.Common;

namespace Cisharpai.Tests.Azure.Common;

public sealed class AzureAuthenticationHandlerTests
{
    [Test]
    public async Task SendAsync_SetsApiKeyHeader_WhenUsingApiKey()
    {
        var options = new AzureOpenAiClientOptions
        {
            Endpoint = "https://myresource.openai.azure.com",
            ApiKey = "my-azure-key",
            DeploymentName = "gpt-4"
        };
        var innerHandler = new MockHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));

        var authHandler = new AzureAuthenticationHandler(options) { InnerHandler = innerHandler };
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
        var options = new AzureOpenAiClientOptions
        {
            Endpoint = "https://myresource.openai.azure.com",
            DeploymentName = "gpt-4"
        };
        var credential = new FakeTokenCredential("fake-token-value");
        var innerHandler = new MockHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));

        var authHandler = new AzureAuthenticationHandler(options, credential) { InnerHandler = innerHandler };
        using var client = new HttpClient(authHandler) { BaseAddress = new Uri("https://myresource.openai.azure.com") };

        await client.GetAsync("/openai/deployments/gpt-4/chat/completions");

        Assert.That(innerHandler.LastRequest!.Headers.Authorization, Is.Not.Null);
        Assert.That(innerHandler.LastRequest.Headers.Authorization!.Scheme, Is.EqualTo("Bearer"));
        Assert.That(innerHandler.LastRequest.Headers.Authorization.Parameter, Is.EqualTo("fake-token-value"));
        Assert.That(innerHandler.LastRequest.Headers.Contains("api-key"), Is.False);
    }
}
