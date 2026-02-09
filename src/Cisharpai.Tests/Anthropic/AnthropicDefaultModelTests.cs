using System.Net;
using System.Text.Json;
using Cisharpai.Models;
using Cisharpai.Anthropic;

namespace Cisharpai.Tests.Anthropic;

public sealed class AnthropicDefaultModelTests
{
    private const string ResponseJson = """
        {
            "id": "msg-1",
            "type": "message",
            "role": "assistant",
            "model": "claude-sonnet-4-5-20250929",
            "content": [
                { "type": "text", "text": "Hello" }
            ],
            "stop_reason": "end_turn",
            "usage": { "input_tokens": 10, "output_tokens": 5 }
        }
        """;

    [Test]
    public async Task UsesDefaultModel_WhenRequestModelIsNull()
    {
        var handler = new FakeHttpHandler(ResponseJson);
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.anthropic.com/v1/") };
        var options = new AnthropicClientOptions { DefaultModel = AnthropicModels.Chat.ClaudeSonnet4_5 };
        var client = new AnthropicChatCompletionClient(httpClient, options);

        var request = new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "Hi")],
            IncludeRawResponse: true);

        var response = await client.GetChatCompletionAsync(request);

        Assert.That(response.IsSuccess, Is.True);
        var rawRequest = JsonDocument.Parse(response.RawRequestJson!);
        Assert.That(rawRequest.RootElement.GetProperty("model").GetString(),
            Is.EqualTo("claude-sonnet-4-5-20250929"));
    }

    [Test]
    public async Task UsesRequestModel_WhenBothAreSet()
    {
        var handler = new FakeHttpHandler(ResponseJson);
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.anthropic.com/v1/") };
        var options = new AnthropicClientOptions { DefaultModel = AnthropicModels.Chat.ClaudeSonnet4_5 };
        var client = new AnthropicChatCompletionClient(httpClient, options);

        var request = new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "Hi")],
            Model: "claude-haiku-4-5-20251001",
            IncludeRawResponse: true);

        var response = await client.GetChatCompletionAsync(request);

        Assert.That(response.IsSuccess, Is.True);
        var rawRequest = JsonDocument.Parse(response.RawRequestJson!);
        Assert.That(rawRequest.RootElement.GetProperty("model").GetString(),
            Is.EqualTo("claude-haiku-4-5-20251001"));
    }

    [Test]
    public void ThrowsInvalidOperationException_WhenNoModelSpecified()
    {
        using var httpClient = new HttpClient { BaseAddress = new Uri("https://api.anthropic.com/v1/") };
        var options = new AnthropicClientOptions();
        var client = new AnthropicChatCompletionClient(httpClient, options);

        var request = new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "Hi")]);

        Assert.ThrowsAsync<InvalidOperationException>(
            async () => await client.GetChatCompletionAsync(request));
    }

    [Test]
    public void ModelConstants_HaveExpectedValues()
    {
        Assert.Multiple(() =>
        {
            Assert.That(AnthropicModels.Chat.ClaudeOpus4_5, Is.EqualTo("claude-opus-4-5-20251101"));
            Assert.That(AnthropicModels.Chat.ClaudeSonnet4_5, Is.EqualTo("claude-sonnet-4-5-20250929"));
            Assert.That(AnthropicModels.Chat.ClaudeHaiku4_5, Is.EqualTo("claude-haiku-4-5-20251001"));
            Assert.That(AnthropicModels.Chat.ClaudeSonnet4, Is.EqualTo("claude-sonnet-4-20250514"));
            Assert.That(AnthropicModels.Chat.ClaudeHaiku4, Is.EqualTo("claude-haiku-4-20250414"));
            Assert.That(AnthropicModels.Chat.ClaudeOpus3, Is.EqualTo("claude-3-opus-20240229"));
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
