using System.Net;
using System.Text;
using Cisharpai.Features.Chat;
using Cisharpai.Models;
using Cisharpai.Anthropic;

namespace Cisharpai.Tests.Anthropic;

public sealed class AnthropicStreamingTests
{
    // Anthropic uses event: / data: pairs, no [DONE] marker
    private const string BasicStreamSse = """
        event: message_start
        data: {"type":"message_start","message":{"id":"msg-abc","type":"message","role":"assistant","model":"claude-3-5-sonnet-20241022","content":[],"stop_reason":null,"usage":{"input_tokens":10,"output_tokens":1}}}

        event: content_block_start
        data: {"type":"content_block_start","index":0,"content_block":{"type":"text","text":""}}

        event: content_block_delta
        data: {"type":"content_block_delta","index":0,"delta":{"type":"text_delta","text":"Hello"}}

        event: content_block_delta
        data: {"type":"content_block_delta","index":0,"delta":{"type":"text_delta","text":", world!"}}

        event: content_block_stop
        data: {"type":"content_block_stop","index":0}

        event: message_delta
        data: {"type":"message_delta","delta":{"stop_reason":"end_turn"},"usage":{"output_tokens":5}}

        event: message_stop
        data: {"type":"message_stop"}

        """;

    private static AnthropicChatCompletionClient CreateStreamingClient(string sseContent)
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
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.anthropic.com/v1/") };
        var options = new AnthropicClientOptions { ApiKey = "test-key" };
        return new AnthropicChatCompletionClient(httpClient, options);
    }

    private static (AnthropicChatCompletionClient, Func<string?>) CreateCapturingClient(string sseContent)
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
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.anthropic.com/v1/") };
        var options = new AnthropicClientOptions { ApiKey = "test-key" };
        return (new AnthropicChatCompletionClient(httpClient, options), () => capturedBody);
    }

    [Test]
    public async Task Streams_Text_Chunks_In_Order()
    {
        var client = CreateStreamingClient(BasicStreamSse);

        var request = new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "Say hello")],
            Model: "claude-3-5-sonnet-20241022");

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
    public async Task Final_Chunk_Has_StopReason()
    {
        var client = CreateStreamingClient(BasicStreamSse);

        var request = new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "test")],
            Model: "claude-3-5-sonnet-20241022");

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
            Assert.That(finishChunk!.FinishReason, Is.EqualTo("end_turn"));
        });
    }

    [Test]
    public async Task Message_Start_Provides_Input_Tokens()
    {
        var client = CreateStreamingClient(BasicStreamSse);

        var request = new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "test")],
            Model: "claude-3-5-sonnet-20241022");

        var feature = client.Features.Get<IStreamingChatFeature>()!;
        var chunks = new List<ChatCompletionChunk>();
        await foreach (var chunk in feature.GetChatCompletionStreamAsync(request))
        {
            chunks.Add(chunk);
        }

        // The message_delta chunk should have output_tokens
        var usageChunk = chunks.FirstOrDefault(c => c.CompletionTokens.HasValue);
        Assert.Multiple(() =>
        {
            Assert.That(usageChunk, Is.Not.Null);
            Assert.That(usageChunk!.CompletionTokens, Is.EqualTo(5));
        });
    }

    [Test]
    public async Task Sets_Stream_True_In_Request()
    {
        var (client, getBody) = CreateCapturingClient(BasicStreamSse);

        var request = new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "test")],
            Model: "claude-3-5-sonnet-20241022");

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
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.anthropic.com/v1/") };
        var client = new AnthropicChatCompletionClient(httpClient, new AnthropicClientOptions { ApiKey = "test-key" });

        var feature = client.Features.Get<IStreamingChatFeature>();
        Assert.Multiple(() =>
        {
            Assert.That(feature, Is.Not.Null);
            Assert.That(feature, Is.SameAs(client));
        });
    }

    [Test]
    public async Task Chunks_Have_Model_From_Message_Start()
    {
        var client = CreateStreamingClient(BasicStreamSse);

        var request = new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "test")],
            Model: "claude-3-5-sonnet-20241022");

        var feature = client.Features.Get<IStreamingChatFeature>()!;
        var chunks = new List<ChatCompletionChunk>();
        await foreach (var chunk in feature.GetChatCompletionStreamAsync(request))
        {
            chunks.Add(chunk);
        }

        // After message_start, text chunks should have the model
        var textChunks = chunks.Where(c => !string.IsNullOrEmpty(c.Content) && c.Model is not null).ToList();
        Assert.Multiple(() =>
        {
            Assert.That(textChunks, Is.Not.Empty);
            Assert.That(textChunks[0].Model, Is.EqualTo("claude-3-5-sonnet-20241022"));
        });
    }

    [Test]
    public async Task Empty_Stream_Returns_No_Chunks()
    {
        var client = CreateStreamingClient(string.Empty);

        var request = new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "test")],
            Model: "claude-3-5-sonnet-20241022");

        var feature = client.Features.Get<IStreamingChatFeature>()!;
        var chunks = new List<ChatCompletionChunk>();
        await foreach (var chunk in feature.GetChatCompletionStreamAsync(request))
        {
            chunks.Add(chunk);
        }

        Assert.That(chunks, Is.Empty);
    }
}
