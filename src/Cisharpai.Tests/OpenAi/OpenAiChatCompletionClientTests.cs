using System.Net;
using System.Text.Json;
using Cisharpai.Models;
using Cisharpai.OpenAi;

namespace Cisharpai.Tests.OpenAi;

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
        var client = new OpenAiChatCompletionClient(httpClient, new OpenAiClientOptions());

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
        var client = new OpenAiChatCompletionClient(httpClient, new OpenAiClientOptions());

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
        var client = new OpenAiChatCompletionClient(httpClient, new OpenAiClientOptions());

        await client.GetChatCompletionAsync(new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "Hi")],
            Model: "gpt-4"));

        Assert.That(handler.LastRequest!.RequestUri!.PathAndQuery, Does.Contain("chat/completions"));
    }

    [Test]
    public async Task GetChatCompletionAsync_LegacyModel_UsesMaxTokens()
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
        var client = new OpenAiChatCompletionClient(httpClient, new OpenAiClientOptions());

        await client.GetChatCompletionAsync(new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "Hi")],
            Model: "gpt-4o",
            MaxTokens: 200));

        var doc = JsonDocument.Parse(capturedBody!);
        Assert.That(doc.RootElement.GetProperty("max_tokens").GetInt32(), Is.EqualTo(200));
        Assert.That(doc.RootElement.TryGetProperty("max_completion_tokens", out _), Is.False);
        Assert.That(handler.LastRequest!.RequestUri!.PathAndQuery, Does.Contain("chat/completions"));
    }

    [Test]
    public async Task GetChatCompletionAsync_ReasoningModel_UsesMaxCompletionTokens()
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
        var client = new OpenAiChatCompletionClient(httpClient, new OpenAiClientOptions());

        await client.GetChatCompletionAsync(new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "Hi")],
            Model: "o3-mini",
            Temperature: 0.5,
            MaxTokens: 500));

        var doc = JsonDocument.Parse(capturedBody!);
        Assert.That(doc.RootElement.GetProperty("max_completion_tokens").GetInt32(), Is.EqualTo(500));
        Assert.That(doc.RootElement.TryGetProperty("max_tokens", out _), Is.False);
        Assert.That(doc.RootElement.TryGetProperty("temperature", out _), Is.False);
    }

    [Test]
    public async Task GetChatCompletionAsync_ReasoningModel_PostsToChatCompletionsEndpoint()
    {
        var handler = new MockHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(OpenAiResponseJson, System.Text.Encoding.UTF8, "application/json")
            }));

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.openai.com/v1/") };
        var client = new OpenAiChatCompletionClient(httpClient, new OpenAiClientOptions());

        await client.GetChatCompletionAsync(new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "Hi")],
            Model: "o1"));

        Assert.That(handler.LastRequest!.RequestUri!.PathAndQuery, Does.Contain("chat/completions"));
    }

    [Test]
    public async Task GetChatCompletionAsync_ReasoningModel_MapsResponse()
    {
        var handler = new MockHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(OpenAiResponseJson, System.Text.Encoding.UTF8, "application/json")
            }));

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.openai.com/v1/") };
        var client = new OpenAiChatCompletionClient(httpClient, new OpenAiClientOptions());

        var response = await client.GetChatCompletionAsync(new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "Hi")],
            Model: "o3"));

        Assert.That(response.Content, Is.EqualTo("Hello there!"));
        Assert.That(response.PromptTokens, Is.EqualTo(10));
        Assert.That(response.CompletionTokens, Is.EqualTo(20));
    }

    [Test]
    public async Task GetChatCompletionAsync_Gpt5Model_PostsToResponsesEndpoint()
    {
        var handler = new MockHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(ResponsesApiResponseJson, System.Text.Encoding.UTF8, "application/json")
            }));

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.openai.com/v1/") };
        var client = new OpenAiChatCompletionClient(httpClient, new OpenAiClientOptions());

        await client.GetChatCompletionAsync(new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "Hi")],
            Model: "gpt-5"));

        Assert.That(handler.LastRequest!.RequestUri!.PathAndQuery, Does.Contain("responses"));
        Assert.That(handler.LastRequest.RequestUri.PathAndQuery, Does.Not.Contain("chat/completions"));
    }

    [Test]
    public async Task GetChatCompletionAsync_Gpt5Model_UsesResponsesApiRequestFormat()
    {
        string? capturedBody = null;
        var handler = new MockHttpMessageHandler(async (request, _) =>
        {
            capturedBody = await request.Content!.ReadAsStringAsync();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(ResponsesApiResponseJson, System.Text.Encoding.UTF8, "application/json")
            };
        });

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.openai.com/v1/") };
        var client = new OpenAiChatCompletionClient(httpClient, new OpenAiClientOptions());

        await client.GetChatCompletionAsync(new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "Hello")],
            Model: "gpt-5",
            MaxTokens: 1000));

        var doc = JsonDocument.Parse(capturedBody!);
        Assert.That(doc.RootElement.GetProperty("model").GetString(), Is.EqualTo("gpt-5"));
        Assert.That(doc.RootElement.GetProperty("max_output_tokens").GetInt32(), Is.EqualTo(1000));
        Assert.That(doc.RootElement.TryGetProperty("max_tokens", out _), Is.False);

        var input = doc.RootElement.GetProperty("input");
        Assert.That(input.GetArrayLength(), Is.EqualTo(1));
        Assert.That(input[0].GetProperty("role").GetString(), Is.EqualTo("user"));
        Assert.That(input[0].GetProperty("content").GetString(), Is.EqualTo("Hello"));

        Assert.That(doc.RootElement.TryGetProperty("messages", out _), Is.False);
    }

    [Test]
    public async Task GetChatCompletionAsync_Gpt5Model_IncludesReasoningAndTextOptions()
    {
        string? capturedBody = null;
        var handler = new MockHttpMessageHandler(async (request, _) =>
        {
            capturedBody = await request.Content!.ReadAsStringAsync();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(ResponsesApiResponseJson, System.Text.Encoding.UTF8, "application/json")
            };
        });

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.openai.com/v1/") };
        var options = new OpenAiClientOptions { ReasoningEffort = "medium", TextVerbosity = "high" };
        var client = new OpenAiChatCompletionClient(httpClient, options);

        await client.GetChatCompletionAsync(new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "Hi")],
            Model: "gpt-5"));

        var doc = JsonDocument.Parse(capturedBody!);
        Assert.That(doc.RootElement.GetProperty("reasoning").GetProperty("effort").GetString(), Is.EqualTo("medium"));
        Assert.That(doc.RootElement.GetProperty("text").GetProperty("verbosity").GetString(), Is.EqualTo("high"));
    }

    [Test]
    public async Task GetChatCompletionAsync_Gpt5Model_OmitsReasoningAndTextWhenNull()
    {
        string? capturedBody = null;
        var handler = new MockHttpMessageHandler(async (request, _) =>
        {
            capturedBody = await request.Content!.ReadAsStringAsync();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(ResponsesApiResponseJson, System.Text.Encoding.UTF8, "application/json")
            };
        });

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.openai.com/v1/") };
        var client = new OpenAiChatCompletionClient(httpClient, new OpenAiClientOptions());

        await client.GetChatCompletionAsync(new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "Hi")],
            Model: "gpt-5"));

        var doc = JsonDocument.Parse(capturedBody!);
        Assert.That(doc.RootElement.TryGetProperty("reasoning", out _), Is.False);
        Assert.That(doc.RootElement.TryGetProperty("text", out _), Is.False);
    }

    [Test]
    public async Task GetChatCompletionAsync_Gpt5Model_MapsResponsesApiResponse()
    {
        var handler = new MockHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(ResponsesApiResponseJson, System.Text.Encoding.UTF8, "application/json")
            }));

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.openai.com/v1/") };
        var client = new OpenAiChatCompletionClient(httpClient, new OpenAiClientOptions());

        var response = await client.GetChatCompletionAsync(new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "Hi")],
            Model: "gpt-5"));

        Assert.That(response.Content, Is.EqualTo("Hello there!"));
        Assert.That(response.Model, Is.EqualTo("gpt-5-20250801"));
        Assert.That(response.PromptTokens, Is.EqualTo(10));
        Assert.That(response.CompletionTokens, Is.EqualTo(20));
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

    private const string ResponsesApiResponseJson = """
        {
            "id": "resp_abc123",
            "model": "gpt-5-20250801",
            "output": [
                {
                    "type": "message",
                    "role": "assistant",
                    "content": [
                        {
                            "type": "output_text",
                            "text": "Hello there!"
                        }
                    ]
                }
            ],
            "usage": {
                "input_tokens": 10,
                "output_tokens": 20
            }
        }
        """;
}
