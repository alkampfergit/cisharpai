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

    private static AzureOpenAiClientOptions CreateOptions() => new()
    {
        Endpoint = "https://myresource.openai.azure.com/",
        DeploymentName = "gpt-4o",
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
}
