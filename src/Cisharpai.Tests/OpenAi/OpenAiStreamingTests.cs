using System.Net;
using System.Text;
using Cisharpai.Features.Chat;
using Cisharpai.Models;
using Cisharpai.OpenAi;

namespace Cisharpai.Tests.OpenAi;

public sealed class OpenAiStreamingTests
{
    // Chat Completions API SSE format
    private const string ChatCompletionStreamSse = """
        data: {"id":"chatcmpl-abc","model":"gpt-4o","choices":[{"index":0,"delta":{"role":"assistant","content":""},"finish_reason":null}]}

        data: {"id":"chatcmpl-abc","model":"gpt-4o","choices":[{"index":0,"delta":{"content":"Hello"},"finish_reason":null}]}

        data: {"id":"chatcmpl-abc","model":"gpt-4o","choices":[{"index":0,"delta":{"content":", world!"},"finish_reason":null}]}

        data: {"id":"chatcmpl-abc","model":"gpt-4o","choices":[{"index":0,"delta":{},"finish_reason":"stop"}]}

        data: {"id":"chatcmpl-abc","model":"gpt-4o","choices":[],"usage":{"prompt_tokens":10,"completion_tokens":5,"total_tokens":15}}

        data: [DONE]
        """;

    // Responses API SSE format (GPT-5)
    private const string ResponsesApiStreamSse = """
        data: {"type":"response.output_text.delta","delta":"Hello"}

        data: {"type":"response.output_text.delta","delta":", world!"}

        data: {"type":"response.completed","response":{"model":"gpt-5","status":"completed","usage":{"input_tokens":10,"output_tokens":5}}}

        data: [DONE]
        """;

    private static (OpenAiChatCompletionClient, Func<string?>) CreateCapturingClient(string sseContent)
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

        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.openai.com/v1/") };
        var options = new OpenAiClientOptions { ApiKey = "test-key" };
        return (new OpenAiChatCompletionClient(httpClient, options), () => capturedBody);
    }

    private static OpenAiChatCompletionClient CreateStreamingClient(string sseContent)
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
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.openai.com/v1/") };
        var options = new OpenAiChatCompletionClient(httpClient, new OpenAiClientOptions { ApiKey = "test-key" });
        return options;
    }

    [Test]
    public async Task Legacy_Chat_Streams_Text_Chunks()
    {
        var client = CreateStreamingClient(ChatCompletionStreamSse);

        var request = new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "Say hello")],
            Model: "gpt-4o");

        var feature = client.Features.Get<IStreamingChatFeature>()!;

        var chunks = new List<ChatCompletionChunk>();
        await foreach (var chunk in feature.GetChatCompletionStreamAsync(request))
        {
            chunks.Add(chunk);
        }

        var textChunks = chunks.Where(c => !string.IsNullOrEmpty(c.Content)).ToList();
        Assert.That(textChunks, Has.Count.GreaterThanOrEqualTo(2));

        var combined = string.Concat(textChunks.Select(c => c.Content));
        Assert.That(combined, Is.EqualTo("Hello, world!"));
    }

    [Test]
    public async Task Legacy_Chat_Final_Chunk_Has_FinishReason()
    {
        var client = CreateStreamingClient(ChatCompletionStreamSse);

        var request = new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "Say hello")],
            Model: "gpt-4o");

        var feature = client.Features.Get<IStreamingChatFeature>()!;
        var chunks = new List<ChatCompletionChunk>();
        await foreach (var chunk in feature.GetChatCompletionStreamAsync(request))
        {
            chunks.Add(chunk);
        }

        var finishChunk = chunks.FirstOrDefault(c => c.FinishReason is not null);
        Assert.Multiple(() =>
        {
            Assert.That(finishChunk, Is.Not.Null);
            Assert.That(finishChunk!.FinishReason, Is.EqualTo("stop"));
        });
    }

    [Test]
    public async Task Legacy_Chat_Usage_Chunk_Contains_Tokens()
    {
        var client = CreateStreamingClient(ChatCompletionStreamSse);

        var request = new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "test")],
            Model: "gpt-4o");

        var feature = client.Features.Get<IStreamingChatFeature>()!;
        var chunks = new List<ChatCompletionChunk>();
        await foreach (var chunk in feature.GetChatCompletionStreamAsync(request))
        {
            chunks.Add(chunk);
        }

        var usageChunk = chunks.FirstOrDefault(c => c.PromptTokens.HasValue);
        Assert.Multiple(() =>
        {
            Assert.That(usageChunk, Is.Not.Null);
            Assert.That(usageChunk!.PromptTokens, Is.EqualTo(10));
            Assert.That(usageChunk.CompletionTokens, Is.EqualTo(5));
        });
    }

    [Test]
    public async Task Legacy_Chat_Sets_Stream_True_In_Request()
    {
        var (client, getBody) = CreateCapturingClient(ChatCompletionStreamSse);

        var request = new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "test")],
            Model: "gpt-4o");

        var feature = client.Features.Get<IStreamingChatFeature>()!;
        await foreach (var _ in feature.GetChatCompletionStreamAsync(request)) { }

        var body = getBody()!;
        Assert.That(body, Does.Contain("\"stream\":true"));
    }

    [Test]
    public async Task Responses_Api_Streams_Text_For_Gpt5()
    {
        var client = CreateStreamingClient(ResponsesApiStreamSse);

        var request = new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "Say hello")],
            Model: "gpt-5");

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
    public async Task Responses_Api_Final_Chunk_Has_Status()
    {
        var client = CreateStreamingClient(ResponsesApiStreamSse);

        var request = new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "test")],
            Model: "gpt-5");

        var feature = client.Features.Get<IStreamingChatFeature>()!;
        var chunks = new List<ChatCompletionChunk>();
        await foreach (var chunk in feature.GetChatCompletionStreamAsync(request))
        {
            chunks.Add(chunk);
        }

        var completedChunk = chunks.FirstOrDefault(c => c.FinishReason is not null);
        Assert.Multiple(() =>
        {
            Assert.That(completedChunk, Is.Not.Null);
            Assert.That(completedChunk!.FinishReason, Is.EqualTo("completed"));
            Assert.That(completedChunk.Model, Is.EqualTo("gpt-5"));
        });
    }

    [Test]
    public void Feature_Is_Discoverable_Via_IStreamingChatFeature()
    {
        var handler = new MockHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.openai.com/v1/") };
        var client = new OpenAiChatCompletionClient(httpClient, new OpenAiClientOptions { ApiKey = "test-key" });

        var feature = client.Features.Get<IStreamingChatFeature>();
        Assert.Multiple(() =>
        {
            Assert.That(feature, Is.Not.Null);
            Assert.That(feature, Is.SameAs(client));
        });
    }

    [Test]
    public async Task Chunks_Have_Model_Set()
    {
        var client = CreateStreamingClient(ChatCompletionStreamSse);

        var request = new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "test")],
            Model: "gpt-4o");

        var feature = client.Features.Get<IStreamingChatFeature>()!;
        var chunks = new List<ChatCompletionChunk>();
        await foreach (var chunk in feature.GetChatCompletionStreamAsync(request))
        {
            chunks.Add(chunk);
        }

        var textChunks = chunks.Where(c => !string.IsNullOrEmpty(c.Content) && c.Model is not null).ToList();
        Assert.Multiple(() =>
        {
            Assert.That(textChunks, Is.Not.Empty);
            Assert.That(textChunks[0].Model, Is.EqualTo("gpt-4o"));
        });
    }
}
