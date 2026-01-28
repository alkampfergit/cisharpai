using System.Net;
using Cisharpai.OpenAi;

namespace Cisharpai.Tests.OpenAi;

public sealed class OpenAiAuthenticationHandlerTests
{
    [Test]
    public async Task SendAsync_SetsBearerToken()
    {
        var options = new OpenAiClientOptions { ApiKey = "sk-test-key" };
        var innerHandler = new MockHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));

        var authHandler = new OpenAiAuthenticationHandler(options) { InnerHandler = innerHandler };
        using var client = new HttpClient(authHandler) { BaseAddress = new Uri("https://api.openai.com") };

        await client.GetAsync("/v1/models");

        Assert.That(innerHandler.LastRequest!.Headers.Authorization, Is.Not.Null);
        Assert.That(innerHandler.LastRequest.Headers.Authorization!.Scheme, Is.EqualTo("Bearer"));
        Assert.That(innerHandler.LastRequest.Headers.Authorization.Parameter, Is.EqualTo("sk-test-key"));
    }

    [Test]
    public async Task SendAsync_AddsOrganizationHeader_WhenProvided()
    {
        var options = new OpenAiClientOptions { ApiKey = "sk-test", Organization = "org-123" };
        var innerHandler = new MockHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));

        var authHandler = new OpenAiAuthenticationHandler(options) { InnerHandler = innerHandler };
        using var client = new HttpClient(authHandler) { BaseAddress = new Uri("https://api.openai.com") };

        await client.GetAsync("/v1/models");

        Assert.That(
            innerHandler.LastRequest!.Headers.Contains("OpenAI-Organization"),
            Is.True);
        Assert.That(
            innerHandler.LastRequest.Headers.GetValues("OpenAI-Organization").First(),
            Is.EqualTo("org-123"));
    }

    [Test]
    public async Task SendAsync_OmitsOrganizationHeader_WhenNotProvided()
    {
        var options = new OpenAiClientOptions { ApiKey = "sk-test" };
        var innerHandler = new MockHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));

        var authHandler = new OpenAiAuthenticationHandler(options) { InnerHandler = innerHandler };
        using var client = new HttpClient(authHandler) { BaseAddress = new Uri("https://api.openai.com") };

        await client.GetAsync("/v1/models");

        Assert.That(
            innerHandler.LastRequest!.Headers.Contains("OpenAI-Organization"),
            Is.False);
    }
}
