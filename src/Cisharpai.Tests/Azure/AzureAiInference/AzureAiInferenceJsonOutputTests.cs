using System.Net;
using System.Text.Json;
using Cisharpai.Azure.AzureAiInference;
using Cisharpai.Features.Chat;
using Cisharpai.Models;

namespace Cisharpai.Tests.Azure.AzureAiInference;

public sealed class AzureAiInferenceJsonOutputTests
{
    private const string JsonResponseFixture = """
        {
            "id": "chatcmpl-123",
            "object": "chat.completion",
            "created": 1700000000,
            "model": "Phi-3-mini-4k-instruct",
            "choices": [
                {
                    "index": 0,
                    "message": {
                        "role": "assistant",
                        "content": "{\"name\":\"John\",\"age\":30}"
                    },
                    "finish_reason": "stop"
                }
            ],
            "usage": {
                "prompt_tokens": 15,
                "completion_tokens": 10,
                "total_tokens": 25
            }
        }
        """;

    private const string TestSchema =
        """{"type":"object","properties":{"name":{"type":"string"},"age":{"type":"integer"}},"required":["name","age"],"additionalProperties":false}""";

    #region JSON Mode Tests

    [Test]
    public async Task JsonMode_StandardModel_SetsResponseFormatJsonObject()
    {
        string? capturedBody = null;
        var handler = new MockHttpMessageHandler(async (request, _) =>
        {
            capturedBody = await request.Content!.ReadAsStringAsync();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonResponseFixture, System.Text.Encoding.UTF8, "application/json")
            };
        });

        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://test.inference.azure.com/")
        };
        var client = new AzureAiInferenceChatCompletionClient(httpClient,
            new AzureAiInferenceClientOptions { ModelId = "Phi-3-mini", ApiKey = "key" });

        var jsonFeature = client.Features.Get<IJsonOutputFeature>()!;
        var response = await jsonFeature.GetChatCompletionWithJsonOutputAsync(
            new ChatCompletionRequest(
                Messages: [new LlmMessage(LlmRole.User, "Give me a person")],
                Model: "Phi-3-mini"),
            new JsonOutputOptions(Mode: JsonOutputMode.JsonMode));

        Assert.That(response.IsSuccess, Is.True);

        var doc = JsonDocument.Parse(capturedBody!);
        var responseFormat = doc.RootElement.GetProperty("response_format");
        Assert.That(responseFormat.GetProperty("type").GetString(), Is.EqualTo("json_object"));
    }

    [Test]
    public async Task JsonMode_ReasoningModel_SetsResponseFormatJsonObject()
    {
        string? capturedBody = null;
        var handler = new MockHttpMessageHandler(async (request, _) =>
        {
            capturedBody = await request.Content!.ReadAsStringAsync();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonResponseFixture, System.Text.Encoding.UTF8, "application/json")
            };
        });

        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://test.inference.azure.com/")
        };
        var client = new AzureAiInferenceChatCompletionClient(httpClient,
            new AzureAiInferenceClientOptions { ModelId = "o3-mini", ApiKey = "key" });

        var jsonFeature = client.Features.Get<IJsonOutputFeature>()!;
        await jsonFeature.GetChatCompletionWithJsonOutputAsync(
            new ChatCompletionRequest(
                Messages: [new LlmMessage(LlmRole.User, "Give me a person")],
                Model: "o3-mini"),
            new JsonOutputOptions(Mode: JsonOutputMode.JsonMode));

        var doc = JsonDocument.Parse(capturedBody!);
        var responseFormat = doc.RootElement.GetProperty("response_format");
        Assert.That(responseFormat.GetProperty("type").GetString(), Is.EqualTo("json_object"));

        // Reasoning model should use max_completion_tokens, not max_tokens
        Assert.That(doc.RootElement.TryGetProperty("temperature", out _), Is.False,
            "Reasoning model request should not contain temperature");
    }

    [Test]
    public async Task JsonMode_InjectsJsonKeywordInSystemMessage()
    {
        string? capturedBody = null;
        var handler = new MockHttpMessageHandler(async (request, _) =>
        {
            capturedBody = await request.Content!.ReadAsStringAsync();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonResponseFixture, System.Text.Encoding.UTF8, "application/json")
            };
        });

        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://test.inference.azure.com/")
        };
        var client = new AzureAiInferenceChatCompletionClient(httpClient,
            new AzureAiInferenceClientOptions { ModelId = "Phi-3-mini", ApiKey = "key" });

        var jsonFeature = client.Features.Get<IJsonOutputFeature>()!;
        await jsonFeature.GetChatCompletionWithJsonOutputAsync(
            new ChatCompletionRequest(
                Messages:
                [
                    new LlmMessage(LlmRole.System, "You are a helpful assistant."),
                    new LlmMessage(LlmRole.User, "Give me a person")
                ],
                Model: "Phi-3-mini"),
            new JsonOutputOptions(Mode: JsonOutputMode.JsonMode));

        var doc = JsonDocument.Parse(capturedBody!);
        var messages = doc.RootElement.GetProperty("messages");
        var systemContent = messages[0].GetProperty("content").GetString();

        Assert.That(systemContent, Does.Contain("JSON"),
            "System message should contain 'JSON' keyword when using JsonMode");
    }

    #endregion

    #region Structured Outputs Tests

    [Test]
    public async Task JsonSchema_StandardModel_SetsResponseFormatJsonSchema()
    {
        string? capturedBody = null;
        var handler = new MockHttpMessageHandler(async (request, _) =>
        {
            capturedBody = await request.Content!.ReadAsStringAsync();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonResponseFixture, System.Text.Encoding.UTF8, "application/json")
            };
        });

        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://test.inference.azure.com/")
        };
        var client = new AzureAiInferenceChatCompletionClient(httpClient,
            new AzureAiInferenceClientOptions { ModelId = "Phi-3-mini", ApiKey = "key" });

        var jsonFeature = client.Features.Get<IJsonOutputFeature>()!;
        await jsonFeature.GetChatCompletionWithJsonOutputAsync(
            new ChatCompletionRequest(
                Messages: [new LlmMessage(LlmRole.User, "Give me a person")],
                Model: "Phi-3-mini"),
            new JsonOutputOptions(
                Mode: JsonOutputMode.JsonSchema,
                SchemaName: "person",
                JsonSchema: TestSchema));

        var doc = JsonDocument.Parse(capturedBody!);
        var responseFormat = doc.RootElement.GetProperty("response_format");
        Assert.That(responseFormat.GetProperty("type").GetString(), Is.EqualTo("json_schema"));

        var jsonSchema = responseFormat.GetProperty("json_schema");
        Assert.That(jsonSchema.GetProperty("name").GetString(), Is.EqualTo("person"));
        Assert.That(jsonSchema.GetProperty("strict").GetBoolean(), Is.True);
        Assert.That(jsonSchema.TryGetProperty("schema", out _), Is.True,
            "response_format.json_schema should contain schema property");
    }

    [Test]
    public async Task JsonSchema_SchemaStringParsedToJsonElement()
    {
        string? capturedBody = null;
        var handler = new MockHttpMessageHandler(async (request, _) =>
        {
            capturedBody = await request.Content!.ReadAsStringAsync();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonResponseFixture, System.Text.Encoding.UTF8, "application/json")
            };
        });

        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://test.inference.azure.com/")
        };
        var client = new AzureAiInferenceChatCompletionClient(httpClient,
            new AzureAiInferenceClientOptions { ModelId = "Phi-3-mini", ApiKey = "key" });

        var jsonFeature = client.Features.Get<IJsonOutputFeature>()!;
        await jsonFeature.GetChatCompletionWithJsonOutputAsync(
            new ChatCompletionRequest(
                Messages: [new LlmMessage(LlmRole.User, "Give me a person")],
                Model: "Phi-3-mini"),
            new JsonOutputOptions(
                Mode: JsonOutputMode.JsonSchema,
                SchemaName: "person",
                JsonSchema: TestSchema));

        var doc = JsonDocument.Parse(capturedBody!);
        var schema = doc.RootElement
            .GetProperty("response_format")
            .GetProperty("json_schema")
            .GetProperty("schema");

        // Verify the schema string was parsed into a proper JSON element (not a string)
        Assert.That(schema.ValueKind, Is.EqualTo(JsonValueKind.Object),
            "Schema should be a JSON object, not a string");
        Assert.That(schema.GetProperty("type").GetString(), Is.EqualTo("object"));
        Assert.That(schema.GetProperty("properties").GetProperty("name").GetProperty("type").GetString(),
            Is.EqualTo("string"));
        Assert.That(schema.GetProperty("properties").GetProperty("age").GetProperty("type").GetString(),
            Is.EqualTo("integer"));
        Assert.That(schema.GetProperty("additionalProperties").GetBoolean(), Is.False);
    }

    #endregion

    #region Feature Discovery

    [Test]
    public void Features_GetJsonOutputFeature_ReturnsSelf()
    {
        var handler = new MockHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));

        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://test.inference.azure.com/")
        };
        var client = new AzureAiInferenceChatCompletionClient(httpClient,
            new AzureAiInferenceClientOptions { ModelId = "Phi-3-mini", ApiKey = "key" });

        var feature = client.Features.Get<IJsonOutputFeature>();

        Assert.That(feature, Is.Not.Null);
        Assert.That(feature, Is.SameAs(client));
    }

    #endregion

    #region Azure AI Inference-Specific

    [Test]
    public async Task Request_UsesCorrectEndpoint()
    {
        Uri? capturedUri = null;
        var handler = new MockHttpMessageHandler((request, _) =>
        {
            capturedUri = request.RequestUri;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonResponseFixture, System.Text.Encoding.UTF8, "application/json")
            });
        });

        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://test.inference.azure.com/")
        };
        var client = new AzureAiInferenceChatCompletionClient(httpClient,
            new AzureAiInferenceClientOptions { ModelId = "Phi-3-mini", ApiKey = "key" });

        var jsonFeature = client.Features.Get<IJsonOutputFeature>()!;
        await jsonFeature.GetChatCompletionWithJsonOutputAsync(
            new ChatCompletionRequest(
                Messages: [new LlmMessage(LlmRole.User, "Give me a person")],
                Model: "Phi-3-mini"),
            new JsonOutputOptions(Mode: JsonOutputMode.JsonMode));

        Assert.That(capturedUri, Is.Not.Null);
        Assert.That(capturedUri!.PathAndQuery, Does.StartWith("/models/chat/completions"));
        Assert.That(capturedUri.Query, Does.Contain("api-version="));
    }

    [Test]
    public async Task Request_IncludesModelInPayload()
    {
        string? capturedBody = null;
        var handler = new MockHttpMessageHandler(async (request, _) =>
        {
            capturedBody = await request.Content!.ReadAsStringAsync();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonResponseFixture, System.Text.Encoding.UTF8, "application/json")
            };
        });

        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://test.inference.azure.com/")
        };
        var client = new AzureAiInferenceChatCompletionClient(httpClient,
            new AzureAiInferenceClientOptions { ModelId = "Phi-3-mini", ApiKey = "key" });

        var jsonFeature = client.Features.Get<IJsonOutputFeature>()!;
        await jsonFeature.GetChatCompletionWithJsonOutputAsync(
            new ChatCompletionRequest(
                Messages: [new LlmMessage(LlmRole.User, "Give me a person")],
                Model: "Phi-3-mini"),
            new JsonOutputOptions(Mode: JsonOutputMode.JsonMode));

        var doc = JsonDocument.Parse(capturedBody!);
        Assert.That(doc.RootElement.GetProperty("model").GetString(), Is.EqualTo("Phi-3-mini"));
    }

    #endregion
}
