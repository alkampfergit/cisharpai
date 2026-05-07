using System.Net;
using System.Text.Json;
using Cisharpai.Azure.AzureAiInference;
using Cisharpai.Models;

namespace Cisharpai.Tests.Azure.AzureAiInference;

public sealed class AzureAiInferenceEmbeddingClientTests
{
    [Test]
    public async Task GetEmbeddingsAsync_SingleTextInput_SendsAsArray()
    {
        string? capturedBody = null;
        var handler = new MockHttpMessageHandler(async (request, _) =>
        {
            capturedBody = await request.Content!.ReadAsStringAsync(CancellationToken.None);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(EmbeddingResponseJson, System.Text.Encoding.UTF8, "application/json")
            };
        });

        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://test.inference.azure.com/")
        };
        var client = new AzureAiInferenceEmbeddingClient(httpClient,
            new AzureAiInferenceClientOptions { ModelId = "text-embedding-model", ApiKey = "key" });

        await client.GetEmbeddingsAsync(new EmbeddingRequest(
            Input: ["Hello world"],
            Model: "text-embedding-model"));

        Assert.That(capturedBody, Is.Not.Null);
        var doc = JsonDocument.Parse(capturedBody!);
        Assert.Multiple(() =>
        {
            Assert.That(doc.RootElement.GetProperty("input").ValueKind, Is.EqualTo(JsonValueKind.Array));
            Assert.That(doc.RootElement.GetProperty("input").GetArrayLength(), Is.EqualTo(1));
            Assert.That(doc.RootElement.GetProperty("input")[0].GetString(), Is.EqualTo("Hello world"));
        });
    }

    [Test]
    public async Task GetEmbeddingsAsync_MapsResponseToSharedModel()
    {
        var handler = new MockHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(EmbeddingResponseJson, System.Text.Encoding.UTF8, "application/json")
            }));

        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://test.inference.azure.com/")
        };
        var client = new AzureAiInferenceEmbeddingClient(httpClient,
            new AzureAiInferenceClientOptions { ModelId = "text-embedding-model", ApiKey = "key" });

        var response = await client.GetEmbeddingsAsync(new EmbeddingRequest(
            Input: ["Hello world"],
            Model: "text-embedding-model"));

        Assert.Multiple(() =>
        {
            Assert.That(response.IsSuccess, Is.True);
            Assert.That(response.Embeddings, Has.Count.EqualTo(1));
            Assert.That(response.Embeddings[0], Has.Length.EqualTo(3));
            Assert.That(response.Dimensions, Is.EqualTo(3));
            Assert.That(response.Model, Is.EqualTo("text-embedding-model"));
        });
    }

    private const string EmbeddingResponseJson = """
        {
            "id": "emb-001",
            "object": "list",
            "model": "text-embedding-model",
            "data": [
                {
                    "index": 0,
                    "object": "embedding",
                    "embedding": [0.1, 0.2, 0.3]
                }
            ],
            "usage": {
                "prompt_tokens": 2,
                "total_tokens": 2
            }
        }
        """;
}