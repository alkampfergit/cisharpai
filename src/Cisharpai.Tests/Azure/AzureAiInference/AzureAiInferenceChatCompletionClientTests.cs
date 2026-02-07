using System.Net;
using System.Text.Json;
using Cisharpai.Models;
using Cisharpai.Azure.AzureAiInference;

namespace Cisharpai.Tests.Azure.AzureAiInference;

public sealed class AzureAiInferenceChatCompletionClientTests
{
    [Test]
    public async Task GetChatCompletionAsync_MapsResponseToSharedModel()
    {
        var handler = new MockHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(InferenceResponseJson, System.Text.Encoding.UTF8, "application/json")
            }));

        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://my-resource.services.ai.azure.com/")
        };
        var options = new AzureAiInferenceClientOptions
        {
            Endpoint = "https://my-resource.services.ai.azure.com/",
            ModelId = "Phi-3-mini-4k-instruct",
            ApiKey = "test-key"
        };
        var client = new AzureAiInferenceChatCompletionClient(httpClient, options);

        var response = await client.GetChatCompletionAsync(new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "Hello")],
            Model: "Phi-3-mini-4k-instruct"));

        Assert.That(response.Content, Is.EqualTo("Hello there!"));
        Assert.That(response.Model, Is.EqualTo("Phi-3-mini-4k-instruct"));
        Assert.That(response.PromptTokens, Is.EqualTo(10));
        Assert.That(response.CompletionTokens, Is.EqualTo(5));
        Assert.That(response.IsSuccess, Is.True);
        Assert.That(response.ErrorMessage, Is.Null);
        Assert.That(response.Status, Is.EqualTo("stop"));
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

        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://my-resource.services.ai.azure.com/")
        };
        var options = new AzureAiInferenceClientOptions
        {
            Endpoint = "https://my-resource.services.ai.azure.com/",
            ModelId = "Phi-3-mini-4k-instruct",
            ApiKey = "test-key"
        };
        var client = new AzureAiInferenceChatCompletionClient(httpClient, options);

        var response = await client.GetChatCompletionAsync(new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "Hello")],
            Model: "Phi-3-mini-4k-instruct"));

        Assert.That(response.IsSuccess, Is.False);
        Assert.That(response.ErrorMessage, Does.Contain("500"));
        Assert.That(response.Content, Is.EqualTo(string.Empty));
        Assert.That(response.Model, Is.EqualTo(string.Empty));
    }

    [Test]
    public async Task GetChatCompletionAsync_UsesConfiguredModelIdWhenModelNotSpecified()
    {
        string? capturedBody = null;
        var handler = new MockHttpMessageHandler(async (request, _) =>
        {
            capturedBody = await request.Content!.ReadAsStringAsync();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(InferenceResponseJson, System.Text.Encoding.UTF8, "application/json")
            };
        });

        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://my-resource.services.ai.azure.com/")
        };
        var options = new AzureAiInferenceClientOptions
        {
            Endpoint = "https://my-resource.services.ai.azure.com/",
            ModelId = "configured-model",
            ApiKey = "test-key"
        };
        var client = new AzureAiInferenceChatCompletionClient(httpClient, options);

        await client.GetChatCompletionAsync(new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "Hello")],
            Model: "")); // Empty model

        var doc = JsonDocument.Parse(capturedBody!);
        Assert.That(doc.RootElement.GetProperty("model").GetString(), Is.EqualTo("configured-model"));
    }

    [Test]
    public async Task GetChatCompletionAsync_ExtraParameters_AreMergedIntoRequest()
    {
        string? capturedBody = null;
        var handler = new MockHttpMessageHandler(async (request, _) =>
        {
            capturedBody = await request.Content!.ReadAsStringAsync();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(InferenceResponseJson, System.Text.Encoding.UTF8, "application/json")
            };
        });

        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://my-resource.services.ai.azure.com/")
        };
        var options = new AzureAiInferenceClientOptions
        {
            Endpoint = "https://my-resource.services.ai.azure.com/",
            ModelId = "Phi-3-mini-4k-instruct",
            ApiKey = "test-key"
        };
        var client = new AzureAiInferenceChatCompletionClient(httpClient, options);

        var extra = JsonDocument.Parse("""{"top_p":0.9,"presence_penalty":0.5}""").RootElement;
        var request = new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "Hello")],
            Model: "Phi-3-mini-4k-instruct",
            Temperature: 0.7,
            ExtraParameters: extra);

        await client.GetChatCompletionAsync(request);

        var doc = JsonDocument.Parse(capturedBody!);
        Assert.That(doc.RootElement.GetProperty("temperature").GetDouble(), Is.EqualTo(0.7));
        Assert.That(doc.RootElement.GetProperty("top_p").GetDouble(), Is.EqualTo(0.9));
        Assert.That(doc.RootElement.GetProperty("presence_penalty").GetDouble(), Is.EqualTo(0.5));
    }

    [Test]
    public async Task GetChatCompletionAsync_IncludeRawResponse_ReturnsBothRawJsons()
    {
        var handler = new MockHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(InferenceResponseJson, System.Text.Encoding.UTF8, "application/json")
            }));

        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://my-resource.services.ai.azure.com/")
        };
        var options = new AzureAiInferenceClientOptions
        {
            Endpoint = "https://my-resource.services.ai.azure.com/",
            ModelId = "Phi-3-mini-4k-instruct",
            ApiKey = "test-key"
        };
        var client = new AzureAiInferenceChatCompletionClient(httpClient, options);

        var response = await client.GetChatCompletionAsync(new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "Hello")],
            Model: "Phi-3-mini-4k-instruct",
            IncludeRawResponse: true));

        Assert.That(response.RawResponseJson, Is.Not.Null);
        Assert.That(response.RawRequestJson, Is.Not.Null);
    }

    [Test]
    public async Task GetChatCompletionAsync_LengthFinishReason_MapsToIncompleteReason()
    {
        var responseWithLength = """
            {
                "id": "chatcmpl-123",
                "model": "Phi-3-mini-4k-instruct",
                "choices": [
                    {
                        "index": 0,
                        "message": { "role": "assistant", "content": "Truncated..." },
                        "finish_reason": "length"
                    }
                ],
                "usage": { "prompt_tokens": 10, "completion_tokens": 100, "total_tokens": 110 }
            }
            """;

        var handler = new MockHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseWithLength, System.Text.Encoding.UTF8, "application/json")
            }));

        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://my-resource.services.ai.azure.com/")
        };
        var options = new AzureAiInferenceClientOptions
        {
            Endpoint = "https://my-resource.services.ai.azure.com/",
            ModelId = "Phi-3-mini-4k-instruct",
            ApiKey = "test-key"
        };
        var client = new AzureAiInferenceChatCompletionClient(httpClient, options);

        var response = await client.GetChatCompletionAsync(new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "Hello")],
            Model: "Phi-3-mini-4k-instruct"));

        Assert.That(response.Status, Is.EqualTo("length"));
        Assert.That(response.IncompleteReason, Is.EqualTo("Token limit reached"));
    }

    [TestCase("gpt-5-nano")]
    [TestCase("gpt-5")]
    [TestCase("o1-preview")]
    [TestCase("o3-mini")]
    [TestCase("o4-mini")]
    public async Task GetChatCompletionAsync_ReasoningModel_UsesMaxCompletionTokens(string modelId)
    {
        string? capturedBody = null;
        var handler = new MockHttpMessageHandler(async (request, _) =>
        {
            capturedBody = await request.Content!.ReadAsStringAsync();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(InferenceResponseJson, System.Text.Encoding.UTF8, "application/json")
            };
        });

        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://my-resource.services.ai.azure.com/")
        };
        var options = new AzureAiInferenceClientOptions
        {
            Endpoint = "https://my-resource.services.ai.azure.com/",
            ModelId = modelId,
            ApiKey = "test-key"
        };
        var client = new AzureAiInferenceChatCompletionClient(httpClient, options);

        await client.GetChatCompletionAsync(new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "Hello")],
            Model: modelId,
            Temperature: 0.7,
            MaxTokens: 200));

        var doc = JsonDocument.Parse(capturedBody!);
        Assert.That(doc.RootElement.TryGetProperty("max_completion_tokens", out var mct), Is.True,
            "Reasoning model request should contain max_completion_tokens");
        Assert.That(mct.GetInt32(), Is.EqualTo(200));
        Assert.That(doc.RootElement.TryGetProperty("max_tokens", out _), Is.False,
            "Reasoning model request should not contain max_tokens");
        Assert.That(doc.RootElement.TryGetProperty("temperature", out _), Is.False,
            "Reasoning model request should not contain temperature");
    }

    [Test]
    public async Task GetChatCompletionAsync_LegacyModel_UsesMaxTokensAndTemperature()
    {
        string? capturedBody = null;
        var handler = new MockHttpMessageHandler(async (request, _) =>
        {
            capturedBody = await request.Content!.ReadAsStringAsync();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(InferenceResponseJson, System.Text.Encoding.UTF8, "application/json")
            };
        });

        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://my-resource.services.ai.azure.com/")
        };
        var options = new AzureAiInferenceClientOptions
        {
            Endpoint = "https://my-resource.services.ai.azure.com/",
            ModelId = "Phi-3-mini-4k-instruct",
            ApiKey = "test-key"
        };
        var client = new AzureAiInferenceChatCompletionClient(httpClient, options);

        await client.GetChatCompletionAsync(new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "Hello")],
            Model: "Phi-3-mini-4k-instruct",
            Temperature: 0.7,
            MaxTokens: 200));

        var doc = JsonDocument.Parse(capturedBody!);
        Assert.That(doc.RootElement.TryGetProperty("max_tokens", out var mt), Is.True,
            "Legacy model request should contain max_tokens");
        Assert.That(mt.GetInt32(), Is.EqualTo(200));
        Assert.That(doc.RootElement.TryGetProperty("temperature", out var temp), Is.True,
            "Legacy model request should contain temperature");
        Assert.That(temp.GetDouble(), Is.EqualTo(0.7));
        Assert.That(doc.RootElement.TryGetProperty("max_completion_tokens", out _), Is.False,
            "Legacy model request should not contain max_completion_tokens");
    }

    [Test]
    public async Task GetChatCompletionAsync_ReasoningModelFromOptions_UsesMaxCompletionTokens()
    {
        string? capturedBody = null;
        var handler = new MockHttpMessageHandler(async (request, _) =>
        {
            capturedBody = await request.Content!.ReadAsStringAsync();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(InferenceResponseJson, System.Text.Encoding.UTF8, "application/json")
            };
        });

        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://my-resource.services.ai.azure.com/")
        };
        var options = new AzureAiInferenceClientOptions
        {
            Endpoint = "https://my-resource.services.ai.azure.com/",
            ModelId = "gpt-5-nano",
            ApiKey = "test-key"
        };
        var client = new AzureAiInferenceChatCompletionClient(httpClient, options);

        // Empty model in request — should fall back to options.ModelId for detection
        await client.GetChatCompletionAsync(new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "Hello")],
            Model: "",
            MaxTokens: 150));

        var doc = JsonDocument.Parse(capturedBody!);
        Assert.That(doc.RootElement.TryGetProperty("max_completion_tokens", out var mct), Is.True,
            "Should detect reasoning model from options.ModelId when request.Model is empty");
        Assert.That(mct.GetInt32(), Is.EqualTo(150));
        Assert.That(doc.RootElement.TryGetProperty("max_tokens", out _), Is.False);
    }

    private const string InferenceResponseJson = """
        {
            "id": "chatcmpl-123",
            "object": "chat.completion",
            "created": 1696522361,
            "model": "Phi-3-mini-4k-instruct",
            "choices": [
                {
                    "index": 0,
                    "message": {
                        "role": "assistant",
                        "content": "Hello there!"
                    },
                    "finish_reason": "stop"
                }
            ],
            "usage": {
                "prompt_tokens": 10,
                "completion_tokens": 5,
                "total_tokens": 15
            }
        }
        """;
}
