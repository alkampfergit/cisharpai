using System.Net;
using Cisharpai.Features.Chat;
using Cisharpai.Models;
using Cisharpai.Azure.AzureOpenAi;

namespace Cisharpai.Tests.Azure.AzureOpenAi;

public sealed class AzureOpenAiPromptCachingTests
{
    [Test]
    public async Task GetChatCompletionAsync_ChatCompletionsApi_MapsCachedTokens()
    {
        var handler = new MockHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(ChatCompletionsCachedJson, System.Text.Encoding.UTF8, "application/json")
            }));

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://myresource.openai.azure.com/") };
        var client = new AzureOpenAiChatCompletionClient(httpClient, CreateOptions());

        var response = await client.GetChatCompletionAsync(new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "Hello")],
            Model: "gpt-4o"));

        Assert.Multiple(() =>
        {
            Assert.That(response.IsSuccess, Is.True);
            Assert.That(response.CachedInputTokens, Is.EqualTo(512));
            Assert.That(response.CacheCreationInputTokens, Is.Null);
        });
    }

    [Test]
    public async Task GetChatCompletionAsync_ChatCompletionsApi_NoCachedTokens_ReturnsNull()
    {
        var handler = new MockHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(ChatCompletionsNoCacheJson, System.Text.Encoding.UTF8, "application/json")
            }));

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://myresource.openai.azure.com/") };
        var client = new AzureOpenAiChatCompletionClient(httpClient, CreateOptions());

        var response = await client.GetChatCompletionAsync(new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "Hello")],
            Model: "gpt-4o"));

        Assert.Multiple(() =>
        {
            Assert.That(response.CachedInputTokens, Is.Null);
            Assert.That(response.CacheCreationInputTokens, Is.Null);
        });
    }

    [Test]
    public void PromptCachingFeature_IsNotRegistered()
    {
        using var httpClient = new HttpClient(new MockHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK))))
        {
            BaseAddress = new Uri("https://myresource.openai.azure.com/")
        };

        var client = new AzureOpenAiChatCompletionClient(httpClient, CreateOptions());
        var feature = client.Features.Get<IPromptCachingFeature>();

        Assert.That(feature, Is.Null);
    }

    [Test]
    public async Task GetChatCompletionAsync_ChatCompletionsApi_ZeroCachedTokens_ReturnsNull()
    {
        var handler = new MockHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(ChatCompletionsZeroCacheJson, System.Text.Encoding.UTF8, "application/json")
            }));

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://myresource.openai.azure.com/") };
        var client = new AzureOpenAiChatCompletionClient(httpClient, CreateOptions());

        var response = await client.GetChatCompletionAsync(new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "Hello")],
            Model: "gpt-4o"));

        Assert.That(response.CachedInputTokens, Is.Null);
    }

    [Test]
    public async Task GetChatCompletionAsync_ResponsesApi_MapsCachedTokens()
    {
        var handler = new MockHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(ResponsesApiCachedJson, System.Text.Encoding.UTF8, "application/json")
            }));

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://myresource.openai.azure.com/") };
        var client = new AzureOpenAiChatCompletionClient(httpClient, CreateGpt5Options());

        var response = await client.GetChatCompletionAsync(new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "Hello")],
            Model: "gpt-5"));

        Assert.Multiple(() =>
        {
            Assert.That(response.IsSuccess, Is.True);
            Assert.That(response.CachedInputTokens, Is.EqualTo(256));
            Assert.That(response.CacheCreationInputTokens, Is.Null);
        });
    }

    [Test]
    public async Task Streaming_ChatCompletions_MapsCachedTokens()
    {
        var sseContent = """
            data: {"id":"chatcmpl-abc","model":"gpt-4o","choices":[{"index":0,"delta":{"role":"assistant","content":"Hi"},"finish_reason":null}]}

            data: {"id":"chatcmpl-abc","model":"gpt-4o","choices":[{"index":0,"delta":{},"finish_reason":"stop"}],"usage":{"prompt_tokens":20,"completion_tokens":3,"prompt_tokens_details":{"cached_tokens":512}}}

            data: [DONE]
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

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://myresource.openai.azure.com/") };
        var client = new AzureOpenAiChatCompletionClient(httpClient, CreateOptions());

        var feature = client.Features.Get<IStreamingChatFeature>()!;
        var chunks = new List<ChatCompletionChunk>();
        await foreach (var chunk in feature.GetChatCompletionStreamAsync(
            new ChatCompletionRequest(
                Messages: [new LlmMessage(LlmRole.User, "Hello")],
                Model: "gpt-4o")))
        {
            chunks.Add(chunk);
        }

        var cachedChunk = chunks.FirstOrDefault(c => c.CachedInputTokens.HasValue);
        Assert.That(cachedChunk, Is.Not.Null);
        Assert.That(cachedChunk!.CachedInputTokens, Is.EqualTo(512));
    }

    [Test]
    public async Task Streaming_ChatCompletions_UsageOnlyChunk_MapsCachedTokens()
    {
        var sseContent = """
            data: {"id":"chatcmpl-abc","model":"gpt-4o","choices":[{"index":0,"delta":{"content":"Hi"},"finish_reason":null}]}

            data: {"id":"chatcmpl-abc","model":"gpt-4o","choices":[{"index":0,"delta":{},"finish_reason":"stop"}]}

            data: {"id":"chatcmpl-abc","model":"gpt-4o","choices":[],"usage":{"prompt_tokens":20,"completion_tokens":3,"prompt_tokens_details":{"cached_tokens":128}}}

            data: [DONE]
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

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://myresource.openai.azure.com/") };
        var client = new AzureOpenAiChatCompletionClient(httpClient, CreateOptions());

        var feature = client.Features.Get<IStreamingChatFeature>()!;
        var chunks = new List<ChatCompletionChunk>();
        await foreach (var chunk in feature.GetChatCompletionStreamAsync(
            new ChatCompletionRequest(
                Messages: [new LlmMessage(LlmRole.User, "Hello")],
                Model: "gpt-4o")))
        {
            chunks.Add(chunk);
        }

        var cachedChunk = chunks.FirstOrDefault(c => c.CachedInputTokens.HasValue);
        Assert.That(cachedChunk, Is.Not.Null);
        Assert.That(cachedChunk!.CachedInputTokens, Is.EqualTo(128));
    }

    [Test]
    public async Task Streaming_ResponsesApi_MapsCachedTokens()
    {
        var sseContent = """
            data: {"type":"response.output_text.delta","delta":"Hi"}

            data: {"type":"response.completed","response":{"model":"gpt-5","status":"completed","output":[],"usage":{"input_tokens":20,"output_tokens":3,"input_tokens_details":{"cached_tokens":64}}}}

            data: [DONE]
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

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://myresource.openai.azure.com/") };
        var client = new AzureOpenAiChatCompletionClient(httpClient, CreateGpt5Options());

        var feature = client.Features.Get<IStreamingChatFeature>()!;
        var chunks = new List<ChatCompletionChunk>();
        await foreach (var chunk in feature.GetChatCompletionStreamAsync(
            new ChatCompletionRequest(
                Messages: [new LlmMessage(LlmRole.User, "Hello")],
                Model: "gpt-5")))
        {
            chunks.Add(chunk);
        }

        var cachedChunk = chunks.FirstOrDefault(c => c.CachedInputTokens.HasValue);
        Assert.That(cachedChunk, Is.Not.Null);
        Assert.That(cachedChunk!.CachedInputTokens, Is.EqualTo(64));
    }

    [Test]
    public async Task GetChatCompletionWithToolsAsync_MapsCachedTokens()
    {
        var handler = new MockHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(ToolCallingCachedJson, System.Text.Encoding.UTF8, "application/json")
            }));

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://myresource.openai.azure.com/") };
        var client = new AzureOpenAiChatCompletionClient(httpClient, CreateOptions());

        var toolParams = System.Text.Json.JsonDocument.Parse("""{"type":"object","properties":{"city":{"type":"string"}},"required":["city"]}""").RootElement.Clone();
        var feature = client.Features.Get<IToolCallingFeature>()!;
        var response = await feature.GetChatCompletionWithToolsAsync(
            new ChatCompletionRequest(
                Messages: [new LlmMessage(LlmRole.User, "Weather?")],
                Model: "gpt-4o"),
            new ToolCallingOptions([new ToolDefinition("get_weather", "Get weather", toolParams)]));

        Assert.That(response.ChatCompletion.CachedInputTokens, Is.EqualTo(256));
    }

    private static AzureOpenAiClientOptions CreateOptions() => new()
    {
        Endpoint = "https://myresource.openai.azure.com/",
        DeploymentName = "gpt-4o",
        ApiKey = "test-key",
        ApiVersion = "2024-12-01-preview"
    };

    private static AzureOpenAiClientOptions CreateGpt5Options() => new()
    {
        Endpoint = "https://myresource.openai.azure.com/",
        DeploymentName = "gpt-5-deployment",
        ModelName = "gpt-5",
        ApiKey = "test-key",
        ApiVersion = "2024-12-01-preview"
    };

    private const string ChatCompletionsCachedJson = """
        {
            "model": "gpt-4o",
            "choices": [
                {
                    "message": { "role": "assistant", "content": "Hi!" },
                    "finish_reason": "stop"
                }
            ],
            "usage": {
                "prompt_tokens": 20,
                "completion_tokens": 5,
                "prompt_tokens_details": {
                    "cached_tokens": 512
                }
            }
        }
        """;

    private const string ChatCompletionsNoCacheJson = """
        {
            "model": "gpt-4o",
            "choices": [
                {
                    "message": { "role": "assistant", "content": "Hi!" },
                    "finish_reason": "stop"
                }
            ],
            "usage": {
                "prompt_tokens": 20,
                "completion_tokens": 5
            }
        }
        """;

    private const string ChatCompletionsZeroCacheJson = """
        {
            "model": "gpt-4o",
            "choices": [
                {
                    "message": { "role": "assistant", "content": "Hi!" },
                    "finish_reason": "stop"
                }
            ],
            "usage": {
                "prompt_tokens": 20,
                "completion_tokens": 5,
                "prompt_tokens_details": {
                    "cached_tokens": 0
                }
            }
        }
        """;

    private const string ResponsesApiCachedJson = """
        {
            "id": "resp_001",
            "model": "gpt-5",
            "status": "completed",
            "output": [
                {
                    "type": "message",
                    "role": "assistant",
                    "content": [
                        { "type": "output_text", "text": "Hi!" }
                    ]
                }
            ],
            "usage": {
                "input_tokens": 20,
                "output_tokens": 5,
                "input_tokens_details": {
                    "cached_tokens": 256
                }
            }
        }
        """;

    private const string ToolCallingCachedJson = """
        {
            "model": "gpt-4o",
            "choices": [
                {
                    "message": {
                        "role": "assistant",
                        "content": null,
                        "tool_calls": [
                            {
                                "id": "call_abc",
                                "type": "function",
                                "function": {
                                    "name": "get_weather",
                                    "arguments": "{\"city\":\"Paris\"}"
                                }
                            }
                        ]
                    },
                    "finish_reason": "tool_calls"
                }
            ],
            "usage": {
                "prompt_tokens": 30,
                "completion_tokens": 10,
                "prompt_tokens_details": {
                    "cached_tokens": 256
                }
            }
        }
        """;
}
