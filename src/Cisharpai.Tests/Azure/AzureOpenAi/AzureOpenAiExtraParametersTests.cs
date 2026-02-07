using System.Net;
using System.Text.Json;
using Cisharpai.Models;
using Cisharpai.Azure.AzureOpenAi;

namespace Cisharpai.Tests.Azure.AzureOpenAi;

public sealed class AzureOpenAiExtraParametersTests
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
                Content = new StringContent(AzureResponseJson, System.Text.Encoding.UTF8, "application/json")
            };
        });

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://myresource.openai.azure.com/") };
        var options = new AzureOpenAiClientOptions
        {
            Endpoint = "https://myresource.openai.azure.com/",
            DeploymentName = "gpt-4",
            ApiVersion = "2024-02-01",
            ApiKey = "test-key"
        };
        var client = new AzureOpenAiChatCompletionClient(httpClient, options);

        var extra = JsonDocument.Parse("""{"top_p":0.9,"presence_penalty":0.5}""").RootElement;
        var request = new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "Hello")],
            Model: "gpt-4",
            Temperature: 0.7,
            ExtraParameters: extra);

        await client.GetChatCompletionAsync(request);

        var doc = JsonDocument.Parse(capturedBody!);
        Assert.That(doc.RootElement.GetProperty("temperature").GetDouble(), Is.EqualTo(0.7));
        Assert.That(doc.RootElement.GetProperty("top_p").GetDouble(), Is.EqualTo(0.9));
        Assert.That(doc.RootElement.GetProperty("presence_penalty").GetDouble(), Is.EqualTo(0.5));
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
                Content = new StringContent(AzureResponseJson, System.Text.Encoding.UTF8, "application/json")
            };
        });

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://myresource.openai.azure.com/") };
        var options = new AzureOpenAiClientOptions
        {
            Endpoint = "https://myresource.openai.azure.com/",
            DeploymentName = "o3",
            ApiVersion = "2024-02-01",
            ApiKey = "test-key"
        };
        var client = new AzureOpenAiChatCompletionClient(httpClient, options);

        var extra = JsonDocument.Parse("""{"reasoning_effort":"high"}""").RootElement;
        var request = new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "Hello")],
            Model: "o3",
            MaxTokens: 1000,
            ExtraParameters: extra);

        await client.GetChatCompletionAsync(request);

        var doc = JsonDocument.Parse(capturedBody!);
        Assert.That(doc.RootElement.GetProperty("max_completion_tokens").GetInt32(), Is.EqualTo(1000));
        Assert.That(doc.RootElement.GetProperty("reasoning_effort").GetString(), Is.EqualTo("high"));
    }

    [Test]
    public async Task IncludeRawResponse_ReturnsRawRequestJson()
    {
        var handler = new MockHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(AzureResponseJson, System.Text.Encoding.UTF8, "application/json")
            }));

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://myresource.openai.azure.com/") };
        var options = new AzureOpenAiClientOptions
        {
            Endpoint = "https://myresource.openai.azure.com/",
            DeploymentName = "gpt-4",
            ApiVersion = "2024-02-01",
            ApiKey = "test-key"
        };
        var client = new AzureOpenAiChatCompletionClient(httpClient, options);

        var extra = JsonDocument.Parse("""{"top_p":0.9}""").RootElement;
        var request = new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "Hello")],
            Model: "gpt-4",
            IncludeRawResponse: true,
            ExtraParameters: extra);

        var response = await client.GetChatCompletionAsync(request);

        Assert.That(response.RawRequestJson, Is.Not.Null);
        var doc = JsonDocument.Parse(response.RawRequestJson!);
        Assert.That(doc.RootElement.GetProperty("top_p").GetDouble(), Is.EqualTo(0.9));
    }

    private const string AzureResponseJson = """
        {
            "model": "gpt-4",
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
