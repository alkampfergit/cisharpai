using Cisharpai.Models;
using Cisharpai.OpenAi;
using Microsoft.Extensions.DependencyInjection;

namespace Cisharpai.Integration.Tests.OpenAi;

public sealed class OpenAiEmbeddingIntegrationTests
{
    [OneTimeSetUp]
    public void LoadEnvironment()
    {
        DotEnv.Load();
    }

    [Test]
    public async Task GetEmbeddingsAsync_SingleText_ReturnsValidResponse()
    {
        var apiKey = Environment.GetEnvironmentVariable(DotEnv.OpenAiTestApiKey);
        Assert.That(apiKey, Is.Not.Null.And.Not.Empty,
            $"Environment variable {DotEnv.OpenAiTestApiKey} must be set.");

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddOpenAiEmbeddingClient(options =>
        {
            options.ApiKey = apiKey!;
        });

        await using var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<IEmbeddingClient>();

        var request = new EmbeddingRequest(
            Input: ["Hello world"],
            Model: "text-embedding-3-small",
            IncludeRawResponse: true);

        var response = await client.GetEmbeddingsAsync(request);

        Assert.That(response.IsSuccess, Is.True, $"Request failed: {response.ErrorMessage}");
        Assert.That(response.Embeddings, Has.Count.EqualTo(1));
        Assert.That(response.Embeddings[0].Length, Is.GreaterThan(0));
        Assert.That(response.Model, Does.Contain("text-embedding-3-small"));
        Assert.That(response.TotalTokens, Is.GreaterThan(0));
        Assert.That(response.Dimensions, Is.GreaterThan(0));
    }

    [Test]
    public async Task GetEmbeddingsAsync_MultipleTexts_ReturnsEmbeddingsForEach()
    {
        var apiKey = Environment.GetEnvironmentVariable(DotEnv.OpenAiTestApiKey);
        Assert.That(apiKey, Is.Not.Null.And.Not.Empty,
            $"Environment variable {DotEnv.OpenAiTestApiKey} must be set.");

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddOpenAiEmbeddingClient(options =>
        {
            options.ApiKey = apiKey!;
        });

        await using var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<IEmbeddingClient>();

        var request = new EmbeddingRequest(
            Input: ["Hello world", "Goodbye world", "The quick brown fox"],
            Model: "text-embedding-3-small");

        var response = await client.GetEmbeddingsAsync(request);

        Assert.That(response.IsSuccess, Is.True, $"Request failed: {response.ErrorMessage}");
        Assert.That(response.Embeddings, Has.Count.EqualTo(3));
        Assert.That(response.TotalTokens, Is.GreaterThan(0));
    }

    [Test]
    public async Task GetEmbeddingsAsync_WithCustomDimensions_ReturnsCorrectDimensionCount()
    {
        var apiKey = Environment.GetEnvironmentVariable(DotEnv.OpenAiTestApiKey);
        Assert.That(apiKey, Is.Not.Null.And.Not.Empty,
            $"Environment variable {DotEnv.OpenAiTestApiKey} must be set.");

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddOpenAiEmbeddingClient(options =>
        {
            options.ApiKey = apiKey!;
        });

        await using var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<IEmbeddingClient>();

        var request = new EmbeddingRequest(
            Input: ["Hello world"],
            Model: "text-embedding-3-small",
            Dimensions: 256);

        var response = await client.GetEmbeddingsAsync(request);

        Assert.That(response.IsSuccess, Is.True, $"Request failed: {response.ErrorMessage}");
        Assert.That(response.Dimensions, Is.EqualTo(256));
        Assert.That(response.Embeddings[0].Length, Is.EqualTo(256));
    }

    [TestCase("text-embedding-3-small")]
    [TestCase("text-embedding-3-large")]
    public async Task GetEmbeddingsAsync_WorksWithModel(string model)
    {
        var apiKey = Environment.GetEnvironmentVariable(DotEnv.OpenAiTestApiKey);
        Assert.That(apiKey, Is.Not.Null.And.Not.Empty,
            $"Environment variable {DotEnv.OpenAiTestApiKey} must be set.");

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddOpenAiEmbeddingClient(options =>
        {
            options.ApiKey = apiKey!;
        });

        await using var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<IEmbeddingClient>();

        var request = new EmbeddingRequest(
            Input: ["Test embedding"],
            Model: model);

        var response = await client.GetEmbeddingsAsync(request);

        Assert.That(response.IsSuccess, Is.True, $"Request failed: {response.ErrorMessage}");
        Assert.That(response.Embeddings, Has.Count.EqualTo(1));
        Assert.That(response.Embeddings[0].Length, Is.GreaterThan(0));
    }
}
