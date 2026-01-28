using System.Net;
using System.Text.Json;
using cisharpai.Models;
using cisharpai.Anthropic;

namespace cisharpai.test.Anthropic;

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
        var client = new AnthropicChatCompletionClient(httpClient);

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
        var client = new AnthropicChatCompletionClient(httpClient);

        var request = new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "Hello")],
            Model: "claude-sonnet-4-20250514");

        var response = await client.GetChatCompletionAsync(request);

        Assert.That(response.Content, Is.EqualTo("Hello there!"));
        Assert.That(response.Model, Is.EqualTo("claude-sonnet-4-20250514"));
        Assert.That(response.PromptTokens, Is.EqualTo(15));
        Assert.That(response.CompletionTokens, Is.EqualTo(25));
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
