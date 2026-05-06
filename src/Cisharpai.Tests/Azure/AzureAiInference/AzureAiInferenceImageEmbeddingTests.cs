using System.Net;
using System.Text.Json;
using Cisharpai.Azure.AzureAiInference;

namespace Cisharpai.Tests.Azure.AzureAiInference;

public sealed class AzureAiInferenceImageEmbeddingTests
{
    [Test]
    public async Task GetImageEmbeddingAsync_MapsRequestWithBase64Image()
    {
        string? capturedBody = null;
        var handler = new MockHttpMessageHandler(async (request, _) =>
        {
            capturedBody = await request.Content!.ReadAsStringAsync(CancellationToken.None);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(ImageEmbeddingResponseJson, System.Text.Encoding.UTF8, "application/json")
            };
        });

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://test.inference.azure.com/") };
        var options = new AzureAiInferenceClientOptions { ModelId = "test-model", ApiKey = "key" };
        var client = new AzureAiInferenceEmbeddingClient(httpClient, options);

        var imagePath = CreateTestImage();
        try
        {
            var response = await client.GetImageEmbeddingAsync(imagePath, "clip-model");

            Assert.That(capturedBody, Is.Not.Null);
            var doc = JsonDocument.Parse(capturedBody!);
            Assert.Multiple(() =>
            {
                Assert.That(doc.RootElement.GetProperty("model").GetString(), Is.EqualTo("clip-model"));
                Assert.That(doc.RootElement.GetProperty("input").GetArrayLength(), Is.EqualTo(1));
                Assert.That(doc.RootElement.GetProperty("input")[0].GetProperty("image").GetString(), Is.Not.Null.And.Not.Empty);
            });
        }
        finally
        {
            File.Delete(imagePath);
        }
    }

    [Test]
    public async Task GetImageEmbeddingAsync_MapsResponseToSharedModel()
    {
        var handler = new MockHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(ImageEmbeddingResponseJson, System.Text.Encoding.UTF8, "application/json")
            }));

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://test.inference.azure.com/") };
        var options = new AzureAiInferenceClientOptions { ModelId = "test-model", ApiKey = "key" };
        var client = new AzureAiInferenceEmbeddingClient(httpClient, options);

        var imagePath = CreateTestImage();
        try
        {
            var response = await client.GetImageEmbeddingAsync(imagePath, "clip-model");

            Assert.Multiple(() =>
            {
                Assert.That(response.IsSuccess, Is.True);
                Assert.That(response.Embeddings, Has.Count.EqualTo(1));
                Assert.That(response.Embeddings[0], Has.Length.EqualTo(3));
                Assert.That(response.Dimensions, Is.EqualTo(3));
            });
        }
        finally
        {
            File.Delete(imagePath);
        }
    }

    [Test]
    public async Task GetImageEmbeddingAsync_UsesOptionsModelId_WhenModelEmpty()
    {
        string? capturedBody = null;
        var handler = new MockHttpMessageHandler(async (request, _) =>
        {
            capturedBody = await request.Content!.ReadAsStringAsync(CancellationToken.None);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(ImageEmbeddingResponseJson, System.Text.Encoding.UTF8, "application/json")
            };
        });

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://test.inference.azure.com/") };
        var options = new AzureAiInferenceClientOptions { ModelId = "fallback-model", ApiKey = "key" };
        var client = new AzureAiInferenceEmbeddingClient(httpClient, options);

        var imagePath = CreateTestImage();
        try
        {
            var response = await client.GetImageEmbeddingAsync(imagePath, "");

            var doc = JsonDocument.Parse(capturedBody!);
            Assert.That(doc.RootElement.GetProperty("model").GetString(), Is.EqualTo("fallback-model"));
        }
        finally
        {
            File.Delete(imagePath);
        }
    }

    [Test]
    public async Task GetImageEmbeddingAsync_HttpError_ReturnsErrorResponse()
    {
        var handler = new MockHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent("""{"error":"Invalid image"}""",
                    System.Text.Encoding.UTF8, "application/json")
            }));

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://test.inference.azure.com/") };
        var options = new AzureAiInferenceClientOptions { ModelId = "test-model", ApiKey = "key" };
        var client = new AzureAiInferenceEmbeddingClient(httpClient, options);

        var imagePath = CreateTestImage();
        try
        {
            var response = await client.GetImageEmbeddingAsync(imagePath, "clip-model");

            Assert.Multiple(() =>
            {
                Assert.That(response.IsSuccess, Is.False);
                Assert.That(response.ErrorMessage, Is.Not.Null.And.Not.Empty);
            });
        }
        finally
        {
            File.Delete(imagePath);
        }
    }

    [Test]
    public void GetImageEmbeddingAsync_EmptyPath_ThrowsArgumentException()
    {
        using var httpClient = new HttpClient { BaseAddress = new Uri("https://test.inference.azure.com/") };
        var options = new AzureAiInferenceClientOptions { ModelId = "test-model", ApiKey = "key" };
        var client = new AzureAiInferenceEmbeddingClient(httpClient, options);

        Assert.ThrowsAsync<ArgumentException>(() => client.GetImageEmbeddingAsync("", "clip-model"));
    }

    [Test]
    public async Task GetImageEmbeddingAsync_PostsToEmbeddingsEndpoint()
    {
        var handler = new MockHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(ImageEmbeddingResponseJson, System.Text.Encoding.UTF8, "application/json")
            }));

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://test.inference.azure.com/") };
        var options = new AzureAiInferenceClientOptions { ModelId = "test-model", ApiKey = "key" };
        var client = new AzureAiInferenceEmbeddingClient(httpClient, options);

        var imagePath = CreateTestImage();
        try
        {
            await client.GetImageEmbeddingAsync(imagePath, "clip-model");

            Assert.That(handler.LastRequest!.RequestUri!.PathAndQuery, Does.Contain("models/embeddings"));
        }
        finally
        {
            File.Delete(imagePath);
        }
    }

    private static string CreateTestImage()
    {
        var path = Path.GetTempFileName();
        File.WriteAllBytes(path, [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);
        return path;
    }

    private const string ImageEmbeddingResponseJson = """
        {
            "id": "emb-img-001",
            "object": "list",
            "model": "clip-model",
            "data": [
                {
                    "index": 0,
                    "object": "embedding",
                    "embedding": [0.1, 0.2, 0.3]
                }
            ],
            "usage": {
                "prompt_tokens": 10,
                "total_tokens": 10
            }
        }
        """;
}
