using Cisharpai.Cohere;
using Cisharpai.Features.Embeddings;
using Microsoft.Extensions.DependencyInjection;

namespace Cisharpai.Integration.Tests.Cohere;

public sealed class CohereImageEmbeddingIntegrationTests
{
    [OneTimeSetUp]
    public void LoadEnvironment()
    {
        DotEnv.Load();
    }

    [Test]
    public async Task GetImageEmbeddingAsync_ReturnsValidResponse()
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

        await using var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<IEmbeddingClient>();

        var imageFeature = client.Features.Get<IImageEmbeddingFeature>();
        Assert.That(imageFeature, Is.Not.Null, "CohereEmbeddingClient should expose IImageEmbeddingFeature.");

        var imagePath = CreateTestImage();
        try
        {
            var response = await imageFeature!.GetImageEmbeddingAsync(imagePath, "embed-v4.0");

            Assert.That(response.IsSuccess, Is.True, $"Request failed: {response.ErrorMessage}");
            Assert.That(response.Embeddings, Has.Count.EqualTo(1));
            Assert.That(response.Embeddings[0].Length, Is.GreaterThan(0));
            Assert.That(response.TotalTokens, Is.GreaterThan(0));
            Assert.That(response.Dimensions, Is.GreaterThan(0));
        }
        finally
        {
            File.Delete(imagePath);
        }
    }

    [Test]
    public void FeatureDiscovery_CohereClient_ExposesImageEmbeddingFeature()
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

        var imageFeature = client.Features.Get<IImageEmbeddingFeature>();
        Assert.That(imageFeature, Is.Not.Null);
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
