using Cisharpai.Cohere;
using Microsoft.Extensions.DependencyInjection;

namespace Cisharpai.Tests.Factory;

public sealed class CohereFactoryTests
{
    private ICisharpaiClientFactory _factory = null!;
    private ServiceProvider _provider = null!;

    [SetUp]
    public void SetUp()
    {
        var services = new ServiceCollection();
        services.AddCisharpaiClientFactory()
            .AddCohereSupport();

        _provider = services.BuildServiceProvider();
        _factory = _provider.GetRequiredService<ICisharpaiClientFactory>();
    }

    [TearDown]
    public void TearDown()
    {
        _provider.Dispose();
    }

    [Test]
    public void CreateChatClient_ReturnsCohereClient()
    {
        var config = new CohereClientConfiguration
        {
            ApiKey = "test-key",
            DefaultModel = "command-r-plus"
        };

        var result = _factory.CreateChatCompletionClient(config);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Client, Is.InstanceOf<CohereChatCompletionClient>());
        });
    }

    [Test]
    public void CreateEmbeddingClient_ReturnsCohereEmbeddingClient()
    {
        var config = new CohereClientConfiguration
        {
            ApiKey = "test-key",
            DefaultModel = "embed-english-v3.0"
        };

        var result = _factory.CreateEmbeddingClient(config);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Client, Is.InstanceOf<CohereEmbeddingClient>());
        });
    }

    [Test]
    public void Configuration_DefaultValues()
    {
        var config = new CohereClientConfiguration { ApiKey = "k" };

        Assert.Multiple(() =>
        {
            Assert.That(config.Provider, Is.EqualTo(CisharpaiProvider.Cohere));
            Assert.That(config.BaseUrl, Is.EqualTo("https://api.cohere.com/v2/"));
            Assert.That(config.DefaultModel, Is.Null);
        });
    }

    [Test]
    public void RegisteredProviders_ContainsCohere()
    {
        Assert.That(_factory.GetRegisteredProviders(), Does.Contain(CisharpaiProvider.Cohere));
    }

    [Test]
    public void CreateRerankerClient_ReturnsCohereRerankerClient()
    {
        var config = new CohereClientConfiguration
        {
            ApiKey = "test-key",
            DefaultModel = CohereModels.Rerank.RerankV3_5
        };

        var result = _factory.CreateRerankerClient(config);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Client, Is.InstanceOf<CohereRerankerClient>());
        });
    }

    [Test]
    public void CreateRerankerClient_WithWrongConfigurationType_Fails()
    {
        var result = new CohereClientFactoryProvider()
            .CreateRerankerClient(_provider, new WrongConfiguration { ApiKey = "k" });

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.ErrorMessage, Does.Contain("CohereClientConfiguration"));
        });
    }

    private sealed record WrongConfiguration : CisharpaiClientConfiguration
    {
        public override CisharpaiProvider Provider => CisharpaiProvider.Cohere;
    }
}
