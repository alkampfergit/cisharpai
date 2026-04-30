using System.Net;
using System.Text.Json;
using Cisharpai.Models;
using Cisharpai.OpenAi;

namespace Cisharpai.Tests.OpenAi;

public sealed class OpenAiExtraParametersTests
{
    [Test]
    public async Task ExtraParameters_AreMergedIntoLegacyRequest()
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

        var extra = JsonDocument.Parse("""{"top_p":0.9,"stream":true}""").RootElement;
        var request = new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "Hello")],
            Model: "gpt-4",
            Temperature: 0.7,
            ExtraParameters: extra);

        await client.GetChatCompletionAsync(request);

        var doc = JsonDocument.Parse(capturedBody!);
        Assert.Multiple(() =>
        {
            Assert.That(doc.RootElement.GetProperty("model").GetString(), Is.EqualTo("gpt-4"));
            Assert.That(doc.RootElement.GetProperty("temperature").GetDouble(), Is.EqualTo(0.7));
            Assert.That(doc.RootElement.GetProperty("top_p").GetDouble(), Is.EqualTo(0.9));
            Assert.That(doc.RootElement.GetProperty("stream").GetBoolean(), Is.True);
        });
    }

    [Test]
    public async Task ExtraParameters_VerbosityPassThrough_OnGpt5Model()
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

        var extra = JsonDocument.Parse("""{"text":{"verbosity":"high"}}""").RootElement;
        var request = new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "Hello")],
            Model: "gpt-5",
            ExtraParameters: extra);

        await client.GetChatCompletionAsync(request);

        var doc = JsonDocument.Parse(capturedBody!);
        Assert.That(doc.RootElement.GetProperty("text").GetProperty("verbosity").GetString(), Is.EqualTo("high"));
    }

    [Test]
    public async Task ExtraParameters_MergesWithExistingReasoningOption()
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
        var options = new OpenAiClientOptions { ReasoningEffort = "medium" };
        var client = new OpenAiChatCompletionClient(httpClient, options);

        var extra = JsonDocument.Parse("""{"reasoning":{"summary":"auto"}}""").RootElement;
        var request = new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "Hello")],
            Model: "gpt-5",
            ExtraParameters: extra);

        await client.GetChatCompletionAsync(request);

        var doc = JsonDocument.Parse(capturedBody!);
        var reasoning = doc.RootElement.GetProperty("reasoning");
        Assert.Multiple(() =>
        {
            Assert.That(reasoning.GetProperty("effort").GetString(), Is.EqualTo("medium"));
            Assert.That(reasoning.GetProperty("summary").GetString(), Is.EqualTo("auto"));
        });
    }

    [Test]
    public async Task ExtraParameters_AreMergedIntoReasoningRequest()
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

        var extra = JsonDocument.Parse("""{"reasoning_effort":"high"}""").RootElement;
        var request = new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "Hello")],
            Model: "o3-mini",
            MaxTokens: 500,
            ExtraParameters: extra);

        await client.GetChatCompletionAsync(request);

        var doc = JsonDocument.Parse(capturedBody!);
        Assert.Multiple(() =>
        {
            Assert.That(doc.RootElement.GetProperty("reasoning_effort").GetString(), Is.EqualTo("high"));
            Assert.That(doc.RootElement.GetProperty("max_completion_tokens").GetInt32(), Is.EqualTo(500));
        });
    }

    [Test]
    public async Task ExtraParameters_Null_NoMergeOccurs()
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
            Temperature: 0.5);

        await client.GetChatCompletionAsync(request);

        var doc = JsonDocument.Parse(capturedBody!);
        Assert.Multiple(() =>
        {
            Assert.That(doc.RootElement.GetProperty("model").GetString(), Is.EqualTo("gpt-4"));
            Assert.That(doc.RootElement.GetProperty("temperature").GetDouble(), Is.EqualTo(0.5));
            Assert.That(doc.RootElement.TryGetProperty("top_p", out _), Is.False);
        });
    }

    [Test]
    public async Task IncludeRawResponse_ReturnsRawRequestJson_WithMergedPayload()
    {
        var handler = new MockHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(OpenAiResponseJson, System.Text.Encoding.UTF8, "application/json")
            }));

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.openai.com/v1/") };
        var client = new OpenAiChatCompletionClient(httpClient, new OpenAiClientOptions());

        var extra = JsonDocument.Parse("""{"top_p":0.9}""").RootElement;
        var request = new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "Hello")],
            Model: "gpt-4",
            IncludeRawResponse: true,
            ExtraParameters: extra);

        var response = await client.GetChatCompletionAsync(request);

        Assert.That(response.RawRequestJson, Is.Not.Null);
        var doc = JsonDocument.Parse(response.RawRequestJson!);
        Assert.Multiple(() =>
        {
            Assert.That(doc.RootElement.GetProperty("top_p").GetDouble(), Is.EqualTo(0.9));
            Assert.That(doc.RootElement.GetProperty("model").GetString(), Is.EqualTo("gpt-4"));
        });
    }

    [Test]
    public async Task IncludeRawResponse_WithoutExtraParameters_StillReturnsRawRequestJson()
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
            Model: "gpt-4",
            IncludeRawResponse: true);

        var response = await client.GetChatCompletionAsync(request);

        Assert.Multiple(() =>
        {
            Assert.That(response.RawRequestJson, Is.Not.Null);
            Assert.That(response.RawResponseJson, Is.Not.Null);
        });
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
            "status": "completed",
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
