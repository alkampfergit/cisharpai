using Cisharpai.Models;
using Cisharpai.Azure;
using Cisharpai.Azure.AzureOpenAi;
using Microsoft.Extensions.DependencyInjection;

namespace Cisharpai.Integration.Tests.AzureOpenAi;

public sealed class AzureOpenAiEmbeddingIntegrationTests
{
    [OneTimeSetUp]
    public void LoadEnvironment()
    {
        DotEnv.Load();
    }

    [Test]
    public async Task GetEmbeddingsAsync_SingleText_ReturnsValidResponse()
    {
        var endpoint = Environment.GetEnvironmentVariable(DotEnv.AzureOpenAiTestEndpoint);
        var apiKey = Environment.GetEnvironmentVariable(DotEnv.AzureOpenAiTestApiKey);
        var deployment = Environment.GetEnvironmentVariable(DotEnv.AzureOpenAiTestEmbeddingDeployment);

        Assert.That(endpoint, Is.Not.Null.And.Not.Empty,
            $"Environment variable {DotEnv.AzureOpenAiTestEndpoint} must be set.");
        Assert.That(apiKey, Is.Not.Null.And.Not.Empty,
            $"Environment variable {DotEnv.AzureOpenAiTestApiKey} must be set.");
        Assert.That(deployment, Is.Not.Null.And.Not.Empty,
            $"Environment variable {DotEnv.AzureOpenAiTestEmbeddingDeployment} must be set.");

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAzureOpenAiEmbeddingClient(options =>
        {
            options.Endpoint = endpoint!;
            options.ApiKey = apiKey!;
            options.DeploymentName = deployment!;
        });

        await using var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<IEmbeddingClient>();

        var request = new EmbeddingRequest(
            Input: ["Hello, world!"],
            Model: deployment!,
            IncludeRawResponse: true);

        var response = await client.GetEmbeddingsAsync(request);

        Assert.That(response, Is.Not.Null);
        Assert.That(response.IsSuccess, Is.True, $"Request failed: {response.ErrorMessage}");
        Assert.That(response.Embeddings, Is.Not.Null.And.Not.Empty);
        Assert.That(response.Embeddings.Count, Is.EqualTo(1));
        Assert.That(response.Embeddings[0].Length, Is.GreaterThan(0));
        Assert.That(response.TotalTokens, Is.GreaterThan(0));
        Assert.That(response.Model, Is.Not.Null.And.Not.Empty);
    }

    [Test]
    public async Task GetEmbeddingsAsync_MultipleTexts_ReturnsEmbeddingsForEach()
    {
        var endpoint = Environment.GetEnvironmentVariable(DotEnv.AzureOpenAiTestEndpoint);
        var apiKey = Environment.GetEnvironmentVariable(DotEnv.AzureOpenAiTestApiKey);
        var deployment = Environment.GetEnvironmentVariable(DotEnv.AzureOpenAiTestEmbeddingDeployment);

        Assert.That(endpoint, Is.Not.Null.And.Not.Empty,
            $"Environment variable {DotEnv.AzureOpenAiTestEndpoint} must be set.");
        Assert.That(apiKey, Is.Not.Null.And.Not.Empty,
            $"Environment variable {DotEnv.AzureOpenAiTestApiKey} must be set.");
        Assert.That(deployment, Is.Not.Null.And.Not.Empty,
            $"Environment variable {DotEnv.AzureOpenAiTestEmbeddingDeployment} must be set.");

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAzureOpenAiEmbeddingClient(options =>
        {
            options.Endpoint = endpoint!;
            options.ApiKey = apiKey!;
            options.DeploymentName = deployment!;
        });

        await using var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<IEmbeddingClient>();

        var request = new EmbeddingRequest(
            Input: ["First text", "Second text", "Third text"],
            Model: deployment!,
            IncludeRawResponse: true);

        var response = await client.GetEmbeddingsAsync(request);

        Assert.That(response, Is.Not.Null);
        Assert.That(response.IsSuccess, Is.True, $"Request failed: {response.ErrorMessage}");
        Assert.That(response.Embeddings, Is.Not.Null);
        Assert.That(response.Embeddings.Count, Is.EqualTo(3));
    }

