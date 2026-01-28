using System.Net;
using System.Text.Json;
using cisharpai.Models;
using cisharpai.OpenAi;

namespace cisharpai.test.OpenAi;

public sealed class OpenAiChatCompletionClientTests
{
    [Test]
    public async Task GetChatCompletionAsync_MapsRequestToOpenAiFormat()
    {
        string? capturedBody = null;
        var handler = new MockHttpMessageHandler(async (request, _) =>
        {
            capturedBody = await request.Content!.ReadAsStringAsync();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(OpenAiResponseJson, System.Text.Encoding.UTF8, "application/json")
            };
        });

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.openai.com/v1/") };
        var client = new OpenAiChatCompletionClient(httpClient);

        var request = new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "Hello")],
            Model: "gpt-4",
            Temperature: 0.7,
            MaxTokens: 100);

        await client.GetChatCompletionAsync(request);

        Assert.That(capturedBody, Is.Not.Null);
        var doc = JsonDocument.Parse(capturedBody!);
        Assert.That(doc.RootElement.GetProperty("model").GetString(), Is.EqualTo("gpt-4"));
        Assert.That(doc.RootElement.GetProperty("temperature").GetDouble(), Is.EqualTo(0.7));
        Assert.That(doc.RootElement.GetProperty("max_tokens").GetInt32(), Is.EqualTo(100));

        var messages = doc.RootElement.GetProperty("messages");
        Assert.That(messages.GetArrayLength(), Is.EqualTo(1));
        Assert.That(messages[0].GetProperty("role").GetString(), Is.EqualTo("user"));
        Assert.That(messages[0].GetProperty("content").GetString(), Is.EqualTo("Hello"));
    }

    [Test]
    public async Task GetChatCompletionAsync_MapsResponseToSharedModel()
    {
        var handler = new MockHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(OpenAiResponseJson, System.Text.Encoding.UTF8, "application/json")
            }));

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.openai.com/v1/") };
        var client = new OpenAiChatCompletionClient(httpClient);

        var request = new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "Hello")],
            Model: "gpt-4");

        var response = await client.GetChatCompletionAsync(request);

        Assert.That(response.Content, Is.EqualTo("Hello there!"));
        Assert.That(response.Model, Is.EqualTo("gpt-4-0613"));
        Assert.That(response.PromptTokens, Is.EqualTo(10));
        Assert.That(response.CompletionTokens, Is.EqualTo(20));
    }

    [Test]
    public async Task GetChatCompletionAsync_PostsToChatCompletionsEndpoint()
    {
        var handler = new MockHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(OpenAiResponseJson, System.Text.Encoding.UTF8, "application/json")
            }));

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.openai.com/v1/") };
        var client = new OpenAiChatCompletionClient(httpClient);

        await client.GetChatCompletionAsync(new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "Hi")],
            Model: "gpt-4"));

        Assert.That(handler.LastRequest!.RequestUri!.PathAndQuery, Does.Contain("chat/completions"));
    }

    private const string OpenAiResponseJson = """
        {
            "model": "gpt-4-0613",
            "choices": [
                {
                    "message": {
                        "role": "assistant",
                        "content": "Hello there!"
                    }
                }
            ],
            "usage": {
                "prompt_tokens": 10,
                "completion_tokens": 20
            }
        }
        """;
}
