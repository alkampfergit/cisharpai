using System.Net;
using System.Text.Json;
using Cisharpai.Cohere;
using Cisharpai.Models;

namespace Cisharpai.Tests.Cohere;

public sealed class CohereMultimodalEmbeddingTests
{
    [Test]
    public async Task GetMultimodalEmbeddingsAsync_TextOnly_MapsToInputsFormat()
    {
        string? capturedBody = null;
        var handler = new MockHttpMessageHandler(async (request, _) =>
        {
            capturedBody = await request.Content!.ReadAsStringAsync();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(MultimodalResponseJson, System.Text.Encoding.UTF8, "application/json")
            };
        });

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.cohere.com/v2/") };
        var client = new CohereEmbeddingClient(httpClient, new CohereClientOptions());

        var inputs = new List<MultimodalEmbeddingInput>
        {
            new([new TextEmbeddingContent("Hello world")])
        };

        await client.GetMultimodalEmbeddingsAsync(inputs, "embed-v4.0");

        Assert.That(capturedBody, Is.Not.Null);
        var doc = JsonDocument.Parse(capturedBody!);
        Assert.That(doc.RootElement.GetProperty("model").GetString(), Is.EqualTo("embed-v4.0"));
        Assert.That(doc.RootElement.GetProperty("inputs").GetArrayLength(), Is.EqualTo(1));

        var content = doc.RootElement.GetProperty("inputs")[0].GetProperty("content");
        Assert.That(content.GetArrayLength(), Is.EqualTo(1));
        Assert.That(content[0].GetProperty("type").GetString(), Is.EqualTo("text"));
        Assert.That(content[0].GetProperty("text").GetString(), Is.EqualTo("Hello world"));

