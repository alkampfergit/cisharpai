using System.Net;
using System.Text.Json;
using Cisharpai.Models;
using Cisharpai.Anthropic;

namespace Cisharpai.Tests.Anthropic;

public sealed class AnthropicChatCompletionClientTests
{
    [Test]
    public async Task GetChatCompletionAsync_ExtractsSystemMessage_IntoSeparateField()
    {
        string? capturedBody = null;
        var handler = new MockHttpMessageHandler(async (request, _) =>
        {
            capturedBody = await request.Content!.ReadAsStringAsync();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(AnthropicResponseJson, System.Text.Encoding.UTF8, "application/json")
            };
        });

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.anthropic.com/v1/") };
        var client = new AnthropicChatCompletionClient(httpClient, new AnthropicClientOptions());

        var request = new ChatCompletionRequest(
            Messages:
            [
                new LlmMessage(LlmRole.System, "You are helpful"),
                new LlmMessage(LlmRole.User, "Hello")
            ],
            Model: "claude-sonnet-4-20250514");

        await client.GetChatCompletionAsync(request);

        var doc = JsonDocument.Parse(capturedBody!);
        Assert.That(doc.RootElement.GetProperty("system").GetString(), Is.EqualTo("You are helpful"));

        var messages = doc.RootElement.GetProperty("messages");
        Assert.That(messages.GetArrayLength(), Is.EqualTo(1));
        Assert.That(messages[0].GetProperty("role").GetString(), Is.EqualTo("user"));
    }

    [Test]
    public async Task GetChatCompletionAsync_MapsResponseToSharedModel()
    {
        var handler = new MockHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(AnthropicResponseJson, System.Text.Encoding.UTF8, "application/json")
            }));

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.anthropic.com/v1/") };
        var client = new AnthropicChatCompletionClient(httpClient, new AnthropicClientOptions());

        var request = new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "Hello")],
            Model: "claude-sonnet-4-20250514");

        var response = await client.GetChatCompletionAsync(request);

        Assert.That(response.Content, Is.EqualTo("Hello there!"));
        Assert.That(response.Model, Is.EqualTo("claude-sonnet-4-20250514"));
        Assert.That(response.PromptTokens, Is.EqualTo(15));
        Assert.That(response.CompletionTokens, Is.EqualTo(25));
        Assert.That(response.IsSuccess, Is.True);
        Assert.That(response.ErrorMessage, Is.Null);
    }

    [Test]
    public async Task GetChatCompletionAsync_IncludeRawResponse_ReturnsRawJson()
    {
        var handler = new MockHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(AnthropicResponseJson, System.Text.Encoding.UTF8, "application/json")
            }));

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.anthropic.com/v1/") };
        var client = new AnthropicChatCompletionClient(httpClient, new AnthropicClientOptions());

        var response = await client.GetChatCompletionAsync(new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "Hello")],
            Model: "claude-sonnet-4-20250514",
            IncludeRawResponse: true));

        Assert.That(response.RawResponseJson, Is.Not.Null);
        Assert.That(response.RawResponseJson, Does.Contain("claude-sonnet-4-20250514"));
    }

    [Test]
    public async Task GetChatCompletionAsync_WithoutIncludeRawResponse_RawJsonIsNull()
    {
        var handler = new MockHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(AnthropicResponseJson, System.Text.Encoding.UTF8, "application/json")
            }));

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.anthropic.com/v1/") };
        var client = new AnthropicChatCompletionClient(httpClient, new AnthropicClientOptions());

        var response = await client.GetChatCompletionAsync(new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "Hello")],
            Model: "claude-sonnet-4-20250514"));

        Assert.That(response.RawResponseJson, Is.Null);
    }

    [Test]
    public async Task GetChatCompletionAsync_HttpError_ReturnsErrorResponse()
    {
        var handler = new MockHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)
            {
                Content = new StringContent("""{"error":{"message":"Internal error"}}""",
                    System.Text.Encoding.UTF8, "application/json")
            }));

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.anthropic.com/v1/") };
        var client = new AnthropicChatCompletionClient(httpClient, new AnthropicClientOptions());

        var response = await client.GetChatCompletionAsync(new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "Hello")],
            Model: "claude-sonnet-4-20250514"));

        Assert.That(response.IsSuccess, Is.False);
        Assert.That(response.ErrorMessage, Does.Contain("500"));
        Assert.That(response.Content, Is.EqualTo(string.Empty));
        Assert.That(response.Model, Is.EqualTo(string.Empty));
    }

    [Test]
    public async Task GetChatCompletionAsync_WithMaxTokensNull_UsesDefaultMaxTokens()
    {
        string? capturedBody = null;
        var handler = new MockHttpMessageHandler(async (request, _) =>
        {
            capturedBody = await request.Content!.ReadAsStringAsync();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(AnthropicResponseJson, System.Text.Encoding.UTF8, "application/json")
            };
        });

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.anthropic.com/v1/") };
        var client = new AnthropicChatCompletionClient(httpClient, new AnthropicClientOptions());

        var request = new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "Hello")],
            Model: "claude-sonnet-4-20250514",
            MaxTokens: null);

        await client.GetChatCompletionAsync(request);

        var doc = JsonDocument.Parse(capturedBody!);
        Assert.That(doc.RootElement.TryGetProperty("max_tokens", out var maxTokens), Is.True,
            "max_tokens must always be sent because Anthropic API requires it");
        Assert.That(maxTokens.GetInt32(), Is.EqualTo(AnthropicChatCompletionClient.DefaultMaxTokens));
    }

    [Test]
    public async Task GetChatCompletionAsync_WithMaxTokensSet_PassesThroughToRequest()
    {
        string? capturedBody = null;
        var handler = new MockHttpMessageHandler(async (request, _) =>
        {
            capturedBody = await request.Content!.ReadAsStringAsync();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(AnthropicResponseJson, System.Text.Encoding.UTF8, "application/json")
            };
        });

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.anthropic.com/v1/") };
        var client = new AnthropicChatCompletionClient(httpClient, new AnthropicClientOptions());

        var request = new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "Hello")],
            Model: "claude-sonnet-4-20250514",
            MaxTokens: 2048);

        await client.GetChatCompletionAsync(request);

        var doc = JsonDocument.Parse(capturedBody!);
        Assert.That(doc.RootElement.TryGetProperty("max_tokens", out var maxTokens), Is.True);
        Assert.That(maxTokens.GetInt32(), Is.EqualTo(2048));
    }

    [Test]
    public async Task GetChatCompletionAsync_WithMaxTokensNotSet_DoesNotDefaultTo1024()
    {
        string? capturedBody = null;
        var handler = new MockHttpMessageHandler(async (request, _) =>
        {
            capturedBody = await request.Content!.ReadAsStringAsync();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(AnthropicResponseJson, System.Text.Encoding.UTF8, "application/json")
            };
        });

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.anthropic.com/v1/") };
        var client = new AnthropicChatCompletionClient(httpClient, new AnthropicClientOptions());

        var request = new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "Hello")],
            Model: "claude-sonnet-4-20250514");

        await client.GetChatCompletionAsync(request);

        var doc = JsonDocument.Parse(capturedBody!);
        Assert.That(doc.RootElement.TryGetProperty("max_tokens", out var maxTokens), Is.True);
        Assert.That(maxTokens.GetInt32(), Is.Not.EqualTo(1024),
            "max_tokens must not silently default to 1024");
        Assert.That(maxTokens.GetInt32(), Is.EqualTo(AnthropicChatCompletionClient.DefaultMaxTokens));
    }

    [Test]
    public async Task GetChatCompletionAsync_StopReasonMaxTokens_PopulatesIncompleteReason()
    {
        var handler = new MockHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(AnthropicMaxTokensResponseJson, System.Text.Encoding.UTF8, "application/json")
            }));

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.anthropic.com/v1/") };
        var client = new AnthropicChatCompletionClient(httpClient, new AnthropicClientOptions());

        var response = await client.GetChatCompletionAsync(new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "Write a long essay")],
            Model: "claude-sonnet-4-20250514",
            MaxTokens: 10));

        Assert.That(response.IsSuccess, Is.True, "Truncated responses are still successful (content is usable)");
        Assert.That(response.Content, Is.EqualTo("This is truncated"));
        Assert.That(response.IncompleteReason, Is.EqualTo("max_tokens"));
        Assert.That(response.Status, Is.EqualTo("max_tokens"));
    }

    [Test]
    public async Task GetChatCompletionAsync_StopReasonEndTurn_NoIncompleteReason()
    {
        var handler = new MockHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(AnthropicResponseJson, System.Text.Encoding.UTF8, "application/json")
            }));

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.anthropic.com/v1/") };
        var client = new AnthropicChatCompletionClient(httpClient, new AnthropicClientOptions());

        var response = await client.GetChatCompletionAsync(new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "Hello")],
            Model: "claude-sonnet-4-20250514"));

        Assert.That(response.IsSuccess, Is.True);
        Assert.That(response.IncompleteReason, Is.Null);
        Assert.That(response.Status, Is.EqualTo("end_turn"));
    }

    private const string AnthropicResponseJson = """
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

    private const string AnthropicMaxTokensResponseJson = """
        {
            "model": "claude-sonnet-4-20250514",
            "content": [
                {
                    "type": "text",
                    "text": "This is truncated"
                }
            ],
            "usage": {
                "input_tokens": 15,
                "output_tokens": 10
            },
            "stop_reason": "max_tokens"
        }
        """;
}
