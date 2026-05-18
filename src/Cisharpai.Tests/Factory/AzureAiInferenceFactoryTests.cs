using Cisharpai.Azure;
using Cisharpai.Azure.AzureAiInference;
using Microsoft.Extensions.DependencyInjection;

namespace Cisharpai.Tests.Factory;

public sealed class AzureAiInferenceFactoryTests
{
    private ICisharpaiClientFactory _factory = null!;
    private ServiceProvider _provider = null!;

    [SetUp]
    public void SetUp()
    {
        var services = new ServiceCollection();
        services.AddCisharpaiClientFactory()
            .AddAzureAiInferenceSupport();

        _provider = services.BuildServiceProvider();
        _factory = _provider.GetRequiredService<ICisharpaiClientFactory>();
    }

    [TearDown]
    public void TearDown()
    {
        _provider.Dispose();
    }

    [Test]
    public void CreateChatClient_ReturnsAzureAiInferenceClient()
    {
        var config = new AzureAiInferenceClientConfiguration
        {
            ApiKey = "test-key",
            Endpoint = "https://myendpoint.inference.ai.azure.com/",
            ModelId = "Phi-3-mini-4k-instruct"
        };

        var result = _factory.CreateChatCompletionClient(config);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Client, Is.InstanceOf<AzureAiInferenceChatCompletionClient>());
        });
    }

    [Test]
    public void CreateEmbeddingClient_ReturnsAzureAiInferenceEmbeddingClient()
    {
        var config = new AzureAiInferenceClientConfiguration
        {
            ApiKey = "test-key",
            Endpoint = "https://myendpoint.inference.ai.azure.com/",
            ModelId = "cohere-embed-v3"
        };

        var result = _factory.CreateEmbeddingClient(config);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Client, Is.InstanceOf<AzureAiInferenceEmbeddingClient>());
        });
    }

    [Test]
    public void Configuration_DefaultValues()
    {
        var config = new AzureAiInferenceClientConfiguration
        {
            ApiKey = "k",
            Endpoint = "https://test.inference.ai.azure.com/"
        };

        Assert.Multiple(() =>
        {
            Assert.That(config.Provider, Is.EqualTo(CisharpaiProvider.AzureAiInference));
            Assert.That(config.ApiVersion, Is.EqualTo("2024-05-01-preview"));
            Assert.That(config.ModelId, Is.EqualTo(string.Empty));
        });
    }

    [Test]
    public void RegisteredProviders_ContainsAzureAiInference()
    {
        Assert.That(_factory.GetRegisteredProviders(), Does.Contain(CisharpaiProvider.AzureAiInference));
    }
}
