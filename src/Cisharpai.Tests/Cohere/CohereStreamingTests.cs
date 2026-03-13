using System.Net;
using System.Text;
using System.Text.Json;
using Cisharpai.Features.Chat;
using Cisharpai.Models;
using Cisharpai.Cohere;

namespace Cisharpai.Tests.Cohere;

public sealed class CohereStreamingTests
{
    #region SSE Fixtures

    private const string BasicStreamSse = """
        data: {"type":"stream-start","generation_id":"gen-123"}

        data: {"type":"content-start","index":0,"delta":{"type":"text-start","text":""}}

        data: {"type":"content-delta","index":0,"delta":{"type":"text-delta","message":{"content":{"text":"Hello"}}}}

        data: {"type":"content-delta","index":0,"delta":{"type":"text-delta","message":{"content":{"text":", world!"}}}}

        data: {"type":"content-end","index":0,"delta":{"type":"text-end"}}

        data: {"type":"message-end","delta":{"finish_reason":"COMPLETE","usage":{"billed_units":{"input_tokens":10,"output_tokens":5}}}}

        """;

    private const string StreamWithUsageSse = """
        data: {"type":"stream-start"}

        data: {"type":"content-delta","index":0,"delta":{"type":"text-delta","message":{"content":{"text":"Answer"}}}}

        data: {"type":"message-end","delta":{"finish_reason":"COMPLETE","usage":{"billed_units":{"input_tokens":15,"output_tokens":3}}}}

        """;

    #endregion

