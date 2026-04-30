using System.Net;
using System.Text.Json;
using Cisharpai.Models;
using Cisharpai.Cohere;

namespace Cisharpai.Tests.Cohere;

public sealed class CohereEmbeddingClientTests
{
    [Test]
    public async Task GetEmbeddingsAsync_MapsRequestToCohereFormat()
    {
        string? capturedBody = null;
        var handler = new MockHttpMessageHandler(async (request, _) =>
        {
            capturedBody = await request.Content!.ReadAsStringAsync();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(CohereEmbedResponseJson, System.Text.Encoding.UTF8, "application/json")
            };
        });

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.cohere.com/v2/") };
        var client = new CohereEmbeddingClient(httpClient, new CohereClientOptions());

        var request = new EmbeddingRequest(
            Input: ["Hello world"],
            Model: "embed-english-v3.0",
            InputType: EmbeddingInputType.Document);

        await client.GetEmbeddingsAsync(request);

        Assert.That(capturedBody, Is.Not.Null);
        var doc = JsonDocument.Parse(capturedBody!);
        Assert.Multiple(() =>
        {
            Assert.That(doc.RootElement.GetProperty("model").GetString(), Is.EqualTo("embed-english-v3.0"));
            Assert.That(doc.RootElement.GetProperty("texts").GetArrayLength(), Is.EqualTo(1));
            Assert.That(doc.RootElement.GetProperty("texts")[0].GetString(), Is.EqualTo("Hello world"));
            Assert.That(doc.RootElement.GetProperty("input_type").GetString(), Is.EqualTo("search_document"));
        });
    }

    [Test]
    public async Task GetEmbeddingsAsync_MapsResponseToSharedModel()
    {
        var handler = new MockHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(CohereEmbedResponseJson, System.Text.Encoding.UTF8, "application/json")
            }));

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.cohere.com/v2/") };
        var client = new CohereEmbeddingClient(httpClient, new CohereClientOptions());

        var request = new EmbeddingRequest(
            Input: ["Hello world"],
            Model: "embed-english-v3.0");

        var response = await client.GetEmbeddingsAsync(request);

        Assert.Multiple(() =>
        {
            Assert.That(response.IsSuccess, Is.True);
            Assert.That(response.ErrorMessage, Is.Null);
            Assert.That(response.Embeddings, Has.Count.EqualTo(1));
            Assert.That(response.Embeddings[0], Has.Length.EqualTo(3));
            Assert.That(response.Embeddings[0][0], Is.EqualTo(0.1f).Within(0.001f));
            Assert.That(response.Dimensions, Is.EqualTo(3));
            Assert.That(response.TotalTokens, Is.EqualTo(2));
        });
    }

    [Test]
    public async Task GetEmbeddingsAsync_PostsToEmbedEndpoint()
    {
        var handler = new MockHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(CohereEmbedResponseJson, System.Text.Encoding.UTF8, "application/json")
            }));

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.cohere.com/v2/") };
        var client = new CohereEmbeddingClient(httpClient, new CohereClientOptions());

        await client.GetEmbeddingsAsync(new EmbeddingRequest(
            Input: ["Hi"],
            Model: "embed-english-v3.0"));

        Assert.That(handler.LastRequest!.RequestUri!.PathAndQuery, Does.Contain("embed"));
    }

    [Test]
    public async Task GetEmbeddingsAsync_BatchInput_MapsAllEmbeddings()
    {
        var handler = new MockHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(CohereBatchEmbedResponseJson, System.Text.Encoding.UTF8, "application/json")
            }));

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.cohere.com/v2/") };
        var client = new CohereEmbeddingClient(httpClient, new CohereClientOptions());

        var response = await client.GetEmbeddingsAsync(new EmbeddingRequest(
            Input: ["First", "Second"],
            Model: "embed-english-v3.0"));

        Assert.Multiple(() =>
        {
            Assert.That(response.Embeddings, Has.Count.EqualTo(2));
            Assert.That(response.Embeddings[0][0], Is.EqualTo(0.1f).Within(0.001f));
            Assert.That(response.Embeddings[1][0], Is.EqualTo(0.4f).Within(0.001f));
        });
    }

    [TestCase(EmbeddingInputType.Query, "search_query")]
    [TestCase(EmbeddingInputType.Document, "search_document")]
    [TestCase(EmbeddingInputType.Classification, "classification")]
    [TestCase(EmbeddingInputType.Clustering, "clustering")]
    public async Task GetEmbeddingsAsync_SetsCorrectInputType(EmbeddingInputType inputType, string expectedValue)
    {
        string? capturedBody = null;
        var handler = new MockHttpMessageHandler(async (request, _) =>
        {
            capturedBody = await request.Content!.ReadAsStringAsync();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(CohereEmbedResponseJson, System.Text.Encoding.UTF8, "application/json")
            };
        });

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.cohere.com/v2/") };
        var client = new CohereEmbeddingClient(httpClient, new CohereClientOptions());

        await client.GetEmbeddingsAsync(new EmbeddingRequest(
            Input: ["Test"],
            Model: "embed-english-v3.0",
            InputType: inputType));

        var doc = JsonDocument.Parse(capturedBody!);
        Assert.That(doc.RootElement.GetProperty("input_type").GetString(), Is.EqualTo(expectedValue));
    }

    [Test]
    public async Task GetEmbeddingsAsync_NullInputType_DefaultsToSearchDocument()
    {
        string? capturedBody = null;
        var handler = new MockHttpMessageHandler(async (request, _) =>
        {
            capturedBody = await request.Content!.ReadAsStringAsync();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(CohereEmbedResponseJson, System.Text.Encoding.UTF8, "application/json")
            };
        });

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.cohere.com/v2/") };
        var client = new CohereEmbeddingClient(httpClient, new CohereClientOptions());

        await client.GetEmbeddingsAsync(new EmbeddingRequest(
            Input: ["Test"],
            Model: "embed-english-v3.0"));

        var doc = JsonDocument.Parse(capturedBody!);
        Assert.That(doc.RootElement.GetProperty("input_type").GetString(), Is.EqualTo("search_document"));
    }

    [Test]
    public async Task GetEmbeddingsAsync_HttpError_ReturnsErrorResponse()
    {
        var handler = new MockHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)
            {
                Content = new StringContent("""{"message":"Server error"}""",
                    System.Text.Encoding.UTF8, "application/json")
            }));

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.cohere.com/v2/") };
        var client = new CohereEmbeddingClient(httpClient, new CohereClientOptions());

        var response = await client.GetEmbeddingsAsync(new EmbeddingRequest(
            Input: ["Hi"],
            Model: "embed-english-v3.0"));

        Assert.Multiple(() =>
        {
            Assert.That(response.IsSuccess, Is.False);
            Assert.That(response.ErrorMessage, Does.Contain("500"));
            Assert.That(response.Embeddings, Is.Empty);
        });
    }

    [Test]
    public async Task GetEmbeddingsAsync_IncludeRawResponse_ReturnsRawJson()
    {
        var handler = new MockHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(CohereEmbedResponseJson, System.Text.Encoding.UTF8, "application/json")
            }));

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.cohere.com/v2/") };
        var client = new CohereEmbeddingClient(httpClient, new CohereClientOptions());

        var response = await client.GetEmbeddingsAsync(new EmbeddingRequest(
            Input: ["Hi"],
            Model: "embed-english-v3.0",
            IncludeRawResponse: true));

        Assert.Multiple(() =>
        {
            Assert.That(response.RawResponseJson, Is.Not.Null);
            Assert.That(response.RawRequestJson, Is.Not.Null);
        });
    }

    private const string CohereEmbedResponseJson = """
        {
            "id": "emb-abc123",
            "embeddings": {
                "float": [
                    [0.1, 0.2, 0.3]
                ]
            },
            "texts": ["Hello world"],
            "meta": {
                "billed_units": {
                    "input_tokens": 2
                }
            }
        }
        """;

    private const string CohereBatchEmbedResponseJson = """
        {
            "id": "emb-abc456",
            "embeddings": {
                "float": [
                    [0.1, 0.2, 0.3],
                    [0.4, 0.5, 0.6]
                ]
            },
            "texts": ["First", "Second"],
            "meta": {
                "billed_units": {
                    "input_tokens": 4
                }
            }
        }
        """;
}
