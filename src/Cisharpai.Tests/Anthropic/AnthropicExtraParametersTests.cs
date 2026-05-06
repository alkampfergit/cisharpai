using System.Net;
using System.Text.Json;
using Cisharpai.Models;
using Cisharpai.Anthropic;

namespace Cisharpai.Tests.Anthropic;

public sealed class AnthropicExtraParametersTests
{
    [Test]
    public async Task ExtraParameters_AreMergedIntoRequest()
    {
        string? capturedBody = null;
        var handler = new MockHttpMessageHandler(async (request, _) =>
        {
            capturedBody = await request.Content!.ReadAsStringAsync(CancellationToken.None);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(AnthropicResponseJson, System.Text.Encoding.UTF8, "application/json")
            };
        });

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.anthropic.com/v1/") };
        var client = new AnthropicChatCompletionClient(httpClient, new AnthropicClientOptions());

        var extra = JsonDocument.Parse("""{"top_k":40,"stream":true}""").RootElement;
        var request = new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "Hello")],
            Model: "claude-sonnet-4-20250514",
            Temperature: 0.5,
            ExtraParameters: extra);

        await client.GetChatCompletionAsync(request);

        var doc = JsonDocument.Parse(capturedBody!);
        Assert.Multiple(() =>
        {
            Assert.That(doc.RootElement.GetProperty("model").GetString(), Is.EqualTo("claude-sonnet-4-20250514"));
            Assert.That(doc.RootElement.GetProperty("temperature").GetDouble(), Is.EqualTo(0.5));
            Assert.That(doc.RootElement.GetProperty("top_k").GetInt32(), Is.EqualTo(40));
            Assert.That(doc.RootElement.GetProperty("stream").GetBoolean(), Is.True);
        });
    }

    [Test]
    public async Task ExtraParameters_CanOverrideMaxTokens()
    {
        string? capturedBody = null;
        var handler = new MockHttpMessageHandler(async (request, _) =>
        {
            capturedBody = await request.Content!.ReadAsStringAsync(CancellationToken.None);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(AnthropicResponseJson, System.Text.Encoding.UTF8, "application/json")
            };
        });

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.anthropic.com/v1/") };
        var client = new AnthropicChatCompletionClient(httpClient, new AnthropicClientOptions());

        var extra = JsonDocument.Parse("""{"max_tokens":8192}""").RootElement;
        var request = new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "Hello")],
            Model: "claude-sonnet-4-20250514",
            MaxTokens: 1024,
            ExtraParameters: extra);

        await client.GetChatCompletionAsync(request);

        var doc = JsonDocument.Parse(capturedBody!);
        Assert.That(doc.RootElement.GetProperty("max_tokens").GetInt32(), Is.EqualTo(8192));
    }

    [Test]
    public async Task IncludeRawResponse_ReturnsRawRequestJson()
    {
        var handler = new MockHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(AnthropicResponseJson, System.Text.Encoding.UTF8, "application/json")
            }));

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.anthropic.com/v1/") };
        var client = new AnthropicChatCompletionClient(httpClient, new AnthropicClientOptions());

        var extra = JsonDocument.Parse("""{"top_k":40}""").RootElement;
        var request = new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "Hello")],
            Model: "claude-sonnet-4-20250514",
            IncludeRawResponse: true,
            ExtraParameters: extra);

        var response = await client.GetChatCompletionAsync(request);

        Assert.That(response.RawRequestJson, Is.Not.Null);
        var doc = JsonDocument.Parse(response.RawRequestJson!);
        Assert.That(doc.RootElement.GetProperty("top_k").GetInt32(), Is.EqualTo(40));
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
            }
        }
        """;
}
