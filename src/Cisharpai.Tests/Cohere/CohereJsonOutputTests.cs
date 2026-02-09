using System.Net;
using System.Text.Json;
using Cisharpai.Features.Chat;
using Cisharpai.Models;
using Cisharpai.Cohere;

namespace Cisharpai.Tests.Cohere;

public sealed class CohereJsonOutputTests
{
    private const string JsonSchemaString =
        """{"type":"object","properties":{"name":{"type":"string"},"age":{"type":"integer"}},"required":["name","age"],"additionalProperties":false}""";

    #region Response Fixtures

    private const string JsonResponseJson = """
        {
            "id": "abc-123",
            "finish_reason": "COMPLETE",
            "message": {
                "role": "assistant",
                "content": [
                    {
                        "type": "text",
                        "text": "{\"name\":\"John\",\"age\":30}"
                    }
                ]
            },
            "usage": {
                "billed_units": {
                    "input_tokens": 15,
                    "output_tokens": 10
                },
                "tokens": {
                    "input_tokens": 200,
                    "output_tokens": 10
                }
            }
        }
        """;

    #endregion

    #region JSON Mode Tests

    [Test]
    public async Task JsonMode_SetsResponseFormatJsonObject()
    {
        string? capturedBody = null;
        var handler = new MockHttpMessageHandler(async (request, _) =>
        {
            capturedBody = await request.Content!.ReadAsStringAsync();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonResponseJson, System.Text.Encoding.UTF8, "application/json")
            };
        });

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.cohere.com/v2/") };
        var client = new CohereChatCompletionClient(httpClient);

        var request = new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "Give me a person")],
            Model: "command-a-03-2025");

        var jsonOptions = new JsonOutputOptions(Mode: JsonOutputMode.JsonMode);

        await client.GetChatCompletionWithJsonOutputAsync(request, jsonOptions);

        Assert.That(capturedBody, Is.Not.Null);
        var doc = JsonDocument.Parse(capturedBody!);
        var responseFormat = doc.RootElement.GetProperty("response_format");
        Assert.That(responseFormat.GetProperty("type").GetString(), Is.EqualTo("json_object"));
    }

    [Test]
    public async Task JsonMode_DoesNotSetJsonSchema()
    {
        string? capturedBody = null;
        var handler = new MockHttpMessageHandler(async (request, _) =>
        {
            capturedBody = await request.Content!.ReadAsStringAsync();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonResponseJson, System.Text.Encoding.UTF8, "application/json")
            };
        });

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.cohere.com/v2/") };
        var client = new CohereChatCompletionClient(httpClient);

        var request = new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "Give me a person")],
            Model: "command-a-03-2025");

        var jsonOptions = new JsonOutputOptions(Mode: JsonOutputMode.JsonMode);

        await client.GetChatCompletionWithJsonOutputAsync(request, jsonOptions);

        Assert.That(capturedBody, Is.Not.Null);
        var doc = JsonDocument.Parse(capturedBody!);
        var responseFormat = doc.RootElement.GetProperty("response_format");
        Assert.That(responseFormat.TryGetProperty("json_schema", out _), Is.False);
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
                Content = new StringContent(JsonResponseJson, System.Text.Encoding.UTF8, "application/json")
            };
        });

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.cohere.com/v2/") };
        var client = new CohereChatCompletionClient(httpClient);

        var request = new ChatCompletionRequest(
            Messages:
            [
                new LlmMessage(LlmRole.System, "Be helpful"),
                new LlmMessage(LlmRole.User, "Give me a person")
            ],
            Model: "command-a-03-2025");

        var jsonOptions = new JsonOutputOptions(Mode: JsonOutputMode.JsonMode);

        await client.GetChatCompletionWithJsonOutputAsync(request, jsonOptions);

        Assert.That(capturedBody, Is.Not.Null);
        var doc = JsonDocument.Parse(capturedBody!);
        var messages = doc.RootElement.GetProperty("messages");
        var systemContent = messages[0].GetProperty("content").GetString();
        Assert.That(systemContent, Is.EqualTo("Be helpful Respond with raw JSON only, no markdown formatting."));
    }

    [Test]
    public async Task JsonMode_PreservesExistingJsonKeyword()
    {
        string? capturedBody = null;
        var handler = new MockHttpMessageHandler(async (request, _) =>
        {
            capturedBody = await request.Content!.ReadAsStringAsync();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonResponseJson, System.Text.Encoding.UTF8, "application/json")
            };
        });

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.cohere.com/v2/") };
        var client = new CohereChatCompletionClient(httpClient);

        var request = new ChatCompletionRequest(
            Messages:
            [
                new LlmMessage(LlmRole.System, "Return JSON data"),
                new LlmMessage(LlmRole.User, "Give me a person")
            ],
            Model: "command-a-03-2025");

        var jsonOptions = new JsonOutputOptions(Mode: JsonOutputMode.JsonMode);

        await client.GetChatCompletionWithJsonOutputAsync(request, jsonOptions);

        Assert.That(capturedBody, Is.Not.Null);
        var doc = JsonDocument.Parse(capturedBody!);
        var messages = doc.RootElement.GetProperty("messages");
        var systemContent = messages[0].GetProperty("content").GetString();
        Assert.That(systemContent, Is.EqualTo("Return JSON data"));
    }

    [Test]
    public async Task JsonMode_NoSystemMessage_AddsSystemMessageWithJson()
    {
        string? capturedBody = null;
        var handler = new MockHttpMessageHandler(async (request, _) =>
        {
            capturedBody = await request.Content!.ReadAsStringAsync();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonResponseJson, System.Text.Encoding.UTF8, "application/json")
            };
        });

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.cohere.com/v2/") };
        var client = new CohereChatCompletionClient(httpClient);

        var request = new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "Give me a person")],
            Model: "command-a-03-2025");

        var jsonOptions = new JsonOutputOptions(Mode: JsonOutputMode.JsonMode);

        await client.GetChatCompletionWithJsonOutputAsync(request, jsonOptions);

        Assert.That(capturedBody, Is.Not.Null);
        var doc = JsonDocument.Parse(capturedBody!);
        var messages = doc.RootElement.GetProperty("messages");
        Assert.That(messages[0].GetProperty("role").GetString(), Is.EqualTo("system"));
        Assert.That(messages[0].GetProperty("content").GetString(), Is.EqualTo("Respond with raw JSON only, no markdown formatting."));
    }

    #endregion

    #region Structured Outputs (JSON Schema) Tests

    [Test]
    public async Task JsonSchema_SetsResponseFormatWithSchema()
    {
        string? capturedBody = null;
        var handler = new MockHttpMessageHandler(async (request, _) =>
        {
            capturedBody = await request.Content!.ReadAsStringAsync();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonResponseJson, System.Text.Encoding.UTF8, "application/json")
            };
        });

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.cohere.com/v2/") };
        var client = new CohereChatCompletionClient(httpClient);

        var request = new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "Give me a person")],
            Model: "command-a-03-2025");

        var jsonOptions = new JsonOutputOptions(
            Mode: JsonOutputMode.JsonSchema,
            SchemaName: "person",
            JsonSchema: JsonSchemaString);

        await client.GetChatCompletionWithJsonOutputAsync(request, jsonOptions);

        Assert.That(capturedBody, Is.Not.Null);
        var doc = JsonDocument.Parse(capturedBody!);
        var responseFormat = doc.RootElement.GetProperty("response_format");
        Assert.That(responseFormat.GetProperty("type").GetString(), Is.EqualTo("json_object"));
        Assert.That(responseFormat.TryGetProperty("json_schema", out _), Is.True);
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
                Content = new StringContent(JsonResponseJson, System.Text.Encoding.UTF8, "application/json")
            };
        });

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.cohere.com/v2/") };
        var client = new CohereChatCompletionClient(httpClient);

        var request = new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "Give me a person")],
            Model: "command-a-03-2025");

        var jsonOptions = new JsonOutputOptions(
            Mode: JsonOutputMode.JsonSchema,
            SchemaName: "person",
            JsonSchema: JsonSchemaString);

        await client.GetChatCompletionWithJsonOutputAsync(request, jsonOptions);

        Assert.That(capturedBody, Is.Not.Null);
        var doc = JsonDocument.Parse(capturedBody!);
        var schema = doc.RootElement
            .GetProperty("response_format")
            .GetProperty("json_schema");

        // Verify it's a JSON object, not a serialized string
        Assert.That(schema.ValueKind, Is.EqualTo(JsonValueKind.Object));
        Assert.That(schema.GetProperty("properties").GetProperty("name").GetProperty("type").GetString(), Is.EqualTo("string"));
        Assert.That(schema.GetProperty("properties").GetProperty("age").GetProperty("type").GetString(), Is.EqualTo("integer"));
        Assert.That(schema.GetProperty("additionalProperties").GetBoolean(), Is.False);
    }

    [Test]
    public async Task JsonSchema_DoesNotInjectSystemMessage()
    {
        string? capturedBody = null;
        var handler = new MockHttpMessageHandler(async (request, _) =>
        {
            capturedBody = await request.Content!.ReadAsStringAsync();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonResponseJson, System.Text.Encoding.UTF8, "application/json")
            };
        });

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.cohere.com/v2/") };
        var client = new CohereChatCompletionClient(httpClient);

        var request = new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "Give me a person")],
            Model: "command-a-03-2025");

        var jsonOptions = new JsonOutputOptions(
            Mode: JsonOutputMode.JsonSchema,
            SchemaName: "person",
            JsonSchema: JsonSchemaString);

        await client.GetChatCompletionWithJsonOutputAsync(request, jsonOptions);

        Assert.That(capturedBody, Is.Not.Null);
        var doc = JsonDocument.Parse(capturedBody!);
        var messages = doc.RootElement.GetProperty("messages");
        // Only the user message should be present
        Assert.That(messages.GetArrayLength(), Is.EqualTo(1));
        Assert.That(messages[0].GetProperty("role").GetString(), Is.EqualTo("user"));
    }

    #endregion

    #region Markdown Code Fence Stripping Tests

    [Test]
    public async Task JsonMode_StripsMarkdownCodeFences()
    {
        const string responseWithFences = """
            {
                "id": "abc-123",
                "finish_reason": "COMPLETE",
                "message": {
                    "role": "assistant",
                    "content": [
                        {
                            "type": "text",
                            "text": "```json\n{\"name\":\"John\",\"age\":30}\n```"
                        }
                    ]
                },
                "usage": {
                    "billed_units": { "input_tokens": 15, "output_tokens": 10 },
                    "tokens": { "input_tokens": 200, "output_tokens": 10 }
                }
            }
            """;

        var handler = new MockHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseWithFences, System.Text.Encoding.UTF8, "application/json")
            }));

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.cohere.com/v2/") };
        var client = new CohereChatCompletionClient(httpClient);

        var request = new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "Give me a person")],
            Model: "command-a-03-2025");

        var jsonOptions = new JsonOutputOptions(Mode: JsonOutputMode.JsonMode);

        var response = await client.GetChatCompletionWithJsonOutputAsync(request, jsonOptions);

        Assert.That(response.Content, Is.EqualTo("{\"name\":\"John\",\"age\":30}"));
    }

    [Test]
    public async Task JsonMode_WithNoFences_ReturnsContentAsIs()
    {
        var handler = new MockHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonResponseJson, System.Text.Encoding.UTF8, "application/json")
            }));

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.cohere.com/v2/") };
        var client = new CohereChatCompletionClient(httpClient);

        var request = new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "Give me a person")],
            Model: "command-a-03-2025");

        var jsonOptions = new JsonOutputOptions(Mode: JsonOutputMode.JsonMode);

        var response = await client.GetChatCompletionWithJsonOutputAsync(request, jsonOptions);

        Assert.That(response.Content, Is.EqualTo("{\"name\":\"John\",\"age\":30}"));
    }

    #endregion

    #region Feature Discovery Tests

    [Test]
    public void Features_GetJsonOutputFeature_ReturnsSelf()
    {
        using var httpClient = new HttpClient { BaseAddress = new Uri("https://api.cohere.com/v2/") };
        var client = new CohereChatCompletionClient(httpClient);

        var feature = client.Features.Get<IJsonOutputFeature>();

        Assert.That(feature, Is.Not.Null);
        Assert.That(feature, Is.SameAs(client));
    }

    #endregion
}
