using Cisharpai.Anthropic;
using Microsoft.Extensions.DependencyInjection;

namespace Cisharpai.Tests.Factory;

public sealed class AnthropicFactoryTests
{
    private ICisharpaiClientFactory _factory = null!;
    private ServiceProvider _provider = null!;

    [SetUp]
    public void SetUp()
    {
        var services = new ServiceCollection();
        services.AddCisharpaiClientFactory()
            .AddAnthropicSupport();

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
        });
    }
}
