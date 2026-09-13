using Cisharpai.Features;
using Cisharpai.Features.Chat;
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
        var client = new OpenAiEmbeddingClient(httpClient, new OpenAiClientOptions());

        Assert.That(client.Features, Is.Not.Null);
    }

    [Test]
    public void OpenAiEmbeddingClient_DoesNotExposeImageEmbeddingFeature()
    {
        using var httpClient = new HttpClient { BaseAddress = new Uri("https://api.openai.com/v1/") };
        var client = new OpenAiEmbeddingClient(httpClient, new OpenAiClientOptions());

        Assert.That(client.Features.Get<IImageEmbeddingFeature>(), Is.Null);
    }

    // --- Anthropic client: no image embedding feature ---

    [Test]
    public void AnthropicChatCompletionClient_Features_IsNotNull()
    {
        using var httpClient = new HttpClient { BaseAddress = new Uri("https://api.anthropic.com/v1/") };
        var client = new AnthropicChatCompletionClient(httpClient, new AnthropicClientOptions());

        Assert.That(client.Features, Is.Not.Null);
    }

    [Test]
    public void AnthropicChatCompletionClient_DoesNotExposeImageEmbeddingFeature()
    {
        using var httpClient = new HttpClient { BaseAddress = new Uri("https://api.anthropic.com/v1/") };
        var client = new AnthropicChatCompletionClient(httpClient, new AnthropicClientOptions());

        Assert.That(client.Features.Get<IImageEmbeddingFeature>(), Is.Null);
    }

    // --- Cohere client: exposes image embedding feature ---

    [Test]
    public void CohereEmbeddingClient_Features_IsNotNull()
    {
        using var httpClient = new HttpClient { BaseAddress = new Uri("https://api.cohere.com/v2/") };
        var client = new CohereEmbeddingClient(httpClient, new CohereClientOptions());

        Assert.That(client.Features, Is.Not.Null);
    }

    [Test]
    public void CohereEmbeddingClient_ExposesImageEmbeddingFeature()
    {
        using var httpClient = new HttpClient { BaseAddress = new Uri("https://api.cohere.com/v2/") };
        var client = new CohereEmbeddingClient(httpClient, new CohereClientOptions());

        var feature = client.Features.Get<IImageEmbeddingFeature>();

        Assert.Multiple(() =>
        {
            Assert.That(feature, Is.Not.Null);
            Assert.That(feature, Is.SameAs(client));
        });
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

        Assert.Multiple(() =>
        {
            Assert.That(feature, Is.Not.Null);
            Assert.That(feature, Is.SameAs(client));
        });
    }

    [Test]
    public void CohereEmbeddingClient_ExposesMultimodalEmbeddingFeature()
    {
        using var httpClient = new HttpClient { BaseAddress = new Uri("https://api.cohere.com/v2/") };
        var client = new CohereEmbeddingClient(httpClient, new CohereClientOptions());

        var feature = client.Features.Get<IMultimodalEmbeddingFeature>();

        Assert.Multiple(() =>
        {
            Assert.That(feature, Is.Not.Null);
            Assert.That(feature, Is.SameAs(client));
        });
    }

    // --- Generic feature discovery pattern ---

    [Test]
    public void FeatureDiscovery_ViaInterface_WorksForCohereClient()
    {
        using var httpClient = new HttpClient { BaseAddress = new Uri("https://api.cohere.com/v2/") };
        var client = new CohereEmbeddingClient(httpClient, new CohereClientOptions());

        var imageFeature = client.Features.Get<IImageEmbeddingFeature>();

        Assert.That(imageFeature, Is.Not.Null);
    }

    [Test]
    public void FeatureDiscovery_ViaInterface_ReturnsNullForOpenAiClient()
    {
        using var httpClient = new HttpClient { BaseAddress = new Uri("https://api.openai.com/v1/") };
        var client = new OpenAiEmbeddingClient(httpClient, new OpenAiClientOptions());

        var imageFeature = client.Features.Get<IImageEmbeddingFeature>();

        Assert.That(imageFeature, Is.Null);
    }

    [Test]
    public void FeatureDiscovery_ViaInterface_WorksForCohereMultimodal()
    {
        using var httpClient = new HttpClient { BaseAddress = new Uri("https://api.cohere.com/v2/") };
        var client = new CohereEmbeddingClient(httpClient, new CohereClientOptions());

        var multimodalFeature = client.Features.Get<IMultimodalEmbeddingFeature>();

        Assert.That(multimodalFeature, Is.Not.Null);
    }

    // --- JSON Output Feature discovery ---

    [Test]
    public void OpenAiChatCompletionClient_ExposesJsonOutputFeature()
    {
        using var httpClient = new HttpClient { BaseAddress = new Uri("https://api.openai.com/v1/") };
        var client = new OpenAiChatCompletionClient(httpClient, new OpenAiClientOptions());

        var feature = client.Features.Get<IJsonOutputFeature>();

        Assert.Multiple(() =>
        {
            Assert.That(feature, Is.Not.Null);
            Assert.That(feature, Is.SameAs(client));
        });
    }

    [Test]
    public void AzureOpenAiChatCompletionClient_ExposesJsonOutputFeature()
    {
        using var httpClient = new HttpClient { BaseAddress = new Uri("https://test.openai.azure.com/") };
        var options = new AzureOpenAiClientOptions { DeploymentName = "test", ApiKey = "key" };
        var client = new AzureOpenAiChatCompletionClient(httpClient, options);

        var feature = client.Features.Get<IJsonOutputFeature>();

        Assert.Multiple(() =>
        {
            Assert.That(feature, Is.Not.Null);
            Assert.That(feature, Is.SameAs(client));
        });
    }

    [Test]
    public void AzureAiInferenceChatCompletionClient_ExposesJsonOutputFeature()
    {
        using var httpClient = new HttpClient { BaseAddress = new Uri("https://test.inference.azure.com/") };
        var options = new AzureAiInferenceClientOptions { ModelId = "test-model", ApiKey = "key" };
        var client = new AzureAiInferenceChatCompletionClient(httpClient, options);

        var feature = client.Features.Get<IJsonOutputFeature>();

        Assert.Multiple(() =>
        {
            Assert.That(feature, Is.Not.Null);
            Assert.That(feature, Is.SameAs(client));
        });
    }

    [Test]
    public void AnthropicChatCompletionClient_ExposesJsonOutputFeature()
    {
        using var httpClient = new HttpClient { BaseAddress = new Uri("https://api.anthropic.com/v1/") };
        var client = new AnthropicChatCompletionClient(httpClient, new AnthropicClientOptions());

        var feature = client.Features.Get<IJsonOutputFeature>();

        Assert.Multiple(() =>
        {
            Assert.That(feature, Is.Not.Null);
            Assert.That(feature, Is.SameAs(client));
        });
    }

    [Test]
    public void CohereEmbeddingClient_DoesNotExposeJsonOutputFeature()
    {
        using var httpClient = new HttpClient { BaseAddress = new Uri("https://api.cohere.com/v2/") };
        var client = new CohereEmbeddingClient(httpClient, new CohereClientOptions());

        Assert.That(client.Features.Get<IJsonOutputFeature>(), Is.Null);
    }

    // --- Grounded Chat Feature discovery ---

    [Test]
    public void CohereChatCompletionClient_ExposesGroundedChatFeature()
    {
        using var httpClient = new HttpClient { BaseAddress = new Uri("https://api.cohere.com/v2/") };
        var client = new CohereChatCompletionClient(httpClient, new CohereClientOptions());

        var feature = client.Features.Get<IGroundedChatFeature>();

        Assert.Multiple(() =>
        {
            Assert.That(feature, Is.Not.Null);
            Assert.That(feature, Is.SameAs(client));
        });
    }

    [Test]
    public void FeatureDiscovery_ViaInterface_WorksForCohereGroundedChat()
    {
        using var httpClient = new HttpClient { BaseAddress = new Uri("https://api.cohere.com/v2/") };
        var client = new CohereChatCompletionClient(httpClient, new CohereClientOptions());

        var groundedFeature = client.Features.Get<IGroundedChatFeature>();

        Assert.That(groundedFeature, Is.Not.Null);
    }

    [Test]
    public void OpenAiChatCompletionClient_ExposesGroundedChatFeature()
    {
        using var httpClient = new HttpClient { BaseAddress = new Uri("https://api.openai.com/v1/") };
        var client = new OpenAiChatCompletionClient(httpClient, new OpenAiClientOptions());

        var feature = client.Features.Get<IGroundedChatFeature>();
        Assert.Multiple(() =>
        {
            Assert.That(feature, Is.Not.Null);
            Assert.That(feature, Is.SameAs(client));
        });
    }

    [Test]
    public void AnthropicChatCompletionClient_ExposesGroundedChatFeature()
    {
        using var httpClient = new HttpClient { BaseAddress = new Uri("https://api.anthropic.com/v1/") };
        var client = new AnthropicChatCompletionClient(httpClient, new AnthropicClientOptions());

        var feature = client.Features.Get<IGroundedChatFeature>();
        Assert.Multiple(() =>
        {
            Assert.That(feature, Is.Not.Null);
            Assert.That(feature, Is.SameAs(client));
        });
    }

    [Test]
    public void AzureOpenAiChatCompletionClient_ExposesGroundedChatFeature()
    {
        using var httpClient = new HttpClient { BaseAddress = new Uri("https://test.openai.azure.com/") };
        var options = new AzureOpenAiClientOptions { DeploymentName = "test", ApiKey = "key" };
        var client = new AzureOpenAiChatCompletionClient(httpClient, options);

        var feature = client.Features.Get<IGroundedChatFeature>();
        Assert.Multiple(() =>
        {
            Assert.That(feature, Is.Not.Null);
            Assert.That(feature, Is.SameAs(client));
        });
    }

    [Test]
    public void AzureAiInferenceChatCompletionClient_DoesNotExposeGroundedChatFeature()
    {
        using var httpClient = new HttpClient { BaseAddress = new Uri("https://test.inference.azure.com/") };
        var options = new AzureAiInferenceClientOptions { ModelId = "test-model", ApiKey = "key" };
        var client = new AzureAiInferenceChatCompletionClient(httpClient, options);

        Assert.That(client.Features.Get<IGroundedChatFeature>(), Is.Null);
    }

    // --- Cohere chat client: exposes JSON output feature ---

    [Test]
    public void CohereChatCompletionClient_Features_IsNotNull()
    {
        using var httpClient = new HttpClient { BaseAddress = new Uri("https://api.cohere.com/v2/") };
        var client = new CohereChatCompletionClient(httpClient, new CohereClientOptions());

        Assert.That(client.Features, Is.Not.Null);
    }

    [Test]
    public void CohereChatCompletionClient_ExposesJsonOutputFeature()
    {
        using var httpClient = new HttpClient { BaseAddress = new Uri("https://api.cohere.com/v2/") };
        var client = new CohereChatCompletionClient(httpClient, new CohereClientOptions());

        var feature = client.Features.Get<IJsonOutputFeature>();

        Assert.Multiple(() =>
        {
            Assert.That(feature, Is.Not.Null);
            Assert.That(feature, Is.SameAs(client));
        });
    }

    [Test]
    public void CohereChatCompletionClient_DoesNotExposeImageEmbeddingFeature()
    {
        using var httpClient = new HttpClient { BaseAddress = new Uri("https://api.cohere.com/v2/") };
        var client = new CohereChatCompletionClient(httpClient, new CohereClientOptions());

        Assert.That(client.Features.Get<IImageEmbeddingFeature>(), Is.Null);
    }

    [Test]
    public void FeatureDiscovery_ViaInterface_WorksForCohereJsonOutput()
    {
        using var httpClient = new HttpClient { BaseAddress = new Uri("https://api.cohere.com/v2/") };
        var client = new CohereChatCompletionClient(httpClient, new CohereClientOptions());

        var jsonFeature = client.Features.Get<IJsonOutputFeature>();

        Assert.That(jsonFeature, Is.Not.Null);
    }

    [Test]
    public void FeatureDiscovery_ViaInterface_WorksForOpenAiJsonOutput()
    {
        using var httpClient = new HttpClient { BaseAddress = new Uri("https://api.openai.com/v1/") };
        var client = new OpenAiChatCompletionClient(httpClient, new OpenAiClientOptions());

        var jsonFeature = client.Features.Get<IJsonOutputFeature>();

        Assert.That(jsonFeature, Is.Not.Null);
    }

    // --- Tool Calling Feature discovery ---

    [Test]
    public void OpenAiChatCompletionClient_ExposesToolCallingFeature()
    {
        using var httpClient = new HttpClient { BaseAddress = new Uri("https://api.openai.com/v1/") };
        var client = new OpenAiChatCompletionClient(httpClient, new OpenAiClientOptions());

        var feature = client.Features.Get<IToolCallingFeature>();

        Assert.Multiple(() =>
        {
            Assert.That(feature, Is.Not.Null);
            Assert.That(feature, Is.SameAs(client));
        });
    }

    [Test]
    public void AnthropicChatCompletionClient_ExposesToolCallingFeature()
    {
        using var httpClient = new HttpClient { BaseAddress = new Uri("https://api.anthropic.com/v1/") };
        var client = new AnthropicChatCompletionClient(httpClient, new AnthropicClientOptions());

        var feature = client.Features.Get<IToolCallingFeature>();

        Assert.Multiple(() =>
        {
            Assert.That(feature, Is.Not.Null);
            Assert.That(feature, Is.SameAs(client));
        });
    }

    [Test]
    public void CohereChatCompletionClient_ExposesToolCallingFeature()
    {
        using var httpClient = new HttpClient { BaseAddress = new Uri("https://api.cohere.com/v2/") };
        var client = new CohereChatCompletionClient(httpClient, new CohereClientOptions());

        var feature = client.Features.Get<IToolCallingFeature>();

        Assert.Multiple(() =>
        {
            Assert.That(feature, Is.Not.Null);
            Assert.That(feature, Is.SameAs(client));
        });
    }

    [Test]
    public void AzureOpenAiChatCompletionClient_ExposesToolCallingFeature()
    {
        using var httpClient = new HttpClient { BaseAddress = new Uri("https://test.openai.azure.com/") };
        var options = new AzureOpenAiClientOptions { DeploymentName = "test", ApiKey = "key" };
        var client = new AzureOpenAiChatCompletionClient(httpClient, options);

        var feature = client.Features.Get<IToolCallingFeature>();

        Assert.Multiple(() =>
        {
            Assert.That(feature, Is.Not.Null);
            Assert.That(feature, Is.SameAs(client));
        });
    }

    [Test]
    public void AzureAiInferenceChatCompletionClient_ExposesToolCallingFeature()
    {
        using var httpClient = new HttpClient { BaseAddress = new Uri("https://test.inference.azure.com/") };
        var options = new AzureAiInferenceClientOptions { ModelId = "test-model", ApiKey = "key" };
        var client = new AzureAiInferenceChatCompletionClient(httpClient, options);

        var feature = client.Features.Get<IToolCallingFeature>();

        Assert.Multiple(() =>
        {
            Assert.That(feature, Is.Not.Null);
            Assert.That(feature, Is.SameAs(client));
        });
    }

    [Test]
    public void OpenAiEmbeddingClient_DoesNotExposeToolCallingFeature()
    {
        using var httpClient = new HttpClient { BaseAddress = new Uri("https://api.openai.com/v1/") };
        var client = new OpenAiEmbeddingClient(httpClient, new OpenAiClientOptions());

        Assert.That(client.Features.Get<IToolCallingFeature>(), Is.Null);
    }

    [Test]
    public void FeatureDiscovery_ViaInterface_WorksForAzureOpenAiToolCalling()
    {
        using var httpClient = new HttpClient { BaseAddress = new Uri("https://test.openai.azure.com/") };
        var options = new AzureOpenAiClientOptions { DeploymentName = "test", ApiKey = "key" };
        var client = new AzureOpenAiChatCompletionClient(httpClient, options);

        var toolFeature = client.Features.Get<IToolCallingFeature>();

        Assert.That(toolFeature, Is.Not.Null);
    }

    [Test]
    public void FeatureDiscovery_ViaInterface_WorksForAzureAiInferenceToolCalling()
    {
        using var httpClient = new HttpClient { BaseAddress = new Uri("https://test.inference.azure.com/") };
        var options = new AzureAiInferenceClientOptions { ModelId = "test-model", ApiKey = "key" };
        var client = new AzureAiInferenceChatCompletionClient(httpClient, options);

        var toolFeature = client.Features.Get<IToolCallingFeature>();

        Assert.That(toolFeature, Is.Not.Null);
    }

    // --- Streaming Chat Feature discovery ---

    [Test]
    public void OpenAiChatCompletionClient_ExposesStreamingChatFeature()
    {
        using var httpClient = new HttpClient { BaseAddress = new Uri("https://api.openai.com/v1/") };
        var client = new OpenAiChatCompletionClient(httpClient, new OpenAiClientOptions());

        var feature = client.Features.Get<IStreamingChatFeature>();

        Assert.Multiple(() =>
        {
            Assert.That(feature, Is.Not.Null);
            Assert.That(feature, Is.SameAs(client));
        });
    }

    [Test]
    public void AnthropicChatCompletionClient_ExposesStreamingChatFeature()
    {
        using var httpClient = new HttpClient { BaseAddress = new Uri("https://api.anthropic.com/v1/") };
        var client = new AnthropicChatCompletionClient(httpClient, new AnthropicClientOptions());

        var feature = client.Features.Get<IStreamingChatFeature>();

        Assert.Multiple(() =>
        {
            Assert.That(feature, Is.Not.Null);
            Assert.That(feature, Is.SameAs(client));
        });
    }

    [Test]
    public void CohereChatCompletionClient_ExposesStreamingChatFeature()
    {
        using var httpClient = new HttpClient { BaseAddress = new Uri("https://api.cohere.com/v2/") };
        var client = new CohereChatCompletionClient(httpClient, new CohereClientOptions());

        var feature = client.Features.Get<IStreamingChatFeature>();

        Assert.Multiple(() =>
        {
            Assert.That(feature, Is.Not.Null);
            Assert.That(feature, Is.SameAs(client));
        });
    }

    [Test]
    public void AzureOpenAiChatCompletionClient_ExposesStreamingChatFeature()
    {
        using var httpClient = new HttpClient { BaseAddress = new Uri("https://test.openai.azure.com/") };
        var options = new AzureOpenAiClientOptions { DeploymentName = "test", ApiKey = "key" };
        var client = new AzureOpenAiChatCompletionClient(httpClient, options);

        var feature = client.Features.Get<IStreamingChatFeature>();

        Assert.Multiple(() =>
        {
            Assert.That(feature, Is.Not.Null);
            Assert.That(feature, Is.SameAs(client));
        });
    }

    [Test]
    public void AzureAiInferenceChatCompletionClient_ExposesStreamingChatFeature()
    {
        using var httpClient = new HttpClient { BaseAddress = new Uri("https://test.inference.azure.com/") };
        var options = new AzureAiInferenceClientOptions { ModelId = "test-model", ApiKey = "key" };
        var client = new AzureAiInferenceChatCompletionClient(httpClient, options);

        var feature = client.Features.Get<IStreamingChatFeature>();

        Assert.Multiple(() =>
        {
            Assert.That(feature, Is.Not.Null);
            Assert.That(feature, Is.SameAs(client));
        });
    }

    [Test]
    public void OpenAiEmbeddingClient_DoesNotExposeStreamingChatFeature()
    {
        using var httpClient = new HttpClient { BaseAddress = new Uri("https://api.openai.com/v1/") };
        var client = new OpenAiEmbeddingClient(httpClient, new OpenAiClientOptions());

        Assert.That(client.Features.Get<IStreamingChatFeature>(), Is.Null);
    }

    [Test]
    public void CohereEmbeddingClient_DoesNotExposeStreamingChatFeature()
    {
        using var httpClient = new HttpClient { BaseAddress = new Uri("https://api.cohere.com/v2/") };
        var client = new CohereEmbeddingClient(httpClient, new CohereClientOptions());

        Assert.That(client.Features.Get<IStreamingChatFeature>(), Is.Null);
    }

    [Test]
    public void FeatureDiscovery_ViaInterface_WorksForOpenAiStreaming()
    {
        using var httpClient = new HttpClient { BaseAddress = new Uri("https://api.openai.com/v1/") };
        var client = new OpenAiChatCompletionClient(httpClient, new OpenAiClientOptions());

        var streamFeature = client.Features.Get<IStreamingChatFeature>();

        Assert.That(streamFeature, Is.Not.Null);
    }

    [Test]
    public void FeatureDiscovery_ViaInterface_WorksForAnthropicStreaming()
    {
        using var httpClient = new HttpClient { BaseAddress = new Uri("https://api.anthropic.com/v1/") };
        var client = new AnthropicChatCompletionClient(httpClient, new AnthropicClientOptions());

        var streamFeature = client.Features.Get<IStreamingChatFeature>();

        Assert.That(streamFeature, Is.Not.Null);
    }
}
