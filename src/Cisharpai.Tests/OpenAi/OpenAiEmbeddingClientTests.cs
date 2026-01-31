using System.Net;
using System.Text.Json;
using Cisharpai.Models;
using Cisharpai.OpenAi;

namespace Cisharpai.Tests.OpenAi;

public sealed class OpenAiEmbeddingClientTests
{
    [Test]
    public async Task GetEmbeddingsAsync_MapsRequestToOpenAiFormat()
    {
        string? capturedBody = null;
        var handler = new MockHttpMessageHandler(async (request, _) =>
        {
            capturedBody = await request.Content!.ReadAsStringAsync();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(SingleEmbeddingResponseJson, System.Text.Encoding.UTF8, "application/json")
            };
        });

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.openai.com/v1/") };
        var client = new OpenAiEmbeddingClient(httpClient);

        var request = new EmbeddingRequest(
            Input: ["Hello world"],
            Model: "text-embedding-3-small",
            Dimensions: 256);

        await client.GetEmbeddingsAsync(request);

        Assert.That(capturedBody, Is.Not.Null);
        var doc = JsonDocument.Parse(capturedBody!);
        Assert.That(doc.RootElement.GetProperty("model").GetString(), Is.EqualTo("text-embedding-3-small"));
        Assert.That(doc.RootElement.GetProperty("input").GetString(), Is.EqualTo("Hello world"));
        Assert.That(doc.RootElement.GetProperty("dimensions").GetInt32(), Is.EqualTo(256));
    }

    [Test]
    public async Task GetEmbeddingsAsync_MapsResponseToSharedModel()
    {
        var handler = new MockHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(SingleEmbeddingResponseJson, System.Text.Encoding.UTF8, "application/json")
            }));

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.openai.com/v1/") };
        var client = new OpenAiEmbeddingClient(httpClient);

        var request = new EmbeddingRequest(
            Input: ["Hello world"],
            Model: "text-embedding-3-small");

        var response = await client.GetEmbeddingsAsync(request);

        Assert.That(response.IsSuccess, Is.True);
        Assert.That(response.ErrorMessage, Is.Null);
        Assert.That(response.Model, Is.EqualTo("text-embedding-3-small"));
        Assert.That(response.TotalTokens, Is.EqualTo(2));
        Assert.That(response.Embeddings, Has.Count.EqualTo(1));
        Assert.That(response.Embeddings[0], Has.Length.EqualTo(3));
        Assert.That(response.Embeddings[0][0], Is.EqualTo(0.1f).Within(0.001f));
        Assert.That(response.Dimensions, Is.EqualTo(3));
    }

    [Test]
    public async Task GetEmbeddingsAsync_PostsToEmbeddingsEndpoint()
    {
        var handler = new MockHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(SingleEmbeddingResponseJson, System.Text.Encoding.UTF8, "application/json")
            }));

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.openai.com/v1/") };
        var client = new OpenAiEmbeddingClient(httpClient);

        await client.GetEmbeddingsAsync(new EmbeddingRequest(
            Input: ["Hi"],
            Model: "text-embedding-3-small"));

        Assert.That(handler.LastRequest!.RequestUri!.PathAndQuery, Does.Contain("embeddings"));
    }

    [Test]
    public async Task GetEmbeddingsAsync_SingleTextInput_SendsAsString()
    {
        string? capturedBody = null;
        var handler = new MockHttpMessageHandler(async (request, _) =>
        {
            capturedBody = await request.Content!.ReadAsStringAsync();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(SingleEmbeddingResponseJson, System.Text.Encoding.UTF8, "application/json")
            };
        });

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.openai.com/v1/") };
        var client = new OpenAiEmbeddingClient(httpClient);

        await client.GetEmbeddingsAsync(new EmbeddingRequest(
            Input: ["Single text"],
            Model: "text-embedding-3-small"));

        var doc = JsonDocument.Parse(capturedBody!);
        Assert.That(doc.RootElement.GetProperty("input").ValueKind, Is.EqualTo(JsonValueKind.String));
    }

    [Test]
    public async Task GetEmbeddingsAsync_BatchTextInput_SendsAsArray()
    {
        string? capturedBody = null;
        var handler = new MockHttpMessageHandler(async (request, _) =>
        {
            capturedBody = await request.Content!.ReadAsStringAsync();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(BatchEmbeddingResponseJson, System.Text.Encoding.UTF8, "application/json")
            };
        });

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.openai.com/v1/") };
        var client = new OpenAiEmbeddingClient(httpClient);

        await client.GetEmbeddingsAsync(new EmbeddingRequest(
            Input: ["First text", "Second text"],
            Model: "text-embedding-3-small"));

        var doc = JsonDocument.Parse(capturedBody!);
        Assert.That(doc.RootElement.GetProperty("input").ValueKind, Is.EqualTo(JsonValueKind.Array));
        Assert.That(doc.RootElement.GetProperty("input").GetArrayLength(), Is.EqualTo(2));
    }

    [Test]
    public async Task GetEmbeddingsAsync_BatchTextInput_MapsAllEmbeddings()
    {
        var handler = new MockHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(BatchEmbeddingResponseJson, System.Text.Encoding.UTF8, "application/json")
            }));

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.openai.com/v1/") };
        var client = new OpenAiEmbeddingClient(httpClient);

        var response = await client.GetEmbeddingsAsync(new EmbeddingRequest(
            Input: ["First", "Second"],
            Model: "text-embedding-3-small"));

        Assert.That(response.Embeddings, Has.Count.EqualTo(2));
        Assert.That(response.Embeddings[0][0], Is.EqualTo(0.1f).Within(0.001f));
        Assert.That(response.Embeddings[1][0], Is.EqualTo(0.4f).Within(0.001f));
    }

    [Test]
    public async Task GetEmbeddingsAsync_PassesDimensionsWhenProvided()
    {
        string? capturedBody = null;
        var handler = new MockHttpMessageHandler(async (request, _) =>
        {
            capturedBody = await request.Content!.ReadAsStringAsync();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(SingleEmbeddingResponseJson, System.Text.Encoding.UTF8, "application/json")
            };
        });

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.openai.com/v1/") };
        var client = new OpenAiEmbeddingClient(httpClient);

        await client.GetEmbeddingsAsync(new EmbeddingRequest(
            Input: ["Test"],
            Model: "text-embedding-3-small",
            Dimensions: 512));

        var doc = JsonDocument.Parse(capturedBody!);
        Assert.That(doc.RootElement.GetProperty("dimensions").GetInt32(), Is.EqualTo(512));
    }

    [Test]
    public async Task GetEmbeddingsAsync_OmitsDimensionsWhenNull()
    {
        string? capturedBody = null;
        var handler = new MockHttpMessageHandler(async (request, _) =>
        {
            capturedBody = await request.Content!.ReadAsStringAsync();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(SingleEmbeddingResponseJson, System.Text.Encoding.UTF8, "application/json")
            };
        });

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.openai.com/v1/") };
        var client = new OpenAiEmbeddingClient(httpClient);

        await client.GetEmbeddingsAsync(new EmbeddingRequest(
            Input: ["Test"],
            Model: "text-embedding-3-small"));

        var doc = JsonDocument.Parse(capturedBody!);
        Assert.That(doc.RootElement.TryGetProperty("dimensions", out _), Is.False);
    }

    [Test]
    public async Task GetEmbeddingsAsync_IncludeRawResponse_ReturnsRawJson()
    {
        var handler = new MockHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(SingleEmbeddingResponseJson, System.Text.Encoding.UTF8, "application/json")
            }));

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.openai.com/v1/") };
        var client = new OpenAiEmbeddingClient(httpClient);

        var response = await client.GetEmbeddingsAsync(new EmbeddingRequest(
            Input: ["Hi"],
            Model: "text-embedding-3-small",
            IncludeRawResponse: true));

        Assert.That(response.RawResponseJson, Is.Not.Null);
        Assert.That(response.RawResponseJson, Does.Contain("text-embedding-3-small"));
        Assert.That(response.RawRequestJson, Is.Not.Null);
    }

    [Test]
    public async Task GetEmbeddingsAsync_WithoutIncludeRawResponse_RawJsonIsNull()
    {
        var handler = new MockHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(SingleEmbeddingResponseJson, System.Text.Encoding.UTF8, "application/json")
            }));

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.openai.com/v1/") };
        var client = new OpenAiEmbeddingClient(httpClient);

        var response = await client.GetEmbeddingsAsync(new EmbeddingRequest(
            Input: ["Hi"],
            Model: "text-embedding-3-small"));

        Assert.That(response.RawResponseJson, Is.Null);
        Assert.That(response.RawRequestJson, Is.Null);
    }

    [Test]
    public async Task GetEmbeddingsAsync_HttpError_ReturnsErrorResponse()
    {
        var handler = new MockHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)
            {
                Content = new StringContent("""{"error":{"message":"Server error"}}""",
                    System.Text.Encoding.UTF8, "application/json")
            }));

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.openai.com/v1/") };
        var client = new OpenAiEmbeddingClient(httpClient);

        var response = await client.GetEmbeddingsAsync(new EmbeddingRequest(
            Input: ["Hi"],
            Model: "text-embedding-3-small"));

        Assert.That(response.IsSuccess, Is.False);
        Assert.That(response.ErrorMessage, Does.Contain("500"));
        Assert.That(response.Embeddings, Is.Empty);
        Assert.That(response.Model, Is.EqualTo(string.Empty));
    }

    [Test]
    public async Task GetEmbeddingsAsync_HttpError_IncludesResponseBodyInRawJson()
    {
        const string errorBody = """{"error":{"message":"Unauthorized"}}""";
        var handler = new MockHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized)
            {
                Content = new StringContent(errorBody, System.Text.Encoding.UTF8, "application/json")
            }));

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.openai.com/v1/") };
        var client = new OpenAiEmbeddingClient(httpClient);

        var response = await client.GetEmbeddingsAsync(new EmbeddingRequest(
            Input: ["Hi"],
            Model: "text-embedding-3-small"));

        Assert.That(response.IsSuccess, Is.False);
        Assert.That(response.RawResponseJson, Is.EqualTo(errorBody));
    }

    [Test]
    public async Task GetEmbeddingsAsync_TokenUsageIsMappedCorrectly()
    {
        var handler = new MockHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(SingleEmbeddingResponseJson, System.Text.Encoding.UTF8, "application/json")
            }));

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.openai.com/v1/") };
        var client = new OpenAiEmbeddingClient(httpClient);

        var response = await client.GetEmbeddingsAsync(new EmbeddingRequest(
            Input: ["Hello world"],
            Model: "text-embedding-3-small"));

        Assert.That(response.TotalTokens, Is.EqualTo(2));
    }

    private const string SingleEmbeddingResponseJson = """
        {
            "object": "list",
            "data": [
                {
                    "object": "embedding",
                    "embedding": [0.1, 0.2, 0.3],
                    "index": 0
                }
            ],
            "model": "text-embedding-3-small",
            "usage": {
                "prompt_tokens": 2,
                "total_tokens": 2
            }
        }
        """;

    private const string BatchEmbeddingResponseJson = """
        {
            "object": "list",
            "data": [
                {
                    "object": "embedding",
                    "embedding": [0.1, 0.2, 0.3],
                    "index": 0
                },
                {
                    "object": "embedding",
                    "embedding": [0.4, 0.5, 0.6],
                    "index": 1
                }
            ],
            "model": "text-embedding-3-small",
            "usage": {
                "prompt_tokens": 4,
                "total_tokens": 4
            }
        }
        """;
}