    [Test]
    public async Task GetEmbeddingsAsync_WithDimensions_ReturnsCorrectDimensionCount()
    {
        var endpoint = Environment.GetEnvironmentVariable(DotEnv.AzureOpenAiTestEndpoint);
        var apiKey = Environment.GetEnvironmentVariable(DotEnv.AzureOpenAiTestApiKey);
        var deployment = Environment.GetEnvironmentVariable(DotEnv.AzureOpenAiTestEmbeddingDeployment);

        Assert.That(endpoint, Is.Not.Null.And.Not.Empty,
            $"Environment variable {DotEnv.AzureOpenAiTestEndpoint} must be set.");
        Assert.That(apiKey, Is.Not.Null.And.Not.Empty,
            $"Environment variable {DotEnv.AzureOpenAiTestApiKey} must be set.");
        Assert.That(deployment, Is.Not.Null.And.Not.Empty,
            $"Environment variable {DotEnv.AzureOpenAiTestEmbeddingDeployment} must be set.");

        // text-embedding-3-* models support custom dimensions
        // Skip this test if using text-embedding-ada-002 which doesn't support dimensions
        if (deployment!.Contains("ada", StringComparison.OrdinalIgnoreCase))
        {
            Assert.Ignore("text-embedding-ada-002 does not support custom dimensions");
        }

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAzureOpenAiEmbeddingClient(options =>
        {
            options.Endpoint = endpoint!;
            options.ApiKey = apiKey!;
            options.DeploymentName = deployment;
        });

        await using var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<IEmbeddingClient>();

        const int requestedDimensions = 256;
        var request = new EmbeddingRequest(
            Input: ["Test text for dimension check"],
            Model: deployment,
            Dimensions: requestedDimensions,
            IncludeRawResponse: true);

        var response = await client.GetEmbeddingsAsync(request);

        Assert.That(response, Is.Not.Null);
        Assert.That(response.IsSuccess, Is.True, $"Request failed: {response.ErrorMessage}");
        Assert.That(response.Embeddings, Is.Not.Null.And.Not.Empty);
        Assert.That(response.Embeddings[0].Length, Is.EqualTo(requestedDimensions));
        Assert.That(response.Dimensions, Is.EqualTo(requestedDimensions));
    }

    [Test]
    public async Task GetEmbeddingsAsync_InvalidApiKey_ReturnsErrorResponse()
    {
        var endpoint = Environment.GetEnvironmentVariable(DotEnv.AzureOpenAiTestEndpoint);
        var deployment = Environment.GetEnvironmentVariable(DotEnv.AzureOpenAiTestEmbeddingDeployment);

        Assert.That(endpoint, Is.Not.Null.And.Not.Empty,
            $"Environment variable {DotEnv.AzureOpenAiTestEndpoint} must be set.");
        Assert.That(deployment, Is.Not.Null.And.Not.Empty,
            $"Environment variable {DotEnv.AzureOpenAiTestEmbeddingDeployment} must be set.");

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAzureOpenAiEmbeddingClient(options =>
        {
            options.Endpoint = endpoint!;
            options.ApiKey = "invalid-api-key";
            options.DeploymentName = deployment!;
        });

        await using var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<IEmbeddingClient>();

        var request = new EmbeddingRequest(
            Input: ["Test"],
            Model: deployment!);

        var response = await client.GetEmbeddingsAsync(request);

        Assert.That(response, Is.Not.Null);
        Assert.That(response.IsSuccess, Is.False);
        Assert.That(response.ErrorMessage, Is.Not.Null.And.Not.Empty);
    }
}
