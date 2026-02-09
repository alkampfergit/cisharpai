using System.Net;
using System.Text.Json;
using Cisharpai.Models;
using Cisharpai.Cohere;

namespace Cisharpai.Tests.Cohere;

public sealed class CohereDefaultModelTests
{
    private const string ChatResponseJson = """
        {
            "id": "abc-123",
            "finish_reason": "COMPLETE",
            "message": {
                "role": "assistant",
                "content": [
                    { "type": "text", "text": "Hello" }
                ]
            },
            "usage": {
                "billed_units": { "input_tokens": 10, "output_tokens": 5 },
                "tokens": { "input_tokens": 100, "output_tokens": 5 }
            }
        }
        """;

    private const string EmbeddingResponseJson = """
        {
            "id": "emb-123",
            "embeddings": {
                "float": [[0.1, 0.2, 0.3]]
            },
            "texts": ["Hello"],
            "meta": {
                "api_version": { "version": "2" },
                "billed_units": { "input_tokens": 5 }
            }
        }
        """;

    [Test]
    public async Task ChatClient_UsesDefaultModel_WhenRequestModelIsNull()
    {
        var handler = new FakeHttpHandler(ChatResponseJson);
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.cohere.com/v2/") };
        var options = new CohereClientOptions { DefaultModel = CohereModels.Chat.CommandA };
        var client = new CohereChatCompletionClient(httpClient, options);

        var request = new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "Hi")],
            IncludeRawResponse: true);

        var response = await client.GetChatCompletionAsync(request);

        Assert.That(response.IsSuccess, Is.True);
        var rawRequest = JsonDocument.Parse(response.RawRequestJson!);
        Assert.That(rawRequest.RootElement.GetProperty("model").GetString(),
            Is.EqualTo("command-a-03-2025"));
    }

    [Test]
    public async Task ChatClient_UsesRequestModel_WhenBothAreSet()
    {
        var handler = new FakeHttpHandler(ChatResponseJson);
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.cohere.com/v2/") };
        var options = new CohereClientOptions { DefaultModel = CohereModels.Chat.CommandA };
        var client = new CohereChatCompletionClient(httpClient, options);

        var request = new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "Hi")],
            Model: "command-r-08-2024",
            IncludeRawResponse: true);

        var response = await client.GetChatCompletionAsync(request);

        Assert.That(response.IsSuccess, Is.True);
        var rawRequest = JsonDocument.Parse(response.RawRequestJson!);
        Assert.That(rawRequest.RootElement.GetProperty("model").GetString(),
            Is.EqualTo("command-r-08-2024"));
    }

    [Test]
    public void ChatClient_ThrowsInvalidOperationException_WhenNoModelSpecified()
    {
        using var httpClient = new HttpClient { BaseAddress = new Uri("https://api.cohere.com/v2/") };
        var options = new CohereClientOptions();
        var client = new CohereChatCompletionClient(httpClient, options);

        var request = new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "Hi")]);

        Assert.ThrowsAsync<InvalidOperationException>(
            async () => await client.GetChatCompletionAsync(request));
    }

    [Test]
    public async Task EmbeddingClient_UsesDefaultModel_WhenRequestModelIsNull()
    {
        var handler = new FakeHttpHandler(EmbeddingResponseJson);
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.cohere.com/v2/") };
        var options = new CohereClientOptions { DefaultModel = CohereModels.Embedding.EmbedEnglishV3 };
        var client = new CohereEmbeddingClient(httpClient, options);

        var request = new EmbeddingRequest(
            Input: ["Hello"],
            IncludeRawResponse: true);

        var response = await client.GetEmbeddingsAsync(request);

        Assert.That(response.IsSuccess, Is.True);
        var rawRequest = JsonDocument.Parse(response.RawRequestJson!);
        Assert.That(rawRequest.RootElement.GetProperty("model").GetString(),
            Is.EqualTo("embed-english-v3.0"));
    }

    [Test]
    public void EmbeddingClient_ThrowsInvalidOperationException_WhenNoModelSpecified()
    {
        using var httpClient = new HttpClient { BaseAddress = new Uri("https://api.cohere.com/v2/") };
        var options = new CohereClientOptions();
        var client = new CohereEmbeddingClient(httpClient, options);

        var request = new EmbeddingRequest(Input: ["Hello"]);

        Assert.ThrowsAsync<InvalidOperationException>(
            async () => await client.GetEmbeddingsAsync(request));
    }

    [Test]
    public void ModelConstants_HaveExpectedValues()
    {
        Assert.Multiple(() =>
        {
            Assert.That(CohereModels.Chat.CommandA, Is.EqualTo("command-a-03-2025"));
            Assert.That(CohereModels.Chat.CommandRPlus, Is.EqualTo("command-r-plus-08-2024"));
            Assert.That(CohereModels.Chat.CommandR, Is.EqualTo("command-r-08-2024"));
            Assert.That(CohereModels.Embedding.EmbedV4, Is.EqualTo("embed-v4.0"));
            Assert.That(CohereModels.Embedding.EmbedEnglishV3, Is.EqualTo("embed-english-v3.0"));
            Assert.That(CohereModels.Embedding.EmbedMultilingualV3, Is.EqualTo("embed-multilingual-v3.0"));
            Assert.That(CohereModels.Embedding.EmbedEnglishLightV3, Is.EqualTo("embed-english-light-v3.0"));
            Assert.That(CohereModels.Embedding.EmbedMultilingualLightV3, Is.EqualTo("embed-multilingual-light-v3.0"));
        });
    }

    private sealed class FakeHttpHandler(string responseJson) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson, System.Text.Encoding.UTF8, "application/json")
            });
        }
    }
}
