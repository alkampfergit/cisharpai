using Cisharpai.Features;
using Cisharpai.Features.Embeddings;
using Cisharpai.OpenAi;
using Cisharpai.Anthropic;
using Cisharpai.Cohere;
using Cisharpai.Azure.AzureOpenAi;
using Cisharpai.Azure.AzureAiInference;

namespace Cisharpai.Tests.Features;

public sealed class FeatureDiscoveryTests
{
    // --- OpenAI clients: no image embedding feature ---

    [Test]
    public void OpenAiChatCompletionClient_Features_IsNotNull()
    {
        using var httpClient = new HttpClient { BaseAddress = new Uri("https://api.openai.com/v1/") };
        var client = new OpenAiChatCompletionClient(httpClient, new OpenAiClientOptions());

        Assert.That(client.Features, Is.Not.Null);
    }

    [Test]
    public void OpenAiChatCompletionClient_DoesNotExposeImageEmbeddingFeature()
    {
        using var httpClient = new HttpClient { BaseAddress = new Uri("https://api.openai.com/v1/") };
        var client = new OpenAiChatCompletionClient(httpClient, new OpenAiClientOptions());

        Assert.That(client.Features.Get<IImageEmbeddingFeature>(), Is.Null);
    }

    [Test]
    public void OpenAiEmbeddingClient_Features_IsNotNull()
    {
        using var httpClient = new HttpClient { BaseAddress = new Uri("https://api.openai.com/v1/") };
        var client = new OpenAiEmbeddingClient(httpClient);

        Assert.That(client.Features, Is.Not.Null);
    }

    [Test]
    public void OpenAiEmbeddingClient_DoesNotExposeImageEmbeddingFeature()
    {
        using var httpClient = new HttpClient { BaseAddress = new Uri("https://api.openai.com/v1/") };
        var client = new OpenAiEmbeddingClient(httpClient);

        Assert.That(client.Features.Get<IImageEmbeddingFeature>(), Is.Null);
    }

    // --- Anthropic client: no image embedding feature ---

    [Test]
    public void AnthropicChatCompletionClient_Features_IsNotNull()
    {
        using var httpClient = new HttpClient { BaseAddress = new Uri("https://api.anthropic.com/v1/") };
        var client = new AnthropicChatCompletionClient(httpClient);

        Assert.That(client.Features, Is.Not.Null);
    }

    [Test]
    public void AnthropicChatCompletionClient_DoesNotExposeImageEmbeddingFeature()
    {
        using var httpClient = new HttpClient { BaseAddress = new Uri("https://api.anthropic.com/v1/") };
        var client = new AnthropicChatCompletionClient(httpClient);

        Assert.That(client.Features.Get<IImageEmbeddingFeature>(), Is.Null);
    }

    // --- Cohere client: exposes image embedding feature ---

    [Test]
    public void CohereEmbeddingClient_Features_IsNotNull()
    {
        using var httpClient = new HttpClient { BaseAddress = new Uri("https://api.cohere.com/v2/") };
        var client = new CohereEmbeddingClient(httpClient);

        Assert.That(client.Features, Is.Not.Null);
    }

    [Test]
    public void CohereEmbeddingClient_ExposesImageEmbeddingFeature()
    {
        using var httpClient = new HttpClient { BaseAddress = new Uri("https://api.cohere.com/v2/") };
        var client = new CohereEmbeddingClient(httpClient);

        var feature = client.Features.Get<IImageEmbeddingFeature>();

        Assert.That(feature, Is.Not.Null);
        Assert.That(feature, Is.SameAs(client));
    }

    // --- Azure OpenAI clients: no image embedding feature ---

    [Test]
    public void AzureOpenAiChatCompletionClient_Features_IsNotNull()
    {
        using var httpClient = new HttpClient { BaseAddress = new Uri("https://test.openai.azure.com/") };
        var options = new AzureOpenAiClientOptions { DeploymentName = "test", ApiKey = "key" };
        var client = new AzureOpenAiChatCompletionClient(httpClient, options);

        Assert.That(client.Features, Is.Not.Null);
    }

    [Test]
    public void AzureOpenAiChatCompletionClient_DoesNotExposeImageEmbeddingFeature()
    {
        using var httpClient = new HttpClient { BaseAddress = new Uri("https://test.openai.azure.com/") };
        var options = new AzureOpenAiClientOptions { DeploymentName = "test", ApiKey = "key" };
        var client = new AzureOpenAiChatCompletionClient(httpClient, options);

        Assert.That(client.Features.Get<IImageEmbeddingFeature>(), Is.Null);
    }

