using System.Net;
using Cisharpai.Anthropic;

namespace Cisharpai.Tests.Anthropic;

public sealed class AnthropicAuthenticationHandlerTests
{
    [Test]
    public async Task SendAsync_SetsApiKeyHeader()
    {
        var options = new AnthropicClientOptions { ApiKey = "sk-ant-test" };
        var innerHandler = new MockHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));

        var authHandler = new AnthropicAuthenticationHandler(options) { InnerHandler = innerHandler };
        using var client = new HttpClient(authHandler) { BaseAddress = new Uri("https://api.anthropic.com") };

        await client.GetAsync("/v1/messages");

        Assert.That(innerHandler.LastRequest!.Headers.Contains("x-api-key"), Is.True);
        Assert.That(
            innerHandler.LastRequest.Headers.GetValues("x-api-key").First(),
            Is.EqualTo("sk-ant-test"));
    }

    [Test]
    public async Task SendAsync_SetsDefaultAnthropicVersion()
    {
        var options = new AnthropicClientOptions { ApiKey = "sk-ant-test" };
        var innerHandler = new MockHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));

        var authHandler = new AnthropicAuthenticationHandler(options) { InnerHandler = innerHandler };
        using var client = new HttpClient(authHandler) { BaseAddress = new Uri("https://api.anthropic.com") };

        await client.GetAsync("/v1/messages");

        Assert.That(innerHandler.LastRequest!.Headers.Contains("anthropic-version"), Is.True);
        Assert.That(
            innerHandler.LastRequest.Headers.GetValues("anthropic-version").First(),
            Is.EqualTo("2023-06-01"));
    }

    [Test]
    public async Task SendAsync_UsesCustomAnthropicVersion_WhenConfigured()
    {
        var options = new AnthropicClientOptions { ApiKey = "sk-ant-test", ApiVersion = "2024-01-01" };
        var innerHandler = new MockHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));

        var authHandler = new AnthropicAuthenticationHandler(options) { InnerHandler = innerHandler };
        using var client = new HttpClient(authHandler) { BaseAddress = new Uri("https://api.anthropic.com") };

        await client.GetAsync("/v1/messages");

        Assert.That(
            innerHandler.LastRequest!.Headers.GetValues("anthropic-version").First(),
            Is.EqualTo("2024-01-01"));
    }
}
