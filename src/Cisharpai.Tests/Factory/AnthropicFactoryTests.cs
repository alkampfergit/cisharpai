using System.Net;
using System.Text.Json;
using Cisharpai.Anthropic;
using Cisharpai.Features.Chat;
using Cisharpai.Models;
using Microsoft.Extensions.DependencyInjection;

namespace Cisharpai.Tests.Factory;

public sealed class AnthropicFactoryTests
{
    private ICisharpaiClientFactory _factory = null!;
    private ServiceProvider _provider = null!;
    private string? _capturedRequestBody;

    private const string MinimalWebSearchResponse = """
        {
            "model": "claude-sonnet-4-20250514",
            "content": [{ "type": "text", "text": "Answer." }],
            "usage": { "input_tokens": 10, "output_tokens": 5, "server_tool_use": { "web_search_requests": 1 } },
            "stop_reason": "end_turn"
        }
        """;

    [SetUp]
    public void SetUp()
    {
        _capturedRequestBody = null;
        var services = new ServiceCollection();
        services.AddCisharpaiClientFactory()
            .AddAnthropicSupport();

        services.AddHttpClient("CisharpaiFactory_Anthropic_Chat")
            .ConfigurePrimaryHttpMessageHandler(() => new MockHttpMessageHandler(async (req, _) =>
            {
                _capturedRequestBody = await req.Content!.ReadAsStringAsync(CancellationToken.None);
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(MinimalWebSearchResponse, System.Text.Encoding.UTF8, "application/json")
                };
            }));

        _provider = services.BuildServiceProvider();
        _factory = _provider.GetRequiredService<ICisharpaiClientFactory>();
    }

    [TearDown]
    public void TearDown()
    {
        _provider.Dispose();
    }

    [Test]
    public void CreateChatClient_ReturnsAnthropicClient()
    {
        var config = new AnthropicClientConfiguration
        {
            ApiKey = "test-key",
            DefaultModel = "claude-sonnet-4-20250514"
        };

        var result = _factory.CreateChatCompletionClient(config);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Client, Is.InstanceOf<AnthropicChatCompletionClient>());
        });
    }

    [Test]
    public void CreateChatClient_WithCustomBaseUrlAndVersion_Succeeds()
    {
        var config = new AnthropicClientConfiguration
        {
            ApiKey = "test-key",
            BaseUrl = "https://custom.anthropic.com/v1/",
            ApiVersion = "2024-01-01"
        };

        var result = _factory.CreateChatCompletionClient(config);

        Assert.That(result.IsSuccess, Is.True);
    }

    [Test]
    public void CreateEmbeddingClient_ReturnsFailure()
    {
        var config = new AnthropicClientConfiguration { ApiKey = "test-key" };

        var result = _factory.CreateEmbeddingClient(config);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.ErrorMessage, Does.Contain("does not support embedding"));
            Assert.That(result.Client, Is.Null);
        });
    }

    [Test]
    public void RegisteredProviders_ContainsAnthropic()
    {
        Assert.That(_factory.GetRegisteredProviders(), Does.Contain(CisharpaiProvider.Anthropic));
    }

    [Test]
    public void Configuration_DefaultValues()
    {
        var config = new AnthropicClientConfiguration { ApiKey = "k" };

        Assert.Multiple(() =>
        {
            Assert.That(config.Provider, Is.EqualTo(CisharpaiProvider.Anthropic));
            Assert.That(config.BaseUrl, Is.EqualTo("https://api.anthropic.com/v1/"));
            Assert.That(config.ApiVersion, Is.EqualTo("2023-06-01"));
            Assert.That(config.DefaultModel, Is.Null);
            Assert.That(config.WebSearchToolVersion, Is.EqualTo("web_search_20260209"));
        });
    }

    [Test]
    public async Task WebSearchToolVersion_FactoryPath_ConfiguredVersionReachesEmittedJson()
    {
        var config = new AnthropicClientConfiguration
        {
            ApiKey = "test-key",
            WebSearchToolVersion = "web_search_20270101"
        };

        var result = _factory.CreateChatCompletionClient(config);
        Assert.That(result.IsSuccess, Is.True);

        var webSearch = result.Client!.Features.Get<IWebSearchFeature>();
        Assert.That(webSearch, Is.Not.Null);

        await webSearch!.GetChatCompletionWithWebSearchAsync(
            new ChatCompletionRequest(
                Messages: [new LlmMessage(LlmRole.User, "test")],
                Model: "claude-sonnet-4-20250514"),
            new WebSearchOptions());

        Assert.That(_capturedRequestBody, Is.Not.Null);
        var doc = JsonDocument.Parse(_capturedRequestBody!);
        var tool = doc.RootElement.GetProperty("tools")[0];
        Assert.That(tool.GetProperty("type").GetString(), Is.EqualTo("web_search_20270101"));
    }
}
