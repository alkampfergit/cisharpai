using System.Net;
using System.Text.Json;
using Cisharpai.Models;
using Cisharpai.Cohere;

namespace Cisharpai.Tests.Cohere;

public sealed class CohereRerankerClientTests
{
    private const string CohereRerankResponseJson = """
    {
        "id": "07734bd2-2473-4f07-94e1-0d9f0e6843cf",
        "results": [
            { "index": 1, "relevance_score": 0.999071 },
            { "index": 2, "relevance_score": 0.752048 },
            { "index": 0, "relevance_score": 0.083821 }
        ],
        "meta": {
            "api_version": { "version": "2" },
            "billed_units": { "search_units": 1 }
        }
    }
    """;

    private static readonly string[] Documents =
    [
        "Carson City is the capital city of Nevada.",
        "Paris is the capital and most populous city of France.",
        "The Louvre is a museum in Paris."
    ];

    private static MockHttpMessageHandler OkHandler(Action<string>? captureBody = null) =>
        new(async (request, _) =>
        {
            if (captureBody is not null)
                captureBody(await request.Content!.ReadAsStringAsync(CancellationToken.None));

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(CohereRerankResponseJson, System.Text.Encoding.UTF8, "application/json")
            };
        });

    private static CohereRerankerClient CreateClient(
        HttpMessageHandler handler,
        out HttpClient httpClient,
        string baseUrl = "https://api.cohere.com/v2/",
        CohereClientOptions? options = null)
    {
        httpClient = new HttpClient(handler) { BaseAddress = new Uri(baseUrl) };
        return new CohereRerankerClient(httpClient, options ?? new CohereClientOptions());
    }

    [Test]
    public async Task RerankAsync_MapsRequestToCohereFormat()
    {
        string? capturedBody = null;
        var handler = OkHandler(body => capturedBody = body);
        var client = CreateClient(handler, out var httpClient);
        using var httpScope = httpClient;

        await client.RerankAsync(new RerankRequest(
            Query: "What is the capital of France?",
            Documents: Documents,
            Model: CohereModels.Rerank.RerankV3_5));

        Assert.That(capturedBody, Is.Not.Null);
        var root = JsonDocument.Parse(capturedBody!).RootElement;
        Assert.Multiple(() =>
        {
            Assert.That(root.GetProperty("model").GetString(), Is.EqualTo("rerank-v3.5"));
            Assert.That(root.GetProperty("query").GetString(), Is.EqualTo("What is the capital of France?"));
            Assert.That(root.GetProperty("documents").GetArrayLength(), Is.EqualTo(3));
            Assert.That(root.GetProperty("documents")[1].GetString(), Is.EqualTo(Documents[1]));
        });
    }

    [Test]
    public async Task RerankAsync_OmitsOptionalParametersWhenNotSet()
    {
        string? capturedBody = null;
        var handler = OkHandler(body => capturedBody = body);
        var client = CreateClient(handler, out var httpClient);
        using var httpScope = httpClient;

        await client.RerankAsync(new RerankRequest("q", Documents, Model: "rerank-v3.5"));

        var root = JsonDocument.Parse(capturedBody!).RootElement;
        Assert.Multiple(() =>
        {
            Assert.That(root.TryGetProperty("top_n", out _), Is.False);
            Assert.That(root.TryGetProperty("max_tokens_per_doc", out _), Is.False);
        });
    }

    [Test]
    public async Task RerankAsync_SendsTopNAndMaxTokensPerDocument()
    {
        string? capturedBody = null;
        var handler = OkHandler(body => capturedBody = body);
        var client = CreateClient(handler, out var httpClient);
        using var httpScope = httpClient;

        await client.RerankAsync(new RerankRequest(
            Query: "q",
            Documents: Documents,
            Model: "rerank-v3.5",
            TopN: 2,
            MaxTokensPerDocument: 4096));

        var root = JsonDocument.Parse(capturedBody!).RootElement;
        Assert.Multiple(() =>
        {
            Assert.That(root.GetProperty("top_n").GetInt32(), Is.EqualTo(2));
            Assert.That(root.GetProperty("max_tokens_per_doc").GetInt32(), Is.EqualTo(4096));
        });
    }

    [Test]
    public async Task RerankAsync_PostsToRerankEndpoint()
    {
        var handler = OkHandler();
        var client = CreateClient(handler, out var httpClient);
        using var httpScope = httpClient;

        await client.RerankAsync(new RerankRequest("q", Documents, Model: "rerank-v3.5"));

        Assert.Multiple(() =>
        {
            Assert.That(handler.LastRequest!.Method, Is.EqualTo(HttpMethod.Post));
            Assert.That(handler.LastRequest!.RequestUri!.ToString(),
                Is.EqualTo("https://api.cohere.com/v2/rerank"));
        });
    }

    [Test]
    public async Task RerankAsync_HonoursCustomBaseUrl()
    {
        var handler = OkHandler();
        var client = CreateClient(handler, out var httpClient,
            baseUrl: "https://my-cohere.inference.ai.azure.com/v2/");
        using var httpScope = httpClient;

        await client.RerankAsync(new RerankRequest("q", Documents, Model: "rerank-v3.5"));

        Assert.That(handler.LastRequest!.RequestUri!.ToString(),
            Is.EqualTo("https://my-cohere.inference.ai.azure.com/v2/rerank"));
    }

    [Test]
    public async Task RerankAsync_MapsResponseToSharedModel()
    {
        var handler = OkHandler();
        var client = CreateClient(handler, out var httpClient);
        using var httpScope = httpClient;

        var response = await client.RerankAsync(new RerankRequest("q", Documents, Model: "rerank-v3.5"));

        Assert.Multiple(() =>
        {
            Assert.That(response.IsSuccess, Is.True);
            Assert.That(response.ErrorMessage, Is.Null);
            Assert.That(response.Results, Has.Count.EqualTo(3));
            Assert.That(response.Model, Is.EqualTo("rerank-v3.5"));
            Assert.That(response.SearchUnits, Is.EqualTo(1));
            Assert.That(response.InputTokens, Is.Null);
        });
    }

    [Test]
    public async Task RerankAsync_PreservesProviderRankingOrderAndIndices()
    {
        var handler = OkHandler();
        var client = CreateClient(handler, out var httpClient);
        using var httpScope = httpClient;

        var response = await client.RerankAsync(new RerankRequest("q", Documents, Model: "rerank-v3.5"));

        Assert.Multiple(() =>
        {
            Assert.That(response.Results[0].Index, Is.EqualTo(1));
            Assert.That(response.Results[0].RelevanceScore, Is.EqualTo(0.999071).Within(0.000001));
            Assert.That(response.Results[1].Index, Is.EqualTo(2));
            Assert.That(response.Results[2].Index, Is.EqualTo(0));
        });

        // Index refers back into the caller's own document list.
        Assert.That(Documents[response.Results[0].Index], Is.EqualTo(Documents[1]));
    }

    [Test]
    public async Task RerankAsync_MergesExtraParameters()
    {
        string? capturedBody = null;
        var handler = OkHandler(body => capturedBody = body);
        var client = CreateClient(handler, out var httpClient);
        using var httpScope = httpClient;

        await client.RerankAsync(new RerankRequest(
            Query: "q",
            Documents: Documents,
            Model: "rerank-v3.5",
            ExtraParameters: JsonSerializer.SerializeToElement(new { priority = 500 })));

        var root = JsonDocument.Parse(capturedBody!).RootElement;
        Assert.Multiple(() =>
        {
            Assert.That(root.GetProperty("priority").GetInt32(), Is.EqualTo(500));
            Assert.That(root.GetProperty("model").GetString(), Is.EqualTo("rerank-v3.5"));
        });
    }

    [Test]
    public async Task RerankAsync_PopulatesRawPayloadsWhenRequested()
    {
        var handler = OkHandler();
        var client = CreateClient(handler, out var httpClient);
        using var httpScope = httpClient;

        var response = await client.RerankAsync(new RerankRequest(
            Query: "q",
            Documents: Documents,
            Model: "rerank-v3.5",
            IncludeRawResponse: true));

        Assert.Multiple(() =>
        {
            Assert.That(response.RawResponseJson, Does.Contain("relevance_score"));
            Assert.That(response.RawRequestJson, Does.Contain("rerank-v3.5"));
        });
    }

    [Test]
    public async Task RerankAsync_LeavesRawPayloadsNullByDefault()
    {
        var handler = OkHandler();
        var client = CreateClient(handler, out var httpClient);
        using var httpScope = httpClient;

        var response = await client.RerankAsync(new RerankRequest("q", Documents, Model: "rerank-v3.5"));

        Assert.Multiple(() =>
        {
            Assert.That(response.RawResponseJson, Is.Null);
            Assert.That(response.RawRequestJson, Is.Null);
        });
    }

    [Test]
    public async Task RerankAsync_ReturnsErrorResponseOnHttpFailure()
    {
        var handler = new MockHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized)
            {
                Content = new StringContent("""{"message":"invalid api token"}""",
                    System.Text.Encoding.UTF8, "application/json")
            }));

        var client = CreateClient(handler, out var httpClient);
        using var httpScope = httpClient;

        var response = await client.RerankAsync(new RerankRequest("q", Documents, Model: "rerank-v3.5"));

        Assert.Multiple(() =>
        {
            Assert.That(response.IsSuccess, Is.False);
            Assert.That(response.ErrorMessage, Is.Not.Null.And.Not.Empty);
            Assert.That(response.Results, Is.Empty);
            Assert.That(response.RawResponseJson, Does.Contain("invalid api token"));
        });
    }

    [Test]
    public async Task RerankAsync_UsesDefaultModelWhenRequestModelIsNull()
    {
        string? capturedBody = null;
        var handler = OkHandler(body => capturedBody = body);
        var client = CreateClient(handler, out var httpClient,
            options: new CohereClientOptions { DefaultModel = "rerank-english-v3.0" });
        using var httpScope = httpClient;

        var response = await client.RerankAsync(new RerankRequest("q", Documents));

        var root = JsonDocument.Parse(capturedBody!).RootElement;
        Assert.Multiple(() =>
        {
            Assert.That(root.GetProperty("model").GetString(), Is.EqualTo("rerank-english-v3.0"));
            Assert.That(response.Model, Is.EqualTo("rerank-english-v3.0"));
        });
    }

    [Test]
    public void RerankAsync_ThrowsWhenNoModelIsResolvable()
    {
        var handler = OkHandler();
        var client = CreateClient(handler, out var httpClient);
        using var httpScope = httpClient;

        Assert.That(
            async () => await client.RerankAsync(new RerankRequest("q", Documents)),
            Throws.InstanceOf<InvalidOperationException>());
    }

    [Test]
    public async Task RerankAsync_RequestModelOverridesDefaultModel()
    {
        string? capturedBody = null;
        var handler = OkHandler(body => capturedBody = body);
        var client = CreateClient(handler, out var httpClient,
            options: new CohereClientOptions { DefaultModel = "rerank-english-v3.0" });
        using var httpScope = httpClient;

        await client.RerankAsync(new RerankRequest("q", Documents, Model: "rerank-v3.5"));

        var root = JsonDocument.Parse(capturedBody!).RootElement;
        Assert.That(root.GetProperty("model").GetString(), Is.EqualTo("rerank-v3.5"));
    }
}
