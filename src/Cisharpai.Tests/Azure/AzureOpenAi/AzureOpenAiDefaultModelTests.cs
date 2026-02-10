using System.Net;
using System.Text.Json;
using Cisharpai.Azure.AzureOpenAi;
using Cisharpai.Models;

namespace Cisharpai.Tests.Azure.AzureOpenAi;

public sealed class AzureOpenAiDefaultModelTests
{
    private const string DeploymentName = "my-deployment";
    private const string ApiKey = "test-key";
    private static readonly Uri BaseAddress = new("https://myresource.openai.azure.com/");

    private const string ChatResponseJson = """
        {
            "model": "gpt-4o",
            "choices": [
                {
                    "message": {
                        "role": "assistant",
                        "content": "Hello"
                    }
                }
            ],
            "usage": {
                "prompt_tokens": 10,
                "completion_tokens": 5
            }
        }
        """;

    private static AzureOpenAiChatCompletionClient CreateClient(
        MockHttpMessageHandler handler,
        string? defaultModel = null)
    {
        var httpClient = new HttpClient(handler) { BaseAddress = BaseAddress };
        var options = new AzureOpenAiClientOptions
        {
            DeploymentName = DeploymentName,
            ApiKey = ApiKey,
            DefaultModel = defaultModel
        };
        return new AzureOpenAiChatCompletionClient(httpClient, options);
    }

    private static MockHttpMessageHandler CreateHandler(
        out Func<string?> getCapturedBody)
    {
        string? captured = null;
        var handler = new MockHttpMessageHandler(async (request, _) =>
        {
            captured = await request.Content!.ReadAsStringAsync();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(ChatResponseJson, System.Text.Encoding.UTF8, "application/json")
            };
        });
        getCapturedBody = () => captured;
        return handler;
    }

    [Test]
    public async Task ChatClient_NullModel_NoDefaultModel_TreatsAsLegacy()
    {
        var handler = CreateHandler(out var getCapturedBody);
        var client = CreateClient(handler);

        var request = new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "Hi")],
            MaxTokens: 100);

        var response = await client.GetChatCompletionAsync(request);

        Assert.That(response.IsSuccess, Is.True);

        // Legacy format uses max_tokens, not max_completion_tokens
        var doc = JsonDocument.Parse(getCapturedBody()!);
        Assert.That(doc.RootElement.TryGetProperty("max_tokens", out _), Is.True);
        Assert.That(doc.RootElement.TryGetProperty("max_completion_tokens", out _), Is.False);
    }

    [Test]
    public async Task ChatClient_NullModel_DefaultModelIsLegacy_UsesLegacyFormat()
    {
        var handler = CreateHandler(out var getCapturedBody);
        var client = CreateClient(handler, defaultModel: "gpt-4o");

        var request = new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "Hi")],
            MaxTokens: 100);

        var response = await client.GetChatCompletionAsync(request);

        Assert.That(response.IsSuccess, Is.True);

        var doc = JsonDocument.Parse(getCapturedBody()!);
        Assert.That(doc.RootElement.TryGetProperty("max_tokens", out _), Is.True);
        Assert.That(doc.RootElement.TryGetProperty("max_completion_tokens", out _), Is.False);
    }

    [Test]
    public async Task ChatClient_NullModel_DefaultModelIsReasoning_UsesReasoningFormat()
    {
        var handler = CreateHandler(out var getCapturedBody);
        var client = CreateClient(handler, defaultModel: "o3-mini");

        var request = new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "Hi")],
            MaxTokens: 100);

        var response = await client.GetChatCompletionAsync(request);

        Assert.That(response.IsSuccess, Is.True);

        // Reasoning format uses max_completion_tokens instead of max_tokens
        var doc = JsonDocument.Parse(getCapturedBody()!);
        Assert.That(doc.RootElement.TryGetProperty("max_completion_tokens", out _), Is.True);
        Assert.That(doc.RootElement.TryGetProperty("max_tokens", out _), Is.False);
    }

    [Test]
    public async Task ChatClient_RequestModel_TakesPrecedenceOverDefaultModel()
    {
        var handler = CreateHandler(out var getCapturedBody);
        var client = CreateClient(handler, defaultModel: "gpt-4o");

        // Request specifies a reasoning model even though default is legacy
        var request = new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "Hi")],
            Model: "o3-mini",
            MaxTokens: 100);

        var response = await client.GetChatCompletionAsync(request);

        Assert.That(response.IsSuccess, Is.True);

        // Should use reasoning format from request model, not legacy from default
        var doc = JsonDocument.Parse(getCapturedBody()!);
        Assert.That(doc.RootElement.TryGetProperty("max_completion_tokens", out _), Is.True);
        Assert.That(doc.RootElement.TryGetProperty("max_tokens", out _), Is.False);
    }

    [Test]
    public async Task ChatClient_JsonOutput_NullModel_DefaultModelIsReasoning_UsesReasoningFormat()
    {
        var handler = CreateHandler(out var getCapturedBody);
        var client = CreateClient(handler, defaultModel: "o3-mini");

        var request = new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "Give me JSON")],
            MaxTokens: 100);

        var jsonOptions = new JsonOutputOptions(Mode: JsonOutputMode.JsonMode);

        var response = await client.GetChatCompletionWithJsonOutputAsync(request, jsonOptions);

        Assert.That(response.IsSuccess, Is.True);

        var doc = JsonDocument.Parse(getCapturedBody()!);
        Assert.That(doc.RootElement.TryGetProperty("max_completion_tokens", out _), Is.True);
        Assert.That(doc.RootElement.TryGetProperty("max_tokens", out _), Is.False);
        Assert.That(doc.RootElement.GetProperty("response_format").GetProperty("type").GetString(),
            Is.EqualTo("json_object"));
    }

    [Test]
    public async Task ChatClient_JsonOutput_NullModel_NoDefaultModel_TreatsAsLegacy()
    {
        var handler = CreateHandler(out var getCapturedBody);
        var client = CreateClient(handler);

        var request = new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "Give me JSON")],
            MaxTokens: 100);

        var jsonOptions = new JsonOutputOptions(Mode: JsonOutputMode.JsonMode);

        var response = await client.GetChatCompletionWithJsonOutputAsync(request, jsonOptions);

        Assert.That(response.IsSuccess, Is.True);

        var doc = JsonDocument.Parse(getCapturedBody()!);
        Assert.That(doc.RootElement.TryGetProperty("max_tokens", out _), Is.True);
        Assert.That(doc.RootElement.TryGetProperty("max_completion_tokens", out _), Is.False);
    }

    [Test]
    public void DefaultModel_Property_IsNullByDefault()
    {
        var options = new AzureOpenAiClientOptions();
        Assert.That(options.DefaultModel, Is.Null);
    }

    [Test]
    public void DefaultModel_Property_CanBeSet()
    {
        var options = new AzureOpenAiClientOptions { DefaultModel = "gpt-4o" };
        Assert.That(options.DefaultModel, Is.EqualTo("gpt-4o"));
    }
}
