using Cisharpai.Models;
using Cisharpai.Cohere;
using Microsoft.Extensions.DependencyInjection;

namespace Cisharpai.Integration.Tests.Cohere;

public sealed class CohereEmbeddingIntegrationTests
{
    [OneTimeSetUp]
    public void LoadEnvironment()
    {
        DotEnv.Load();
    }

    [TestCase("embed-english-v3.0")]
    [TestCase("embed-v4.0")]
    public async Task GetEmbeddingsAsync_ReturnsValidResponse(string model)
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

        var request = new EmbeddingRequest(
            Input: ["Hello world"],
            Model: model,
            InputType: EmbeddingInputType.Document,
            IncludeRawResponse: true);

        var response = await client.GetEmbeddingsAsync(request);

        Assert.That(response.IsSuccess, Is.True, $"Request failed: {response.ErrorMessage}");
        Assert.That(response.Embeddings, Has.Count.EqualTo(1));
        Assert.That(response.Embeddings[0].Length, Is.GreaterThan(0));
        Assert.That(response.TotalTokens, Is.GreaterThan(0));
        Assert.That(response.Dimensions, Is.GreaterThan(0));
    }

    [TestCase("embed-english-v3.0", 1024)]
    [TestCase("embed-v4.0", 1536)]
    public async Task GetEmbeddingsAsync_ReturnsCorrectDimensionCount(string model, int expectedDimensions)
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

        var request = new EmbeddingRequest(
            Input: ["Hello world"],
            Model: model,
            InputType: EmbeddingInputType.Document);

        var response = await client.GetEmbeddingsAsync(request);

        Assert.That(response.IsSuccess, Is.True, $"Request failed: {response.ErrorMessage}");
        Assert.That(response.Dimensions, Is.EqualTo(expectedDimensions));
    }

    [TestCase(EmbeddingInputType.Query)]
    [TestCase(EmbeddingInputType.Document)]
    [TestCase(EmbeddingInputType.Classification)]
    [TestCase(EmbeddingInputType.Clustering)]
    public async Task GetEmbeddingsAsync_WorksWithDifferentInputTypes(EmbeddingInputType inputType)
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

        var request = new EmbeddingRequest(
            Input: ["Test embedding"],
            Model: "embed-english-v3.0",
            InputType: inputType);

        var response = await client.GetEmbeddingsAsync(request);

        Assert.That(response.IsSuccess, Is.True, $"Request failed: {response.ErrorMessage}");
        Assert.That(response.Embeddings, Has.Count.EqualTo(1));
    }
}
