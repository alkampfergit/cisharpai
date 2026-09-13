using System.Net;
using System.Text.Json;
using Cisharpai.Features.Chat;
using Cisharpai.Models;
using Cisharpai.Anthropic;

namespace Cisharpai.Tests.Anthropic;

public sealed class AnthropicPromptCachingTests
{
    [Test]
    public async Task GetChatCompletionAsync_MapsCacheUsageFields()
    {
        var handler = new MockHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(CachedResponseJson, System.Text.Encoding.UTF8, "application/json")
            }));

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.anthropic.com/v1/") };
        var client = new AnthropicChatCompletionClient(httpClient, new AnthropicClientOptions());

        var response = await client.GetChatCompletionAsync(new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "Hello")],
            Model: "claude-sonnet-4-20250514"));

        Assert.Multiple(() =>
        {
            Assert.That(response.IsSuccess, Is.True);
            Assert.That(response.CachedInputTokens, Is.EqualTo(1024));
            Assert.That(response.CacheCreationInputTokens, Is.EqualTo(512));
            Assert.That(response.PromptTokens, Is.EqualTo(15));
        });
    }

    [Test]
    public async Task GetChatCompletionAsync_NoCacheFields_ReturnsNull()
    {
        var handler = new MockHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(NoCacheResponseJson, System.Text.Encoding.UTF8, "application/json")
            }));

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.anthropic.com/v1/") };
        var client = new AnthropicChatCompletionClient(httpClient, new AnthropicClientOptions());

        var response = await client.GetChatCompletionAsync(new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "Hello")],
            Model: "claude-sonnet-4-20250514"));

        Assert.Multiple(() =>
        {
            Assert.That(response.CachedInputTokens, Is.Null);
            Assert.That(response.CacheCreationInputTokens, Is.Null);
        });
    }

    [Test]
    public void PromptCachingFeature_IsRegistered()
    {
        using var httpClient = new HttpClient(new MockHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK))))
        {
            BaseAddress = new Uri("https://api.anthropic.com/v1/")
        };

        var client = new AnthropicChatCompletionClient(httpClient, new AnthropicClientOptions());
        var feature = client.Features.Get<IPromptCachingFeature>();

        Assert.That(feature, Is.Not.Null);
    }

    [Test]
    public async Task GetChatCompletionWithCachingAsync_AddsSystemCacheControl()
    {
        string? capturedBody = null;
        var handler = new MockHttpMessageHandler(async (request, _) =>
        {
            capturedBody = await request.Content!.ReadAsStringAsync(CancellationToken.None);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(CachedResponseJson, System.Text.Encoding.UTF8, "application/json")
            };
        });

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.anthropic.com/v1/") };
        var client = new AnthropicChatCompletionClient(httpClient, new AnthropicClientOptions());

        var cachingFeature = client.Features.Get<IPromptCachingFeature>()!;
        var request = new ChatCompletionRequest(
            Messages:
            [
                new LlmMessage(LlmRole.System, "You are a helpful assistant"),
                new LlmMessage(LlmRole.User, "Hello")
            ],
            Model: "claude-sonnet-4-20250514");

        await cachingFeature.GetChatCompletionWithCachingAsync(
            request,
            new PromptCachingOptions { CacheSystemMessage = true });

        var doc = JsonDocument.Parse(capturedBody!);
        var system = doc.RootElement.GetProperty("system");

        Assert.That(system.ValueKind, Is.EqualTo(JsonValueKind.Array));
        var firstBlock = system[0];
        Assert.Multiple(() =>
        {
            Assert.That(firstBlock.GetProperty("type").GetString(), Is.EqualTo("text"));
            Assert.That(firstBlock.GetProperty("text").GetString(), Is.EqualTo("You are a helpful assistant"));
            Assert.That(firstBlock.GetProperty("cache_control").GetProperty("type").GetString(), Is.EqualTo("ephemeral"));
        });
    }

    [Test]
    public async Task GetChatCompletionWithCachingAsync_AddsMessageBreakpoint()
    {
        string? capturedBody = null;
        var handler = new MockHttpMessageHandler(async (request, _) =>
        {
            capturedBody = await request.Content!.ReadAsStringAsync(CancellationToken.None);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(CachedResponseJson, System.Text.Encoding.UTF8, "application/json")
            };
        });

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.anthropic.com/v1/") };
        var client = new AnthropicChatCompletionClient(httpClient, new AnthropicClientOptions());

        var cachingFeature = client.Features.Get<IPromptCachingFeature>()!;
        var request = new ChatCompletionRequest(
            Messages:
            [
                new LlmMessage(LlmRole.User, "First message"),
                new LlmMessage(LlmRole.Assistant, "Response"),
                new LlmMessage(LlmRole.User, "Second message")
            ],
            Model: "claude-sonnet-4-20250514");

        await cachingFeature.GetChatCompletionWithCachingAsync(
            request,
            new PromptCachingOptions { MessageBreakpoints = [0] });

        var doc = JsonDocument.Parse(capturedBody!);
        var messages = doc.RootElement.GetProperty("messages");

        var firstMsg = messages[0];
        Assert.That(firstMsg.GetProperty("content").ValueKind, Is.EqualTo(JsonValueKind.Array));
        var contentBlock = firstMsg.GetProperty("content")[0];
        Assert.That(contentBlock.GetProperty("cache_control").GetProperty("type").GetString(), Is.EqualTo("ephemeral"));

        var secondMsg = messages[1];
        Assert.That(secondMsg.GetProperty("content").ValueKind, Is.EqualTo(JsonValueKind.String));
    }

    [Test]
    public async Task GetChatCompletionWithCachingAsync_IgnoresOutOfRangeBreakpoints()
    {
        string? capturedBody = null;
        var handler = new MockHttpMessageHandler(async (request, _) =>
        {
            capturedBody = await request.Content!.ReadAsStringAsync(CancellationToken.None);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(CachedResponseJson, System.Text.Encoding.UTF8, "application/json")
            };
        });

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.anthropic.com/v1/") };
        var client = new AnthropicChatCompletionClient(httpClient, new AnthropicClientOptions());

        var cachingFeature = client.Features.Get<IPromptCachingFeature>()!;
        var request = new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "Hello")],
            Model: "claude-sonnet-4-20250514");

        var response = await cachingFeature.GetChatCompletionWithCachingAsync(
            request,
            new PromptCachingOptions { MessageBreakpoints = [5, -1] });

        Assert.That(response.IsSuccess, Is.True);
    }

    [Test]
    public async Task GetChatCompletionWithCachingAsync_ReturnsCacheUsage()
    {
        var handler = new MockHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(CachedResponseJson, System.Text.Encoding.UTF8, "application/json")
            }));

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.anthropic.com/v1/") };
        var client = new AnthropicChatCompletionClient(httpClient, new AnthropicClientOptions());

        var cachingFeature = client.Features.Get<IPromptCachingFeature>()!;
        var response = await cachingFeature.GetChatCompletionWithCachingAsync(
            new ChatCompletionRequest(
                Messages: [new LlmMessage(LlmRole.User, "Hello")],
                Model: "claude-sonnet-4-20250514"),
            new PromptCachingOptions { CacheSystemMessage = true });

        Assert.Multiple(() =>
        {
            Assert.That(response.CachedInputTokens, Is.EqualTo(1024));
            Assert.That(response.CacheCreationInputTokens, Is.EqualTo(512));
        });
    }

    [Test]
    public async Task GetChatCompletionWithCachingAsync_ToolBreakpoints_DoesNotCrashWithoutTools()
    {
        var handler = new MockHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(CachedResponseJson, System.Text.Encoding.UTF8, "application/json")
            }));

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.anthropic.com/v1/") };
        var client = new AnthropicChatCompletionClient(httpClient, new AnthropicClientOptions());

        var cachingFeature = client.Features.Get<IPromptCachingFeature>()!;
        var response = await cachingFeature.GetChatCompletionWithCachingAsync(
            new ChatCompletionRequest(
                Messages: [new LlmMessage(LlmRole.User, "Hello")],
                Model: "claude-sonnet-4-20250514"),
            new PromptCachingOptions { ToolBreakpoints = [0] });

        Assert.That(response.IsSuccess, Is.True);
    }

    [Test]
    public async Task Streaming_MapsCacheUsageOnFinalChunk()
    {
        var sseContent = """
            event: message_start
            data: {"type":"message_start","message":{"id":"msg-abc","type":"message","role":"assistant","model":"claude-sonnet-4-20250514","content":[],"stop_reason":null,"usage":{"input_tokens":10,"output_tokens":1,"cache_creation_input_tokens":200,"cache_read_input_tokens":800}}}

            event: content_block_start
            data: {"type":"content_block_start","index":0,"content_block":{"type":"text","text":""}}

            event: content_block_delta
            data: {"type":"content_block_delta","index":0,"delta":{"type":"text_delta","text":"Hello"}}

            event: content_block_stop
            data: {"type":"content_block_stop","index":0}

            event: message_delta
            data: {"type":"message_delta","delta":{"stop_reason":"end_turn"},"usage":{"output_tokens":5}}

            event: message_stop
            data: {"type":"message_stop"}

            """;

        var bytes = System.Text.Encoding.UTF8.GetBytes(sseContent);
        var handler = new MockHttpMessageHandler((_, _) =>
        {
            var stream = new System.IO.MemoryStream(bytes);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StreamContent(stream)
            });
        });

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.anthropic.com/v1/") };
        var client = new AnthropicChatCompletionClient(httpClient, new AnthropicClientOptions());

        var feature = client.Features.Get<IStreamingChatFeature>()!;
        var chunks = new List<ChatCompletionChunk>();
        await foreach (var chunk in feature.GetChatCompletionStreamAsync(
            new ChatCompletionRequest(
                Messages: [new LlmMessage(LlmRole.User, "Hello")],
                Model: "claude-sonnet-4-20250514")))
        {
            chunks.Add(chunk);
        }

        var finalChunk = chunks.Last(c => c.FinishReason is not null);
        Assert.Multiple(() =>
        {
            Assert.That(finalChunk.CachedInputTokens, Is.EqualTo(800));
            Assert.That(finalChunk.CacheCreationInputTokens, Is.EqualTo(200));
        });
    }

    private const string CachedResponseJson = """
        {
            "model": "claude-sonnet-4-20250514",
            "content": [
                {
                    "type": "text",
                    "text": "Hello there!"
                }
            ],
            "usage": {
                "input_tokens": 15,
                "output_tokens": 25,
                "cache_creation_input_tokens": 512,
                "cache_read_input_tokens": 1024
            },
            "stop_reason": "end_turn"
        }
        """;

    private const string NoCacheResponseJson = """
        {
            "model": "claude-sonnet-4-20250514",
            "content": [
                {
                    "type": "text",
                    "text": "Hello there!"
                }
            ],
            "usage": {
                "input_tokens": 15,
                "output_tokens": 25
            },
            "stop_reason": "end_turn"
        }
        """;
}
