using Cisharpai.Azure;
using Cisharpai.Azure.AzureOpenAi;
using Microsoft.Extensions.DependencyInjection;

namespace Cisharpai.Tests.Factory;

public sealed class AzureOpenAiFactoryTests
{
    private ICisharpaiClientFactory _factory = null!;
    private ServiceProvider _provider = null!;

    [SetUp]
    public void SetUp()
    {
        var services = new ServiceCollection();
        services.AddCisharpaiClientFactory()
            .AddAzureOpenAiSupport();

        _provider = services.BuildServiceProvider();
        _factory = _provider.GetRequiredService<ICisharpaiClientFactory>();
    }

    [TearDown]
    public void TearDown()
    {
        _provider.Dispose();
    }

    [Test]
    public void CreateChatClient_ReturnsAzureOpenAiClient()
    {
        var config = new AzureOpenAiClientConfiguration
        {
            ApiKey = "test-key",
            Endpoint = "https://myresource.openai.azure.com/",
            DeploymentName = "gpt-4o"
        };

        var result = _factory.CreateChatCompletionClient(config);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Client, Is.InstanceOf<AzureOpenAiChatCompletionClient>());
        });
    }

    [Test]
    public void CreateEmbeddingClient_ReturnsAzureOpenAiEmbeddingClient()
    {
        var config = new AzureOpenAiClientConfiguration
        {
            ApiKey = "test-key",
            Endpoint = "https://myresource.openai.azure.com/",
            DeploymentName = "text-embedding-ada-002"
        };

        var result = _factory.CreateEmbeddingClient(config);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Client, Is.InstanceOf<AzureOpenAiEmbeddingClient>());
        });
    }

    [Test]
    public void CreateChatClient_WithAllOptions_Succeeds()
    {
        var config = new AzureOpenAiClientConfiguration
        {
            ApiKey = "test-key",
            Endpoint = "https://myresource.openai.azure.com/",
            DeploymentName = "gpt-5",
            ApiVersion = "2024-10-21",
            DefaultModel = "gpt-5",
            ModelName = "gpt-5",
            ReasoningEffort = "high",
            TextVerbosity = "low"
        };

        var result = _factory.CreateChatCompletionClient(config);

        Assert.That(result.IsSuccess, Is.True);
    }

    [Test]
    public void Configuration_DefaultValues()
    {
        var config = new AzureOpenAiClientConfiguration
        {
            ApiKey = "k",
            Endpoint = "https://test.openai.azure.com/",
            DeploymentName = "dep"
        };

        Assert.Multiple(() =>
        {
            Assert.That(config.Provider, Is.EqualTo(CisharpaiProvider.AzureOpenAi));
            Assert.That(config.ApiVersion, Is.EqualTo("2024-10-21"));
            Assert.That(config.DefaultModel, Is.Null);
            Assert.That(config.ModelName, Is.Null);
        });
    }

    [Test]
    public void RegisteredProviders_ContainsAzureOpenAi()
    {
        Assert.That(_factory.GetRegisteredProviders(), Does.Contain(CisharpaiProvider.AzureOpenAi));
    }
}