    [Test]
    public void AzureOpenAiEmbeddingClient_Features_IsNotNull()
    {
        using var httpClient = new HttpClient { BaseAddress = new Uri("https://test.openai.azure.com/") };
        var options = new AzureOpenAiClientOptions { DeploymentName = "test", ApiKey = "key" };
        var client = new AzureOpenAiEmbeddingClient(httpClient, options);

        Assert.That(client.Features, Is.Not.Null);
    }

    [Test]
    public void AzureOpenAiEmbeddingClient_DoesNotExposeImageEmbeddingFeature()
    {
        using var httpClient = new HttpClient { BaseAddress = new Uri("https://test.openai.azure.com/") };
        var options = new AzureOpenAiClientOptions { DeploymentName = "test", ApiKey = "key" };
        var client = new AzureOpenAiEmbeddingClient(httpClient, options);

        Assert.That(client.Features.Get<IImageEmbeddingFeature>(), Is.Null);
    }

    // --- Azure AI Inference clients ---

    [Test]
    public void AzureAiInferenceChatCompletionClient_Features_IsNotNull()
    {
        using var httpClient = new HttpClient { BaseAddress = new Uri("https://test.inference.azure.com/") };
        var options = new AzureAiInferenceClientOptions { ModelId = "test-model", ApiKey = "key" };
        var client = new AzureAiInferenceChatCompletionClient(httpClient, options);

        Assert.That(client.Features, Is.Not.Null);
    }

    [Test]
    public void AzureAiInferenceChatCompletionClient_DoesNotExposeImageEmbeddingFeature()
    {
        using var httpClient = new HttpClient { BaseAddress = new Uri("https://test.inference.azure.com/") };
        var options = new AzureAiInferenceClientOptions { ModelId = "test-model", ApiKey = "key" };
        var client = new AzureAiInferenceChatCompletionClient(httpClient, options);

        Assert.That(client.Features.Get<IImageEmbeddingFeature>(), Is.Null);
    }

    [Test]
    public void AzureAiInferenceEmbeddingClient_Features_IsNotNull()
    {
        using var httpClient = new HttpClient { BaseAddress = new Uri("https://test.inference.azure.com/") };
        var options = new AzureAiInferenceClientOptions { ModelId = "test-model", ApiKey = "key" };
        var client = new AzureAiInferenceEmbeddingClient(httpClient, options);

        Assert.That(client.Features, Is.Not.Null);
    }

    [Test]
    public void AzureAiInferenceEmbeddingClient_ExposesImageEmbeddingFeature()
    {
        using var httpClient = new HttpClient { BaseAddress = new Uri("https://test.inference.azure.com/") };
        var options = new AzureAiInferenceClientOptions { ModelId = "test-model", ApiKey = "key" };
        var client = new AzureAiInferenceEmbeddingClient(httpClient, options);

        var feature = client.Features.Get<IImageEmbeddingFeature>();

        Assert.That(feature, Is.Not.Null);
        Assert.That(feature, Is.SameAs(client));
    }

    [Test]
    public void CohereEmbeddingClient_ExposesMultimodalEmbeddingFeature()
    {
        using var httpClient = new HttpClient { BaseAddress = new Uri("https://api.cohere.com/v2/") };
        var client = new CohereEmbeddingClient(httpClient);

        var feature = client.Features.Get<IMultimodalEmbeddingFeature>();

        Assert.That(feature, Is.Not.Null);
        Assert.That(feature, Is.SameAs(client));
    }

    // --- Generic feature discovery pattern ---

    [Test]
    public void FeatureDiscovery_ViaInterface_WorksForCohereClient()
    {
        using var httpClient = new HttpClient { BaseAddress = new Uri("https://api.cohere.com/v2/") };
        IEmbeddingClient client = new CohereEmbeddingClient(httpClient);

        var imageFeature = client.Features.Get<IImageEmbeddingFeature>();

        Assert.That(imageFeature, Is.Not.Null);
    }

    [Test]
    public void FeatureDiscovery_ViaInterface_ReturnsNullForOpenAiClient()
    {
        using var httpClient = new HttpClient { BaseAddress = new Uri("https://api.openai.com/v1/") };
        IEmbeddingClient client = new OpenAiEmbeddingClient(httpClient);

        var imageFeature = client.Features.Get<IImageEmbeddingFeature>();

        Assert.That(imageFeature, Is.Null);
    }

    [Test]
    public void FeatureDiscovery_ViaInterface_WorksForCohereMultimodal()
    {
        using var httpClient = new HttpClient { BaseAddress = new Uri("https://api.cohere.com/v2/") };
        IEmbeddingClient client = new CohereEmbeddingClient(httpClient);

        var multimodalFeature = client.Features.Get<IMultimodalEmbeddingFeature>();

        Assert.That(multimodalFeature, Is.Not.Null);
    }
}