    private static CohereChatCompletionClient CreateStreamingClient(string sseContent)
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

        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.cohere.com/v2/") };
        var options = new CohereClientOptions { ApiKey = "test-key" };
        return new CohereChatCompletionClient(httpClient, options);
    }

    private static CohereChatCompletionClient CreateCapturingStreamingClient(string sseContent, out Func<string?> getBody)
    {
        string? capturedBody = null;
        var handler = new MockHttpMessageHandler(async (req, ct) =>
        {
            capturedBody = await req.Content!.ReadAsStringAsync(ct);
            var bytes = Encoding.UTF8.GetBytes(sseContent);
            var stream = new MemoryStream(bytes);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StreamContent(stream)
            };
        });

        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.cohere.com/v2/") };
        var options = new CohereClientOptions { ApiKey = "test-key" };
        getBody = () => capturedBody;
        return new CohereChatCompletionClient(httpClient, options);
    }

    [Test]
    public async Task Streams_Text_Chunks_In_Order()
    {
        var client = CreateStreamingClient(BasicStreamSse);

        var request = new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "Say hello")],
            Model: "command-a-03-2025");

        var feature = client.Features.Get<IStreamingChatFeature>();
        Assert.That(feature, Is.Not.Null);

        var chunks = new List<ChatCompletionChunk>();
        await foreach (var chunk in feature!.GetChatCompletionStreamAsync(request))
        {
            chunks.Add(chunk);
        }

        var textChunks = chunks.Where(c => !string.IsNullOrEmpty(c.Content)).ToList();
        Assert.That(textChunks, Has.Count.GreaterThanOrEqualTo(2));

        var combined = string.Concat(textChunks.Select(c => c.Content));
        Assert.That(combined, Is.EqualTo("Hello, world!"));
    }

    [Test]
    public async Task Final_Chunk_Contains_FinishReason()
    {
        var client = CreateStreamingClient(BasicStreamSse);

        var request = new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "Say hello")],
            Model: "command-a-03-2025");

        var feature = client.Features.Get<IStreamingChatFeature>()!;

        var chunks = new List<ChatCompletionChunk>();
        await foreach (var chunk in feature.GetChatCompletionStreamAsync(request))
        {
            chunks.Add(chunk);
        }

        var finalChunk = chunks.Last();
        Assert.That(finalChunk.FinishReason, Is.EqualTo("COMPLETE"));
    }

    [Test]
    public async Task Final_Chunk_Contains_Token_Counts()
    {
        var client = CreateStreamingClient(StreamWithUsageSse);

        var request = new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "Ask something")],
            Model: "command-a-03-2025");

        var feature = client.Features.Get<IStreamingChatFeature>()!;

        var chunks = new List<ChatCompletionChunk>();
        await foreach (var chunk in feature.GetChatCompletionStreamAsync(request))
        {
            chunks.Add(chunk);
        }

        var lastChunk = chunks.Last();
        Assert.Multiple(() =>
        {
            Assert.That(lastChunk.PromptTokens, Is.EqualTo(15));
            Assert.That(lastChunk.CompletionTokens, Is.EqualTo(3));
        });
    }

    [Test]
    public async Task Sets_Stream_True_In_Request()
    {
        var client = CreateCapturingStreamingClient(BasicStreamSse, out var getBody);

        var request = new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "test")],
            Model: "command-a-03-2025");

        var feature = client.Features.Get<IStreamingChatFeature>()!;

        await foreach (var _ in feature.GetChatCompletionStreamAsync(request)) { }

        var body = getBody();
        Assert.Multiple(() =>
        {
            Assert.That(body, Is.Not.Null);
            Assert.That(body, Does.Contain("\"stream\":true"));
        });
    }

    [Test]
    public async Task Model_Is_Set_On_Text_Chunks()
    {
        var client = CreateStreamingClient(BasicStreamSse);

        var request = new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "test")],
            Model: "command-a-03-2025");

        var feature = client.Features.Get<IStreamingChatFeature>()!;

        var chunks = new List<ChatCompletionChunk>();
        await foreach (var chunk in feature.GetChatCompletionStreamAsync(request))
        {
            chunks.Add(chunk);
        }

        var textChunks = chunks.Where(c => !string.IsNullOrEmpty(c.Content)).ToList();
        Assert.That(textChunks, Is.Not.Empty);
        foreach (var chunk in textChunks)
        {
            Assert.That(chunk.Model, Is.EqualTo("command-a-03-2025"));
        }
    }

    [Test]
    public void Feature_Is_Discoverable_Via_IStreamingChatFeature()
    {
        var handler = new MockHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.cohere.com/v2/") };
        var options = new CohereClientOptions { ApiKey = "test-key" };
        var client = new CohereChatCompletionClient(httpClient, options);

        var feature = client.Features.Get<IStreamingChatFeature>();
        Assert.Multiple(() =>
        {
            Assert.That(feature, Is.Not.Null);
            Assert.That(feature, Is.SameAs(client));
        });
    }

    [Test]
    public async Task Skips_Unknown_Event_Types_Gracefully()
    {
        const string sseWithUnknownEvent = """
            data: {"type":"unknown-event","data":"ignored"}

            data: {"type":"content-delta","index":0,"delta":{"type":"text-delta","message":{"content":{"text":"Hello"}}}}

            data: {"type":"message-end","delta":{"finish_reason":"COMPLETE","usage":{"billed_units":{"input_tokens":5,"output_tokens":2}}}}

            """;

        var client = CreateStreamingClient(sseWithUnknownEvent);

        var request = new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "test")],
            Model: "command-a-03-2025");

        var feature = client.Features.Get<IStreamingChatFeature>()!;

        var chunks = new List<ChatCompletionChunk>();
        await foreach (var chunk in feature.GetChatCompletionStreamAsync(request))
        {
            chunks.Add(chunk);
        }

        var textChunks = chunks.Where(c => !string.IsNullOrEmpty(c.Content)).ToList();
        Assert.Multiple(() =>
        {
            Assert.That(textChunks, Has.Count.EqualTo(1));
            Assert.That(textChunks[0].Content, Is.EqualTo("Hello"));
        });
    }

    [Test]
    public async Task Handles_Empty_Stream_Gracefully()
    {
        var client = CreateStreamingClient(string.Empty);

        var request = new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "test")],
            Model: "command-a-03-2025");

        var feature = client.Features.Get<IStreamingChatFeature>()!;

        var chunks = new List<ChatCompletionChunk>();
        await foreach (var chunk in feature.GetChatCompletionStreamAsync(request))
        {
            chunks.Add(chunk);
        }

        Assert.That(chunks, Is.Empty);
    }
}
