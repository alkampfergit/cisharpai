using Cisharpai.Cohere;
using Cisharpai.Features.Embeddings;
using Cisharpai.Models;
using Microsoft.Extensions.DependencyInjection;

namespace Cisharpai.Integration.Tests.Cohere;

public sealed class CohereMultimodalEmbeddingIntegrationTests
{
    [OneTimeSetUp]
    public void LoadEnvironment()
    {
        DotEnv.Load();
    }

    [Test]
    public async Task GetMultimodalEmbeddingsAsync_TextOnly_ReturnsValidResponse()
    {
        var client = ResolveMultimodalFeature();

        var inputs = new List<MultimodalEmbeddingInput>
        {
            new([new TextEmbeddingContent("Hello world")])
        };

        var response = await client.GetMultimodalEmbeddingsAsync(
            inputs, "embed-v4.0", inputType: EmbeddingInputType.Document);

        Assert.That(response.IsSuccess, Is.True, $"Request failed: {response.ErrorMessage}");
        Assert.That(response.Embeddings, Has.Count.EqualTo(1));
        Assert.That(response.Embeddings[0].Length, Is.GreaterThan(0));
        Assert.That(response.TotalTokens, Is.GreaterThan(0));
    }

    [Test]
    public async Task GetMultimodalEmbeddingsAsync_ImageOnly_ReturnsValidResponse()
    {
        var client = ResolveMultimodalFeature();

        var imagePath = CreateTestImage();
        try
        {
            var inputs = new List<MultimodalEmbeddingInput>
            {
                new([new ImageEmbeddingContent(imagePath)])
            };

            var response = await client.GetMultimodalEmbeddingsAsync(
                inputs, "embed-v4.0", inputType: EmbeddingInputType.Document);

            Assert.That(response.IsSuccess, Is.True, $"Request failed: {response.ErrorMessage}");
            Assert.That(response.Embeddings, Has.Count.EqualTo(1));
            Assert.That(response.Embeddings[0].Length, Is.GreaterThan(0));
        }
        finally
        {
            File.Delete(imagePath);
        }
    }

    [Test]
    public async Task GetMultimodalEmbeddingsAsync_MixedTextAndImage_ReturnsValidResponse()
    {
        var client = ResolveMultimodalFeature();

        var imagePath = CreateTestImage();
        try
        {
            var inputs = new List<MultimodalEmbeddingInput>
            {
                new([
                    new TextEmbeddingContent("A simple test image"),
                    new ImageEmbeddingContent(imagePath)
                ])
            };

            var response = await client.GetMultimodalEmbeddingsAsync(
                inputs, "embed-v4.0", inputType: EmbeddingInputType.Document);

            Assert.That(response.IsSuccess, Is.True, $"Request failed: {response.ErrorMessage}");
            Assert.That(response.Embeddings, Has.Count.EqualTo(1));
            Assert.That(response.Embeddings[0].Length, Is.GreaterThan(0));
        }
        finally
        {
            File.Delete(imagePath);
        }
    }

    [Test]
    public async Task GetMultimodalEmbeddingsAsync_WithOutputDimension_ReturnsCorrectDimensions()
    {
        var client = ResolveMultimodalFeature();

        var inputs = new List<MultimodalEmbeddingInput>
        {
            new([new TextEmbeddingContent("Test dimension control")])
        };

        var response = await client.GetMultimodalEmbeddingsAsync(
            inputs, "embed-v4.0",
            inputType: EmbeddingInputType.Document,
            outputDimension: 256);

        Assert.That(response.IsSuccess, Is.True, $"Request failed: {response.ErrorMessage}");
        Assert.That(response.Dimensions, Is.EqualTo(256));
    }

    [Test]
    public async Task GetMultimodalEmbeddingsAsync_BatchInputs_ReturnsMultipleEmbeddings()
    {
        var client = ResolveMultimodalFeature();

        var inputs = new List<MultimodalEmbeddingInput>
        {
            new([new TextEmbeddingContent("First input")]),
            new([new TextEmbeddingContent("Second input")])
        };

        var response = await client.GetMultimodalEmbeddingsAsync(
            inputs, "embed-v4.0", inputType: EmbeddingInputType.Document);

        Assert.That(response.IsSuccess, Is.True, $"Request failed: {response.ErrorMessage}");
        Assert.That(response.Embeddings, Has.Count.EqualTo(2));
    }

    [Test]
    public void FeatureDiscovery_CohereClient_ExposesMultimodalEmbeddingFeature()
    {
        var apiKey = Environment.GetEnvironmentVariable(DotEnv.CohereTestApiKey);
        Assert.That(apiKey, Is.Not.Null.And.Not.Empty,
            $"Environment variable {DotEnv.CohereTestApiKey} must be set.");

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCohereEmbeddingClient(options =>
        {
            options.ApiKey = apiKey!;
        });

        using var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<IEmbeddingClient>();

        var multimodalFeature = client.Features.Get<IMultimodalEmbeddingFeature>();
        Assert.That(multimodalFeature, Is.Not.Null);
    }

    private static IMultimodalEmbeddingFeature ResolveMultimodalFeature()
    {
        var apiKey = Environment.GetEnvironmentVariable(DotEnv.CohereTestApiKey);
        Assert.That(apiKey, Is.Not.Null.And.Not.Empty,
            $"Environment variable {DotEnv.CohereTestApiKey} must be set.");

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCohereEmbeddingClient(options =>
        {
            options.ApiKey = apiKey!;
        });

        var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<IEmbeddingClient>();

        var feature = client.Features.Get<IMultimodalEmbeddingFeature>();
        Assert.That(feature, Is.Not.Null, "CohereEmbeddingClient should expose IMultimodalEmbeddingFeature.");
        return feature!;
    }

    private static string CreateTestImage()
    {
        // Valid 64x64 solid red PNG (168 bytes) - Cohere requires decodable images
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