        // texts and images should not be present
        Assert.That(doc.RootElement.TryGetProperty("texts", out _), Is.False);
        Assert.That(doc.RootElement.TryGetProperty("images", out _), Is.False);
    }

    [Test]
    public async Task GetMultimodalEmbeddingsAsync_ImageOnly_MapsToInputsWithDataUri()
    {
        string? capturedBody = null;
        var handler = new MockHttpMessageHandler(async (request, _) =>
        {
            capturedBody = await request.Content!.ReadAsStringAsync();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(MultimodalResponseJson, System.Text.Encoding.UTF8, "application/json")
            };
        });

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.cohere.com/v2/") };
        var client = new CohereEmbeddingClient(httpClient, new CohereClientOptions());

        var imagePath = CreateTestImage(".png");
        try
        {
            var inputs = new List<MultimodalEmbeddingInput>
            {
                new([new ImageEmbeddingContent(imagePath)])
            };

            await client.GetMultimodalEmbeddingsAsync(inputs, "embed-v4.0");

            Assert.That(capturedBody, Is.Not.Null);
            var doc = JsonDocument.Parse(capturedBody!);
            var content = doc.RootElement.GetProperty("inputs")[0].GetProperty("content");
            Assert.That(content[0].GetProperty("type").GetString(), Is.EqualTo("image_url"));

            var imageUrl = content[0].GetProperty("image_url").GetProperty("url").GetString();
            Assert.That(imageUrl, Does.StartWith("data:image/png;base64,"));
        }
        finally
        {
            File.Delete(imagePath);
        }
    }

    [Test]
    public async Task GetMultimodalEmbeddingsAsync_MixedTextAndImage_MapsCorrectly()
    {
        string? capturedBody = null;
        var handler = new MockHttpMessageHandler(async (request, _) =>
        {
            capturedBody = await request.Content!.ReadAsStringAsync();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(MultimodalResponseJson, System.Text.Encoding.UTF8, "application/json")
            };
        });

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.cohere.com/v2/") };
        var client = new CohereEmbeddingClient(httpClient, new CohereClientOptions());

        var imagePath = CreateTestImage(".png");
        try
        {
            var inputs = new List<MultimodalEmbeddingInput>
            {
                new([
                    new TextEmbeddingContent("A chart showing growth"),
                    new ImageEmbeddingContent(imagePath)
                ])
            };

            await client.GetMultimodalEmbeddingsAsync(inputs, "embed-v4.0");

            var doc = JsonDocument.Parse(capturedBody!);
            var content = doc.RootElement.GetProperty("inputs")[0].GetProperty("content");
            Assert.That(content.GetArrayLength(), Is.EqualTo(2));
            Assert.That(content[0].GetProperty("type").GetString(), Is.EqualTo("text"));
            Assert.That(content[1].GetProperty("type").GetString(), Is.EqualTo("image_url"));
        }
        finally
        {
            File.Delete(imagePath);
        }
    }

    [Test]
    public async Task GetMultimodalEmbeddingsAsync_BatchInputs_MapsMultipleInputs()
    {
        string? capturedBody = null;
        var handler = new MockHttpMessageHandler(async (request, _) =>
        {
            capturedBody = await request.Content!.ReadAsStringAsync();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(MultimodalBatchResponseJson, System.Text.Encoding.UTF8, "application/json")
            };
        });

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.cohere.com/v2/") };
        var client = new CohereEmbeddingClient(httpClient, new CohereClientOptions());

        var inputs = new List<MultimodalEmbeddingInput>
        {
            new([new TextEmbeddingContent("First")]),
            new([new TextEmbeddingContent("Second")])
        };

        var response = await client.GetMultimodalEmbeddingsAsync(inputs, "embed-v4.0");

        var doc = JsonDocument.Parse(capturedBody!);
        Assert.That(doc.RootElement.GetProperty("inputs").GetArrayLength(), Is.EqualTo(2));
        Assert.That(response.Embeddings, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task GetMultimodalEmbeddingsAsync_OutputDimension_SentInRequest()
    {
        string? capturedBody = null;
        var handler = new MockHttpMessageHandler(async (request, _) =>
        {
            capturedBody = await request.Content!.ReadAsStringAsync();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(MultimodalResponseJson, System.Text.Encoding.UTF8, "application/json")
            };
        });

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.cohere.com/v2/") };
        var client = new CohereEmbeddingClient(httpClient, new CohereClientOptions());

        var inputs = new List<MultimodalEmbeddingInput>
        {
            new([new TextEmbeddingContent("Test")])
        };

        await client.GetMultimodalEmbeddingsAsync(inputs, "embed-v4.0", outputDimension: 256);

        var doc = JsonDocument.Parse(capturedBody!);
        Assert.That(doc.RootElement.GetProperty("output_dimension").GetInt32(), Is.EqualTo(256));
    }

    [TestCase(EmbeddingInputType.Query, "search_query")]
    [TestCase(EmbeddingInputType.Document, "search_document")]
    [TestCase(EmbeddingInputType.Classification, "classification")]
    [TestCase(EmbeddingInputType.Clustering, "clustering")]
    public async Task GetMultimodalEmbeddingsAsync_InputType_MapsCorrectly(
        EmbeddingInputType inputType, string expectedValue)
    {
        string? capturedBody = null;
        var handler = new MockHttpMessageHandler(async (request, _) =>
        {
            capturedBody = await request.Content!.ReadAsStringAsync();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(MultimodalResponseJson, System.Text.Encoding.UTF8, "application/json")
            };
        });

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.cohere.com/v2/") };
        var client = new CohereEmbeddingClient(httpClient, new CohereClientOptions());

        var inputs = new List<MultimodalEmbeddingInput>
        {
            new([new TextEmbeddingContent("Test")])
        };

        await client.GetMultimodalEmbeddingsAsync(inputs, "embed-v4.0", inputType: inputType);

        var doc = JsonDocument.Parse(capturedBody!);
        Assert.That(doc.RootElement.GetProperty("input_type").GetString(), Is.EqualTo(expectedValue));
    }

    [Test]
    public async Task GetMultimodalEmbeddingsAsync_IncludeRawResponse_ReturnsRawJson()
    {
        var handler = new MockHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(MultimodalResponseJson, System.Text.Encoding.UTF8, "application/json")
            }));

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.cohere.com/v2/") };
        var client = new CohereEmbeddingClient(httpClient, new CohereClientOptions());

        var inputs = new List<MultimodalEmbeddingInput>
        {
            new([new TextEmbeddingContent("Test")])
        };

        var response = await client.GetMultimodalEmbeddingsAsync(
            inputs, "embed-v4.0", includeRawResponse: true);

        Assert.That(response.RawResponseJson, Is.Not.Null);
        Assert.That(response.RawRequestJson, Is.Not.Null);
    }

    [Test]
    public async Task GetMultimodalEmbeddingsAsync_HttpError_ReturnsErrorResponse()
    {
        var handler = new MockHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent("""{"message":"Invalid request"}""",
                    System.Text.Encoding.UTF8, "application/json")
            }));

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.cohere.com/v2/") };
        var client = new CohereEmbeddingClient(httpClient, new CohereClientOptions());

        var inputs = new List<MultimodalEmbeddingInput>
        {
            new([new TextEmbeddingContent("Test")])
        };

        var response = await client.GetMultimodalEmbeddingsAsync(inputs, "embed-v4.0");

        Assert.That(response.IsSuccess, Is.False);
        Assert.That(response.ErrorMessage, Is.Not.Null.And.Not.Empty);
        Assert.That(response.Embeddings, Is.Empty);
    }

    [Test]
    public async Task GetMultimodalEmbeddingsAsync_MapsResponseWithImageTokens()
    {
        var handler = new MockHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(MultimodalImageTokensResponseJson, System.Text.Encoding.UTF8, "application/json")
            }));

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.cohere.com/v2/") };
        var client = new CohereEmbeddingClient(httpClient, new CohereClientOptions());

        var inputs = new List<MultimodalEmbeddingInput>
        {
            new([new TextEmbeddingContent("Test")])
        };

        var response = await client.GetMultimodalEmbeddingsAsync(inputs, "embed-v4.0");

        Assert.That(response.IsSuccess, Is.True);
        Assert.That(response.TotalTokens, Is.EqualTo(1610), "Should sum input_tokens (10) + image_tokens (1600)");
    }

    [Test]
    public async Task GetMultimodalEmbeddingsAsync_OutputDimensionNull_OmittedFromRequest()
    {
        string? capturedBody = null;
        var handler = new MockHttpMessageHandler(async (request, _) =>
        {
            capturedBody = await request.Content!.ReadAsStringAsync();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(MultimodalResponseJson, System.Text.Encoding.UTF8, "application/json")
            };
        });

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.cohere.com/v2/") };
        var client = new CohereEmbeddingClient(httpClient, new CohereClientOptions());

        var inputs = new List<MultimodalEmbeddingInput>
        {
            new([new TextEmbeddingContent("Test")])
        };

        await client.GetMultimodalEmbeddingsAsync(inputs, "embed-v4.0");

        var doc = JsonDocument.Parse(capturedBody!);
        Assert.That(doc.RootElement.TryGetProperty("output_dimension", out _), Is.False,
            "output_dimension should be omitted when null");
    }

    private static string CreateTestImage(string extension)
    {
        var path = Path.Combine(Path.GetTempPath(), $"test-image-{Guid.NewGuid()}{extension}");
        File.WriteAllBytes(path, [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);
        return path;
    }

    private const string MultimodalResponseJson = """
        {
            "id": "emb-mm-123",
            "embeddings": {
                "float": [
                    [0.1, 0.2, 0.3]
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

    private const string MultimodalBatchResponseJson = """
        {
            "id": "emb-mm-456",
            "embeddings": {
                "float": [
                    [0.1, 0.2, 0.3],
                    [0.4, 0.5, 0.6]
                ]
            },
            "texts": [],
            "meta": {
                "billed_units": {
                    "input_tokens": 10
                }
            }
        }
        """;

    private const string MultimodalImageTokensResponseJson = """
        {
            "id": "emb-mm-789",
            "embeddings": {
                "float": [
                    [0.1, 0.2, 0.3]
                ]
            },
            "texts": [],
            "meta": {
                "billed_units": {
                    "input_tokens": 10,
                    "images": 1,
                    "image_tokens": 1600
                }
            }
        }
        """;
}
