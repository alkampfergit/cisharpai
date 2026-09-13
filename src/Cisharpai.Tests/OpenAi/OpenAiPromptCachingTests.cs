using System.Net;
using Cisharpai.Features.Chat;
using Cisharpai.Models;
using Cisharpai.OpenAi;

namespace Cisharpai.Tests.OpenAi;

public sealed class OpenAiPromptCachingTests
{
    [Test]
    public async Task GetChatCompletionAsync_LegacyApi_MapsCachedTokens()
    {
        var handler = new MockHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(LegacyCachedResponseJson, System.Text.Encoding.UTF8, "application/json")
            }));

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.openai.com/v1/") };
        var client = new OpenAiChatCompletionClient(httpClient, new OpenAiClientOptions());

        var response = await client.GetChatCompletionAsync(new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "Hello")],
            Model: "gpt-4o"));

        Assert.Multiple(() =>
        {
            Assert.That(response.IsSuccess, Is.True);
            Assert.That(response.CachedInputTokens, Is.EqualTo(256));
            Assert.That(response.CacheCreationInputTokens, Is.Null);
        });
    }

    [Test]
    public async Task GetChatCompletionAsync_LegacyApi_NoCachedTokens_ReturnsNull()
    {
        var handler = new MockHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(LegacyNoCacheResponseJson, System.Text.Encoding.UTF8, "application/json")
            }));

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.openai.com/v1/") };
        var client = new OpenAiChatCompletionClient(httpClient, new OpenAiClientOptions());

        var response = await client.GetChatCompletionAsync(new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "Hello")],
            Model: "gpt-4o"));

        Assert.That(response.CachedInputTokens, Is.Null);
    }

    [Test]
    public async Task GetChatCompletionAsync_LegacyApi_ZeroCachedTokens_ReturnsNull()
    {
        var handler = new MockHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(LegacyZeroCacheResponseJson, System.Text.Encoding.UTF8, "application/json")
            }));

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.openai.com/v1/") };
        var client = new OpenAiChatCompletionClient(httpClient, new OpenAiClientOptions());

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

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.openai.com/v1/") };
        var client = new OpenAiChatCompletionClient(httpClient, new OpenAiClientOptions());

        var response = await client.GetChatCompletionAsync(new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "Hello")],
            Model: "gpt-5"));

        Assert.Multiple(() =>
        {
            Assert.That(response.IsSuccess, Is.True);
            Assert.That(response.CachedInputTokens, Is.EqualTo(128));
            Assert.That(response.CacheCreationInputTokens, Is.Null);
        });
    }

    [Test]
    public void PromptCachingFeature_IsNotRegistered()
    {
        using var httpClient = new HttpClient(new MockHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK))))
        {
            BaseAddress = new Uri("https://api.openai.com/v1/")
        };

        var client = new OpenAiChatCompletionClient(httpClient, new OpenAiClientOptions());
        var feature = client.Features.Get<IPromptCachingFeature>();

        Assert.That(feature, Is.Null);
    }

    private const string LegacyCachedResponseJson = """
        {
            "model": "gpt-4o-2025-05-13",
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
                    "cached_tokens": 256
                }
            }
        }
        """;

    private const string LegacyNoCacheResponseJson = """
        {
            "model": "gpt-4o-2025-05-13",
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

    private const string LegacyZeroCacheResponseJson = """
        {
            "model": "gpt-4o-2025-05-13",
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
            "model": "gpt-5-20250601",
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
                    "cached_tokens": 128
                }
            }
        }
        """;
}
