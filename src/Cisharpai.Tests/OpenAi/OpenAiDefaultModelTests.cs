using System.Net;
using System.Text.Json;
using Cisharpai.Models;
using Cisharpai.OpenAi;

namespace Cisharpai.Tests.OpenAi;

public sealed class OpenAiDefaultModelTests
{
    private const string ChatResponseJson = """
        {
            "id": "chatcmpl-1",
            "model": "gpt-4.1-nano",
            "choices": [
                {
                    "index": 0,
                    "message": { "role": "assistant", "content": "Hello" },
                    "finish_reason": "stop"
                }
            ],
            "usage": { "prompt_tokens": 10, "completion_tokens": 5, "total_tokens": 15 }
        }
        """;

    private const string EmbeddingResponseJson = """
        {
            "object": "list",
            "model": "text-embedding-3-small",
            "data": [
                { "object": "embedding", "index": 0, "embedding": [0.1, 0.2, 0.3] }
            ],
            "usage": { "prompt_tokens": 5, "total_tokens": 5 }
        }
        """;

    [Test]
    public async Task ChatClient_UsesDefaultModel_WhenRequestModelIsNull()
    {
        var handler = new FakeHttpHandler(ChatResponseJson);
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.openai.com/v1/") };
        var options = new OpenAiClientOptions { DefaultModel = OpenAiModels.Chat.Gpt4_1Nano };
        var client = new OpenAiChatCompletionClient(httpClient, options);

        var request = new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "Hi")],
            IncludeRawResponse: true);

        var response = await client.GetChatCompletionAsync(request);

        Assert.That(response.IsSuccess, Is.True);
        var rawRequest = JsonDocument.Parse(response.RawRequestJson!);
        Assert.That(rawRequest.RootElement.GetProperty("model").GetString(), Is.EqualTo("gpt-4.1-nano"));
    }

    [Test]
    public async Task ChatClient_UsesRequestModel_WhenBothAreSet()
    {
        var handler = new FakeHttpHandler(ChatResponseJson);
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.openai.com/v1/") };
        var options = new OpenAiClientOptions { DefaultModel = OpenAiModels.Chat.Gpt4_1Nano };
        var client = new OpenAiChatCompletionClient(httpClient, options);

        var request = new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "Hi")],
            Model: "gpt-4o",
            IncludeRawResponse: true);

        var response = await client.GetChatCompletionAsync(request);

        Assert.That(response.IsSuccess, Is.True);
        var rawRequest = JsonDocument.Parse(response.RawRequestJson!);
        Assert.That(rawRequest.RootElement.GetProperty("model").GetString(), Is.EqualTo("gpt-4o"));
    }

    [Test]
    public void ChatClient_ThrowsInvalidOperationException_WhenNoModelSpecified()
    {
        using var httpClient = new HttpClient { BaseAddress = new Uri("https://api.openai.com/v1/") };
        var options = new OpenAiClientOptions();
        var client = new OpenAiChatCompletionClient(httpClient, options);

        var request = new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "Hi")]);

        Assert.ThrowsAsync<InvalidOperationException>(
            async () => await client.GetChatCompletionAsync(request));
    }

    [Test]
    public async Task EmbeddingClient_UsesDefaultModel_WhenRequestModelIsNull()
    {
        var handler = new FakeHttpHandler(EmbeddingResponseJson);
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.openai.com/v1/") };
        var options = new OpenAiClientOptions { DefaultModel = OpenAiModels.Embedding.TextEmbedding3Small };
        var client = new OpenAiEmbeddingClient(httpClient, options);

        var request = new EmbeddingRequest(
            Input: ["Hello"],
            IncludeRawResponse: true);

        var response = await client.GetEmbeddingsAsync(request);

        Assert.That(response.IsSuccess, Is.True);
        var rawRequest = JsonDocument.Parse(response.RawRequestJson!);
        Assert.That(rawRequest.RootElement.GetProperty("model").GetString(),
            Is.EqualTo("text-embedding-3-small"));
    }

    [Test]
    public void EmbeddingClient_ThrowsInvalidOperationException_WhenNoModelSpecified()
    {
        using var httpClient = new HttpClient { BaseAddress = new Uri("https://api.openai.com/v1/") };
        var options = new OpenAiClientOptions();
        var client = new OpenAiEmbeddingClient(httpClient, options);

        var request = new EmbeddingRequest(Input: ["Hello"]);

        Assert.ThrowsAsync<InvalidOperationException>(
            async () => await client.GetEmbeddingsAsync(request));
    }

    [Test]
    public void ModelConstants_HaveExpectedValues()
    {
        Assert.Multiple(() =>
        {
            Assert.That(OpenAiModels.Chat.Gpt4_1, Is.EqualTo("gpt-4.1"));
            Assert.That(OpenAiModels.Chat.Gpt4_1Mini, Is.EqualTo("gpt-4.1-mini"));
            Assert.That(OpenAiModels.Chat.Gpt4_1Nano, Is.EqualTo("gpt-4.1-nano"));
            Assert.That(OpenAiModels.Chat.Gpt4o, Is.EqualTo("gpt-4o"));
            Assert.That(OpenAiModels.Chat.Gpt4oMini, Is.EqualTo("gpt-4o-mini"));
            Assert.That(OpenAiModels.Chat.O3, Is.EqualTo("o3"));
            Assert.That(OpenAiModels.Chat.O3Mini, Is.EqualTo("o3-mini"));
            Assert.That(OpenAiModels.Chat.O1, Is.EqualTo("o1"));
            Assert.That(OpenAiModels.Embedding.TextEmbedding3Small, Is.EqualTo("text-embedding-3-small"));
            Assert.That(OpenAiModels.Embedding.TextEmbedding3Large, Is.EqualTo("text-embedding-3-large"));
            Assert.That(OpenAiModels.Embedding.TextEmbeddingAda002, Is.EqualTo("text-embedding-ada-002"));
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
