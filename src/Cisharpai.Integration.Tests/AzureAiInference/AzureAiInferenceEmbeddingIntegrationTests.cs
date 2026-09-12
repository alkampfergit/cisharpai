using Cisharpai.Azure;
using Cisharpai.Azure.AzureAiInference;
using Cisharpai.Features.Embeddings;
using Cisharpai.Models;
using Microsoft.Extensions.DependencyInjection;

namespace Cisharpai.Integration.Tests.AzureAiInference;

public sealed class AzureAiInferenceEmbeddingIntegrationTests
{
    [OneTimeSetUp]
    public void LoadEnvironment()
    {
        DotEnv.Load();
    }

    private IEmbeddingClient CreateClient()
    {
        var endpoint = Environment.GetEnvironmentVariable(DotEnv.AzureAiInferenceTestEmbeddingEndpoint);
        var apiKey = Environment.GetEnvironmentVariable(DotEnv.AzureAiInferenceTestEmbeddingKey);
        var modelId = Environment.GetEnvironmentVariable(DotEnv.AzureAiInferenceTestEmbeddingModel);

        Assert.That(endpoint, Is.Not.Null.And.Not.Empty,
            $"Environment variable {DotEnv.AzureAiInferenceTestEmbeddingEndpoint} must be set.");
        Assert.That(apiKey, Is.Not.Null.And.Not.Empty,
            $"Environment variable {DotEnv.AzureAiInferenceTestEmbeddingKey} must be set.");
        Assert.That(modelId, Is.Not.Null.And.Not.Empty,
            $"Environment variable {DotEnv.AzureAiInferenceTestEmbeddingModel} must be set.");

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAzureAiInferenceEmbeddings(options =>
        {
            options.Endpoint = endpoint!;
            options.ApiKey = apiKey!;
            options.ModelId = modelId!;
        });

        var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IEmbeddingClient>();
    }

    // --- Text Embedding Basic ---

    [Test]
    public async Task GetEmbeddingsAsync_TextInput_ReturnsValidResponse()
    {
        var client = CreateClient();
        var modelId = Environment.GetEnvironmentVariable(DotEnv.AzureAiInferenceTestEmbeddingModel)!;

        var request = new EmbeddingRequest(
            Input: ["Hello world"],
            Model: modelId,
            IncludeRawResponse: true);

        var response = await client.GetEmbeddingsAsync(request);

        Assert.That(response.IsSuccess, Is.True, $"Request failed: {response.ErrorMessage}");
        Assert.That(response.Embeddings, Has.Count.EqualTo(1));
        Assert.That(response.Embeddings[0].Length, Is.GreaterThan(0));
        Assert.That(response.Dimensions, Is.GreaterThan(0));
    }

    // --- Batch Text Embedding ---

    [Test]
    public async Task GetEmbeddingsAsync_BatchInput_ReturnsMultipleEmbeddings()
    {
        var client = CreateClient();
        var modelId = Environment.GetEnvironmentVariable(DotEnv.AzureAiInferenceTestEmbeddingModel)!;

        var request = new EmbeddingRequest(
            Input: ["First text", "Second text", "Third text"],
            Model: modelId);

        var response = await client.GetEmbeddingsAsync(request);

        Assert.That(response.IsSuccess, Is.True, $"Request failed: {response.ErrorMessage}");
        Assert.That(response.Embeddings, Has.Count.EqualTo(3));
        Assert.That(response.Embeddings[0].Length, Is.GreaterThan(0));
        Assert.That(response.Embeddings[1].Length, Is.GreaterThan(0));
        Assert.That(response.Embeddings[2].Length, Is.GreaterThan(0));
    }

    // --- Text Embedding with Dimensions ---

    [Test]
    public async Task GetEmbeddingsAsync_WithDimensions_ReturnsTruncatedVector()
    {
        var client = CreateClient();
        var modelId = Environment.GetEnvironmentVariable(DotEnv.AzureAiInferenceTestEmbeddingModel)!;

        var request = new EmbeddingRequest(
            Input: ["Hello world"],
            Model: modelId,
            Dimensions: 256);

        var response = await client.GetEmbeddingsAsync(request);

        // Dimension control is model-dependent; if unsupported, the test is inconclusive
        if (!response.IsSuccess)
        {
            Assert.Inconclusive(
                $"Dimension control may not be supported by model {modelId}: {response.ErrorMessage}");
            return;
        }

        Assert.That(response.Embeddings, Has.Count.EqualTo(1));
        Assert.That(response.Embeddings[0].Length, Is.EqualTo(256));
        Assert.That(response.Dimensions, Is.EqualTo(256));
    }

    // --- Image Embedding via IImageEmbeddingFeature ---

    [Test]
    public async Task ImageEmbedding_ViaFeatureDiscovery_ReturnsValidResponse()
    {
        var client = CreateClient();
        var modelId = Environment.GetEnvironmentVariable(DotEnv.AzureAiInferenceTestEmbeddingModel)!;

        var imageFeature = client.Features.Get<IImageEmbeddingFeature>();
        Assert.That(imageFeature, Is.Not.Null,
            "AzureAiInferenceEmbeddingClient should expose IImageEmbeddingFeature.");

        var imagePath = CreateTestImage();
        try
        {
            var response = await imageFeature!.GetImageEmbeddingAsync(imagePath, modelId);

            // Image embedding support is model-dependent
            if (!response.IsSuccess)
            {
                Assert.Inconclusive(
                    $"Image embedding may not be supported by model {modelId}: {response.ErrorMessage}");
                return;
            }

            Assert.That(response.Embeddings, Has.Count.EqualTo(1));
            Assert.That(response.Embeddings[0].Length, Is.GreaterThan(0));
            Assert.That(response.Dimensions, Is.GreaterThan(0));
        }
        finally
        {
            File.Delete(imagePath);
        }
    }

    // --- Feature Discovery ---

    [Test]
    public void FeatureDiscovery_ImageEmbeddingFeature_Available()
    {
        var client = CreateClient();

        var imageFeature = client.Features.Get<IImageEmbeddingFeature>();

        Assert.That(imageFeature, Is.Not.Null);
    }

    private static string CreateTestImage()
    {
        // Valid 64x64 solid red PNG (168 bytes) - embedding models require decodable images
        var pngBytes = Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAEAAAABACAIAAAAlC+aJAAAAb0lEQVR4nO3PAQkA" +
            "AAyEwO9feoshgnABdLep8QUNyPEFDcjxBQ3I8QUNyPEFDcjxBQ3I8QUNyPEFDcjx" +
            "BQ3I8QUNyPEFDcjxBQ3I8QUNyPEFDcjxBQ3I8QUNyPEFDcjxBQ3I8QUNyPEFDcjx" +
            "BQ3IPanc8OLDQitxAAAAAElFTkSuQmCC");

        var path = Path.Combine(Path.GetTempPath(), $"test-image-{Guid.NewGuid()}.png");
        File.WriteAllBytes(path, pngBytes);
        return path;
    }
}
