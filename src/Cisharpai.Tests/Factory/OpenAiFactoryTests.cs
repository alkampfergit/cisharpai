using Cisharpai.OpenAi;
using Microsoft.Extensions.DependencyInjection;

namespace Cisharpai.Tests.Factory;

public sealed class OpenAiFactoryTests
{
    private ICisharpaiClientFactory _factory = null!;
    private ServiceProvider _provider = null!;

    [SetUp]
    public void SetUp()
    {
        var services = new ServiceCollection();
        services.AddCisharpaiClientFactory()
            .AddOpenAiSupport();

        _provider = services.BuildServiceProvider();
        _factory = _provider.GetRequiredService<ICisharpaiClientFactory>();
    }

    [TearDown]
    public void TearDown()
    {
        _provider.Dispose();
    }

    [Test]
    public void CreateChatClient_ReturnsOpenAiClient()
    {
        var config = new OpenAiClientConfiguration
        {
            ApiKey = "test-key",
            DefaultModel = "gpt-4o"
        };

        var result = _factory.CreateChatCompletionClient(config);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Client, Is.InstanceOf<OpenAiChatCompletionClient>());
        });
    }

    [Test]
    public void CreateEmbeddingClient_ReturnsOpenAiEmbeddingClient()
    {
        var config = new OpenAiClientConfiguration
        {
            ApiKey = "test-key",
            DefaultModel = "text-embedding-3-small"
        };

        var result = _factory.CreateEmbeddingClient(config);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Client, Is.InstanceOf<OpenAiEmbeddingClient>());
        });
    }

    [Test]
    public void CreateChatClient_WithAllOptions_Succeeds()
    {
        var config = new OpenAiClientConfiguration
        {
            ApiKey = "test-key",
            BaseUrl = "https://custom.openai.com/v1/",
            DefaultModel = "gpt-4o",
            Organization = "org-test",
            ReasoningEffort = "high",
            TextVerbosity = "low"
        };

        var result = _factory.CreateChatCompletionClient(config);

        Assert.That(result.IsSuccess, Is.True);
    }

    [Test]
    public void MultipleClients_AreIndependentInstances()
    {
        var config1 = new OpenAiClientConfiguration { ApiKey = "key-1" };
        var config2 = new OpenAiClientConfiguration { ApiKey = "key-2" };

        var result1 = _factory.CreateChatCompletionClient(config1);
        var result2 = _factory.CreateChatCompletionClient(config2);

        Assert.Multiple(() =>
        {
            Assert.That(result1.IsSuccess, Is.True);
            Assert.That(result2.IsSuccess, Is.True);
            Assert.That(result1.Client, Is.Not.SameAs(result2.Client));
        });
    }

    [Test]
    public void RegisteredProviders_ContainsOpenAi()
    {
        var registered = _factory.GetRegisteredProviders();

        Assert.That(registered, Does.Contain(CisharpaiProvider.OpenAi));
    }
}
