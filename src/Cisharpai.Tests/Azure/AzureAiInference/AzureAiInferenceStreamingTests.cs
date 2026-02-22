using System.Net;
using System.Text;
using Cisharpai.Features.Chat;
using Cisharpai.Models;
using Cisharpai.Azure.AzureAiInference;

namespace Cisharpai.Tests.Azure.AzureAiInference;

public sealed class AzureAiInferenceStreamingTests
{
    private const string StreamSse = """
        data: {"id":"chatcmpl-abc","model":"Phi-3-mini","choices":[{"index":0,"delta":{"role":"assistant","content":""},"finish_reason":null}]}

        data: {"id":"chatcmpl-abc","model":"Phi-3-mini","choices":[{"index":0,"delta":{"content":"Hello"},"finish_reason":null}]}

        data: {"id":"chatcmpl-abc","model":"Phi-3-mini","choices":[{"index":0,"delta":{"content":", world!"},"finish_reason":null}]}

        data: {"id":"chatcmpl-abc","model":"Phi-3-mini","choices":[{"index":0,"delta":{},"finish_reason":"stop"}]}

        data: {"id":"chatcmpl-abc","model":"Phi-3-mini","choices":[],"usage":{"prompt_tokens":10,"completion_tokens":5,"total_tokens":15}}

        data: [DONE]
        """;

    private static AzureAiInferenceChatCompletionClient CreateStreamingClient(string sseContent, string modelId = "")
    {
        var bytes = Encoding.UTF8.GetBytes(sseContent);
        var handler = new MockHttpMessageHandler((_, _) =>
        {
            var stream = new MemoryStream(bytes);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StreamContent(stream)
            });
        });
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://mymodel.eastus.models.ai.azure.com/")
        };
        var options = new AzureAiInferenceClientOptions
        {
            Endpoint = "https://mymodel.eastus.models.ai.azure.com/",
            ApiKey = "test-key",
            ModelId = modelId
        };
        return new AzureAiInferenceChatCompletionClient(httpClient, options);
    }

    private static (AzureAiInferenceChatCompletionClient, Func<string?>) CreateCapturingClient(string sseContent, string modelId = "")
    {
        string? capturedBody = null;
        var bytes = Encoding.UTF8.GetBytes(sseContent);
        var handler = new MockHttpMessageHandler(async (req, _) =>
        {
            capturedBody = await req.Content!.ReadAsStringAsync();
            var stream = new MemoryStream(bytes);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StreamContent(stream)
            };
        });
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://mymodel.eastus.models.ai.azure.com/")
        };
        var options = new AzureAiInferenceClientOptions
        {
            Endpoint = "https://mymodel.eastus.models.ai.azure.com/",
            ApiKey = "test-key",
            ModelId = modelId
        };
        return (new AzureAiInferenceChatCompletionClient(httpClient, options), () => capturedBody);
    }

    [Test]
    public async Task Streams_Text_Chunks_In_Order()
    {
        var client = CreateStreamingClient(StreamSse);

        var request = new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "Say hello")],
            Model: "Phi-3-mini");

        var feature = client.Features.Get<IStreamingChatFeature>()!;

        var chunks = new List<ChatCompletionChunk>();
        await foreach (var chunk in feature.GetChatCompletionStreamAsync(request))
        {
            chunks.Add(chunk);
        }

        var textChunks = chunks.Where(c => !string.IsNullOrEmpty(c.Content)).ToList();
        var combined = string.Concat(textChunks.Select(c => c.Content));
        Assert.That(combined, Is.EqualTo("Hello, world!"));
    }

    [Test]
    public async Task Final_Chunk_Has_FinishReason()
    {
        var client = CreateStreamingClient(StreamSse);

        var request = new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "test")],
            Model: "Phi-3-mini");

        var feature = client.Features.Get<IStreamingChatFeature>()!;
        var chunks = new List<ChatCompletionChunk>();
        await foreach (var chunk in feature.GetChatCompletionStreamAsync(request))
        {
            chunks.Add(chunk);
        }

        var finishChunk = chunks.FirstOrDefault(c => c.FinishReason is not null);
        Assert.That(finishChunk, Is.Not.Null);
        Assert.That(finishChunk!.FinishReason, Is.EqualTo("stop"));
    }

    [Test]
    public async Task Usage_Chunk_Contains_Token_Counts()
    {
        var client = CreateStreamingClient(StreamSse);

        var request = new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "test")],
            Model: "Phi-3-mini");

        var feature = client.Features.Get<IStreamingChatFeature>()!;
        var chunks = new List<ChatCompletionChunk>();
        await foreach (var chunk in feature.GetChatCompletionStreamAsync(request))
        {
            chunks.Add(chunk);
        }

        var usageChunk = chunks.FirstOrDefault(c => c.PromptTokens.HasValue);
        Assert.That(usageChunk, Is.Not.Null);
        Assert.That(usageChunk!.PromptTokens, Is.EqualTo(10));
        Assert.That(usageChunk.CompletionTokens, Is.EqualTo(5));
    }

    [Test]
    public async Task Sets_Stream_True_In_Request()
    {
        var (client, getBody) = CreateCapturingClient(StreamSse);

        var request = new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "test")],
            Model: "Phi-3-mini");

        var feature = client.Features.Get<IStreamingChatFeature>()!;
        await foreach (var _ in feature.GetChatCompletionStreamAsync(request)) { }

        var body = getBody()!;
        Assert.That(body, Does.Contain("\"stream\":true"));
    }

    [Test]
    public void Feature_Is_Discoverable_Via_IStreamingChatFeature()
    {
        var handler = new MockHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://mymodel.eastus.models.ai.azure.com/")
        };
        var options = new AzureAiInferenceClientOptions
        {
            Endpoint = "https://mymodel.eastus.models.ai.azure.com/",
            ApiKey = "test-key"
        };
        var client = new AzureAiInferenceChatCompletionClient(httpClient, options);

        var feature = client.Features.Get<IStreamingChatFeature>();
        Assert.That(feature, Is.Not.Null);
        Assert.That(feature, Is.SameAs(client));
    }

    [Test]
    public async Task Model_From_Options_Used_When_Request_Model_Null()
    {
        var (client, getBody) = CreateCapturingClient(StreamSse, "Phi-3-mini-options");

        var request = new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "test")],
            Model: null);

        var feature = client.Features.Get<IStreamingChatFeature>()!;
        await foreach (var _ in feature.GetChatCompletionStreamAsync(request)) { }

        var body = getBody()!;
        Assert.That(body, Does.Contain("Phi-3-mini-options"));
    }
}
