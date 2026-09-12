using System.Text.Json;
using Cisharpai.Models;
using Cisharpai.Cohere;
using Microsoft.Extensions.DependencyInjection;

namespace Cisharpai.Integration.Tests.Cohere;

public sealed class CohereRerankIntegrationTests
{
    private static readonly string[] Documents =
    [
        "Carson City is the capital city of the American state of Nevada.",
        "Paris is the capital and most populous city of France.",
        "The Louvre is the world's most-visited museum, located in Paris.",
        "Washington, D.C. is the capital of the United States."
    ];

    private const string Query = "What is the capital of France?";

    private const int ParisIndex = 1;

    [OneTimeSetUp]
    public void LoadEnvironment()
    {
        DotEnv.Load();
    }

    private static IRerankerClient CreateClient(out ServiceProvider provider)
    {
        var apiKey = Environment.GetEnvironmentVariable(DotEnv.CohereTestApiKey);
        Assert.That(apiKey, Is.Not.Null.And.Not.Empty,
            $"Environment variable {DotEnv.CohereTestApiKey} must be set.");

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCohereRerankerClient(options =>
        {
            options.ApiKey = apiKey!;
        });

        provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IRerankerClient>();
    }

    [TestCase(CohereModels.Rerank.RerankV3_5)]
    public async Task RerankAsync_RanksTheMostRelevantDocumentFirst(string model)
    {
        var client = CreateClient(out var provider);
        await using var _ = provider;

        var response = await client.RerankAsync(new RerankRequest(
            Query: Query,
            Documents: Documents,
            Model: model,
            IncludeRawResponse: true));

        Assert.That(response.IsSuccess, Is.True, $"Request failed: {response.ErrorMessage}");
        Assert.That(response.Results, Has.Count.EqualTo(Documents.Length));
        Assert.That(response.Results[0].Index, Is.EqualTo(ParisIndex));
        Assert.That(response.Model, Is.EqualTo(model));
        Assert.That(response.RawResponseJson, Is.Not.Null.And.Not.Empty);
        Assert.That(response.RawRequestJson, Is.Not.Null.And.Not.Empty);
    }

    [Test]
    public async Task RerankAsync_ReturnsScoresInDescendingOrder()
    {
        var client = CreateClient(out var provider);
        await using var _ = provider;

        var response = await client.RerankAsync(new RerankRequest(
            Query: Query,
            Documents: Documents,
            Model: CohereModels.Rerank.RerankV3_5));

        Assert.That(response.IsSuccess, Is.True, $"Request failed: {response.ErrorMessage}");
        var scores = response.Results.Select(r => r.RelevanceScore).ToList();
        Assert.That(scores, Is.Ordered.Descending);
    }

    [Test]
    public async Task RerankAsync_HonoursTopN()
    {
        var client = CreateClient(out var provider);
        await using var _ = provider;

        var response = await client.RerankAsync(new RerankRequest(
            Query: Query,
            Documents: Documents,
            Model: CohereModels.Rerank.RerankV3_5,
            TopN: 2));

        Assert.That(response.IsSuccess, Is.True, $"Request failed: {response.ErrorMessage}");
        Assert.That(response.Results, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task RerankAsync_AcceptsPriorityViaExtraParameters()
    {
        var client = CreateClient(out var provider);
        await using var _ = provider;

        var response = await client.RerankAsync(new RerankRequest(
            Query: Query,
            Documents: Documents,
            Model: CohereModels.Rerank.RerankV3_5,
            ExtraParameters: JsonSerializer.SerializeToElement(new { priority = "high" })));

        Assert.That(response.IsSuccess, Is.True, $"Request failed: {response.ErrorMessage}");
        Assert.That(response.Results[0].Index, Is.EqualTo(ParisIndex));
    }

    [Test]
    public async Task RerankAsync_WithInvalidApiKey_ReturnsErrorWithoutThrowing()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCohereRerankerClient(options =>
        {
            options.ApiKey = "invalid-api-key";
        });

        await using var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<IRerankerClient>();

        var response = await client.RerankAsync(new RerankRequest(
            Query: Query,
            Documents: Documents,
            Model: CohereModels.Rerank.RerankV3_5));

        Assert.That(response.IsSuccess, Is.False);
        Assert.That(response.ErrorMessage, Is.Not.Null.And.Not.Empty);
    }
}
