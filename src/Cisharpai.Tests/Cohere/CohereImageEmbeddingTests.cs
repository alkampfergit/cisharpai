using System.Net;
using System.Text.Json;
using Cisharpai.Cohere;

namespace Cisharpai.Tests.Cohere;

public sealed class CohereImageEmbeddingTests
{
    [Test]
    public async Task GetImageEmbeddingAsync_MapsRequestWithBase64Image()
    {
        string? capturedBody = null;
        var handler = new MockHttpMessageHandler(async (request, _) =>
        {
            capturedBody = await request.Content!.ReadAsStringAsync();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(CohereImageEmbedResponseJson, System.Text.Encoding.UTF8, "application/json")
            };
        });

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.cohere.com/v2/") };
        var client = new CohereEmbeddingClient(httpClient);

        var imagePath = CreateTestImage();
        try
        {
            var response = await client.GetImageEmbeddingAsync(imagePath, "embed-v4.0");

            Assert.That(capturedBody, Is.Not.Null);
            var doc = JsonDocument.Parse(capturedBody!);
            Assert.That(doc.RootElement.GetProperty("model").GetString(), Is.EqualTo("embed-v4.0"));
            Assert.That(doc.RootElement.GetProperty("input_type").GetString(), Is.EqualTo("image"));
            Assert.That(doc.RootElement.GetProperty("images").GetArrayLength(), Is.EqualTo(1));
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
                Content = new StringContent(CohereImageEmbedResponseJson, System.Text.Encoding.UTF8, "application/json")
            }));

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.cohere.com/v2/") };
        var client = new CohereEmbeddingClient(httpClient);

        var imagePath = CreateTestImage();
        try
        {
            var response = await client.GetImageEmbeddingAsync(imagePath, "embed-v4.0");

            Assert.That(response.IsSuccess, Is.True);
            Assert.That(response.Embeddings, Has.Count.EqualTo(1));
            Assert.That(response.Embeddings[0], Has.Length.EqualTo(3));
            Assert.That(response.Dimensions, Is.EqualTo(3));
            Assert.That(response.TotalTokens, Is.EqualTo(5));
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
                Content = new StringContent("""{"message":"Invalid image"}""",
                    System.Text.Encoding.UTF8, "application/json")
            }));

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.cohere.com/v2/") };
        var client = new CohereEmbeddingClient(httpClient);

        var imagePath = CreateTestImage();
        try
        {
            var response = await client.GetImageEmbeddingAsync(imagePath, "embed-v4.0");

            Assert.That(response.IsSuccess, Is.False);
            Assert.That(response.ErrorMessage, Is.Not.Null.And.Not.Empty);
        }
        finally
        {
            File.Delete(imagePath);
        }
    }

    [Test]
    public async Task GetImageEmbeddingAsync_SendsDataUriFormat()
    {
        string? capturedBody = null;
        var handler = new MockHttpMessageHandler(async (request, _) =>
        {
            capturedBody = await request.Content!.ReadAsStringAsync();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(CohereImageEmbedResponseJson, System.Text.Encoding.UTF8, "application/json")
            };
        });

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.cohere.com/v2/") };
        var client = new CohereEmbeddingClient(httpClient);

        var imagePath = CreateTestImage();
        try
        {
            await client.GetImageEmbeddingAsync(imagePath, "embed-v4.0");

            var doc = JsonDocument.Parse(capturedBody!);
            var imageValue = doc.RootElement.GetProperty("images")[0].GetString();
            Assert.That(imageValue, Does.StartWith("data:image/"));
        }
        finally
        {
            File.Delete(imagePath);
        }
    }

    [Test]
    public void GetImageEmbeddingAsync_EmptyPath_ThrowsArgumentException()
    {
        using var httpClient = new HttpClient { BaseAddress = new Uri("https://api.cohere.com/v2/") };
        var client = new CohereEmbeddingClient(httpClient);

        Assert.ThrowsAsync<ArgumentException>(() => client.GetImageEmbeddingAsync("", "embed-v4.0"));
    }

    private static string CreateTestImage()
    {
        var path = Path.Combine(Path.GetTempPath(), $"test-image-{Guid.NewGuid()}.png");
        // Write minimal PNG-like bytes for testing
        File.WriteAllBytes(path, [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);
        return path;
    }

    private const string CohereImageEmbedResponseJson = """
        {
            "id": "emb-img-123",
            "embeddings": {
                "float": [
                    [0.5, 0.6, 0.7]
                ]
            },
            "texts": [],
            "meta": {
                "billed_units": {
                    "input_tokens": 5
                }
            }
        }
        """;
}
