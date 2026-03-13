using System.Net;
using System.Text.Json;
using Cisharpai.Features.Chat;
using Cisharpai.Models;
using Cisharpai.Anthropic;

namespace Cisharpai.Tests.Anthropic;

public sealed class AnthropicJsonOutputTests
{
    private const string JsonSchemaString =
        """{"type":"object","properties":{"name":{"type":"string"},"age":{"type":"integer"}},"required":["name","age"],"additionalProperties":false}""";

    #region Response Fixtures

    private const string JsonResponseJson = """
        {
            "model": "claude-sonnet-4-5-20250929",
            "content": [
                {
                    "type": "text",
                    "text": "{\"name\":\"John\",\"age\":30}"
                }
            ],
            "usage": {
                "input_tokens": 15,
                "output_tokens": 10
            },
            "stop_reason": "end_turn"
        }
        """;

    private const string RefusalResponseJson = """
        {
            "model": "claude-sonnet-4-5-20250929",
            "content": [
                {
                    "type": "text",
                    "text": "I cannot assist with that request."
                }
            ],
            "usage": {
                "input_tokens": 15,
                "output_tokens": 5
            },
            "stop_reason": "refusal"
        }
        """;

    #endregion

    #region JSON Mode Tests

    [Test]
    public async Task JsonMode_DoesNotSetOutputConfig()
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

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.anthropic.com/v1/") };
        var client = new AnthropicChatCompletionClient(httpClient, new AnthropicClientOptions());

        var request = new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "Give me a person")],
            Model: "claude-sonnet-4-5-20250929");

        var jsonOptions = new JsonOutputOptions(Mode: JsonOutputMode.JsonMode);

        await client.GetChatCompletionWithJsonOutputAsync(request, jsonOptions);

        Assert.That(capturedBody, Is.Not.Null);
        var doc = JsonDocument.Parse(capturedBody!);
        Assert.That(doc.RootElement.TryGetProperty("output_config", out _), Is.False);
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

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.anthropic.com/v1/") };
        var client = new AnthropicChatCompletionClient(httpClient, new AnthropicClientOptions());

        var request = new ChatCompletionRequest(
            Messages:
            [
                new LlmMessage(LlmRole.System, "Be helpful"),
                new LlmMessage(LlmRole.User, "Give me a person")
            ],
            Model: "claude-sonnet-4-5-20250929");

        var jsonOptions = new JsonOutputOptions(Mode: JsonOutputMode.JsonMode);

        await client.GetChatCompletionWithJsonOutputAsync(request, jsonOptions);

        Assert.That(capturedBody, Is.Not.Null);
        var doc = JsonDocument.Parse(capturedBody!);
        var system = doc.RootElement.GetProperty("system").GetString();
        Assert.That(system, Is.EqualTo("Be helpful Respond with raw JSON only, no markdown formatting."));
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

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.anthropic.com/v1/") };
        var client = new AnthropicChatCompletionClient(httpClient, new AnthropicClientOptions());

        var request = new ChatCompletionRequest(
            Messages:
            [
                new LlmMessage(LlmRole.System, "Return JSON data"),
                new LlmMessage(LlmRole.User, "Give me a person")
            ],
            Model: "claude-sonnet-4-5-20250929");

        var jsonOptions = new JsonOutputOptions(Mode: JsonOutputMode.JsonMode);

        await client.GetChatCompletionWithJsonOutputAsync(request, jsonOptions);

        Assert.That(capturedBody, Is.Not.Null);
        var doc = JsonDocument.Parse(capturedBody!);
        var system = doc.RootElement.GetProperty("system").GetString();
        Assert.That(system, Is.EqualTo("Return JSON data"));
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

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.anthropic.com/v1/") };
        var client = new AnthropicChatCompletionClient(httpClient, new AnthropicClientOptions());

        var request = new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "Give me a person")],
            Model: "claude-sonnet-4-5-20250929");

        var jsonOptions = new JsonOutputOptions(Mode: JsonOutputMode.JsonMode);

        await client.GetChatCompletionWithJsonOutputAsync(request, jsonOptions);

        Assert.That(capturedBody, Is.Not.Null);
        var doc = JsonDocument.Parse(capturedBody!);
        var system = doc.RootElement.GetProperty("system").GetString();
        Assert.That(system, Is.EqualTo("Respond with raw JSON only, no markdown formatting."));
    }

    #endregion

    #region Structured Outputs (JSON Schema) Tests

    [Test]
    public async Task JsonSchema_SetsOutputConfigJsonSchema()
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

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.anthropic.com/v1/") };
        var client = new AnthropicChatCompletionClient(httpClient, new AnthropicClientOptions());

        var request = new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "Give me a person")],
            Model: "claude-sonnet-4-5-20250929");

        var jsonOptions = new JsonOutputOptions(
            Mode: JsonOutputMode.JsonSchema,
            SchemaName: "person",
            JsonSchema: JsonSchemaString);

        await client.GetChatCompletionWithJsonOutputAsync(request, jsonOptions);

        Assert.That(capturedBody, Is.Not.Null);
        var doc = JsonDocument.Parse(capturedBody!);
        var outputConfig = doc.RootElement.GetProperty("output_config");
        var format = outputConfig.GetProperty("format");
        Assert.That(format.GetProperty("type").GetString(), Is.EqualTo("json_schema"));
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

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.anthropic.com/v1/") };
        var client = new AnthropicChatCompletionClient(httpClient, new AnthropicClientOptions());

        var request = new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "Give me a person")],
            Model: "claude-sonnet-4-5-20250929");

        var jsonOptions = new JsonOutputOptions(
            Mode: JsonOutputMode.JsonSchema,
            SchemaName: "person",
            JsonSchema: JsonSchemaString);

        await client.GetChatCompletionWithJsonOutputAsync(request, jsonOptions);

        Assert.That(capturedBody, Is.Not.Null);
        var doc = JsonDocument.Parse(capturedBody!);
        var schema = doc.RootElement
            .GetProperty("output_config")
            .GetProperty("format")
            .GetProperty("schema");

        // Verify it's a JSON object, not a serialized string
        Assert.Multiple(() =>
        {
            Assert.That(schema.ValueKind, Is.EqualTo(JsonValueKind.Object));
            Assert.That(schema.GetProperty("properties").GetProperty("name").GetProperty("type").GetString(), Is.EqualTo("string"));
            Assert.That(schema.GetProperty("properties").GetProperty("age").GetProperty("type").GetString(), Is.EqualTo("integer"));
            Assert.That(schema.GetProperty("additionalProperties").GetBoolean(), Is.False);
        });
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

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.anthropic.com/v1/") };
        var client = new AnthropicChatCompletionClient(httpClient, new AnthropicClientOptions());

        var request = new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "Give me a person")],
            Model: "claude-sonnet-4-5-20250929");

        var jsonOptions = new JsonOutputOptions(
            Mode: JsonOutputMode.JsonSchema,
            SchemaName: "person",
            JsonSchema: JsonSchemaString);

        await client.GetChatCompletionWithJsonOutputAsync(request, jsonOptions);

        Assert.That(capturedBody, Is.Not.Null);
        var doc = JsonDocument.Parse(capturedBody!);
        // For JsonSchema mode with no system message, system should not be present (null is omitted)
        Assert.That(doc.RootElement.TryGetProperty("system", out _), Is.False);
    }

    #endregion

    #region Refusal Tests

    [Test]
    public async Task StructuredOutput_WithRefusal_MapsToResponse()
    {
        var handler = new MockHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(RefusalResponseJson, System.Text.Encoding.UTF8, "application/json")
            }));

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.anthropic.com/v1/") };
        var client = new AnthropicChatCompletionClient(httpClient, new AnthropicClientOptions());

        var request = new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "Give me something dangerous")],
            Model: "claude-sonnet-4-5-20250929");

        var jsonOptions = new JsonOutputOptions(
            Mode: JsonOutputMode.JsonSchema,
            SchemaName: "person",
            JsonSchema: JsonSchemaString);

        var response = await client.GetChatCompletionWithJsonOutputAsync(request, jsonOptions);

        Assert.Multiple(() =>
        {
            Assert.That(response.Refusal, Is.EqualTo("I cannot assist with that request."));
            Assert.That(response.Content, Is.Empty);
        });
    }

    [Test]
    public async Task StructuredOutput_NoRefusal_ContentPopulated()
    {
        var handler = new MockHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonResponseJson, System.Text.Encoding.UTF8, "application/json")
            }));

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.anthropic.com/v1/") };
        var client = new AnthropicChatCompletionClient(httpClient, new AnthropicClientOptions());

        var request = new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "Give me a person")],
            Model: "claude-sonnet-4-5-20250929");

        var jsonOptions = new JsonOutputOptions(
            Mode: JsonOutputMode.JsonSchema,
            SchemaName: "person",
            JsonSchema: JsonSchemaString);

        var response = await client.GetChatCompletionWithJsonOutputAsync(request, jsonOptions);

        Assert.Multiple(() =>
        {
            Assert.That(response.Content, Is.EqualTo("{\"name\":\"John\",\"age\":30}"));
            Assert.That(response.Refusal, Is.Null);
            Assert.That(response.IsSuccess, Is.True);
        });
    }

    #endregion

    #region Markdown Code Fence Stripping Tests

    [Test]
    public async Task JsonMode_StripsMarkdownCodeFences()
    {
        const string responseWithFences = """
        {
            "model": "claude-sonnet-4-5-20250929",
            "content": [
                {
                    "type": "text",
                    "text": "```json\n{\"name\":\"John\",\"age\":30}\n```"
                }
            ],
            "usage": {
                "input_tokens": 15,
                "output_tokens": 10
            },
            "stop_reason": "end_turn"
        }
        """;

        var handler = new MockHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseWithFences, System.Text.Encoding.UTF8, "application/json")
            }));

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.anthropic.com/v1/") };
        var client = new AnthropicChatCompletionClient(httpClient, new AnthropicClientOptions());

        var request = new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "Give me a person")],
            Model: "claude-sonnet-4-5-20250929");

        var jsonOptions = new JsonOutputOptions(Mode: JsonOutputMode.JsonMode);

        var response = await client.GetChatCompletionWithJsonOutputAsync(request, jsonOptions);

        Assert.That(response.Content, Is.EqualTo("{\"name\":\"John\",\"age\":30}"));
    }

    [Test]
    public async Task JsonSchema_DoesNotStripMarkdownCodeFences()
    {
        var handler = new MockHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonResponseJson, System.Text.Encoding.UTF8, "application/json")
            }));

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.anthropic.com/v1/") };
        var client = new AnthropicChatCompletionClient(httpClient, new AnthropicClientOptions());

        var request = new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "Give me a person")],
            Model: "claude-sonnet-4-5-20250929");

        var jsonOptions = new JsonOutputOptions(
            Mode: JsonOutputMode.JsonSchema,
            SchemaName: "person",
            JsonSchema: JsonSchemaString);

        var response = await client.GetChatCompletionWithJsonOutputAsync(request, jsonOptions);

        // JsonSchema mode uses output_config - no stripping needed
        Assert.That(response.Content, Is.EqualTo("{\"name\":\"John\",\"age\":30}"));
    }

    [Test]
    public async Task JsonMode_WithPlainFences_StripsCodeFences()
    {
        const string responseWithPlainFences = """
        {
            "model": "claude-sonnet-4-5-20250929",
            "content": [
                {
                    "type": "text",
                    "text": "```\n{\"name\":\"John\"}\n```"
                }
            ],
            "usage": {
                "input_tokens": 15,
                "output_tokens": 10
            },
            "stop_reason": "end_turn"
        }
        """;

        var handler = new MockHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseWithPlainFences, System.Text.Encoding.UTF8, "application/json")
            }));

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.anthropic.com/v1/") };
        var client = new AnthropicChatCompletionClient(httpClient, new AnthropicClientOptions());

        var request = new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "Give me a person")],
            Model: "claude-sonnet-4-5-20250929");

        var jsonOptions = new JsonOutputOptions(Mode: JsonOutputMode.JsonMode);

        var response = await client.GetChatCompletionWithJsonOutputAsync(request, jsonOptions);

        Assert.That(response.Content, Is.EqualTo("{\"name\":\"John\"}"));
    }

    [Test]
    public async Task JsonMode_WithNoFences_ReturnsContentAsIs()
    {
        var handler = new MockHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonResponseJson, System.Text.Encoding.UTF8, "application/json")
            }));

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.anthropic.com/v1/") };
        var client = new AnthropicChatCompletionClient(httpClient, new AnthropicClientOptions());

        var request = new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "Give me a person")],
            Model: "claude-sonnet-4-5-20250929");

        var jsonOptions = new JsonOutputOptions(Mode: JsonOutputMode.JsonMode);

        var response = await client.GetChatCompletionWithJsonOutputAsync(request, jsonOptions);

        Assert.That(response.Content, Is.EqualTo("{\"name\":\"John\",\"age\":30}"));
    }

    #endregion

    #region Feature Discovery Tests

    [Test]
    public void Features_GetJsonOutputFeature_ReturnsSelf()
    {
        using var httpClient = new HttpClient { BaseAddress = new Uri("https://api.anthropic.com/v1/") };
        var client = new AnthropicChatCompletionClient(httpClient, new AnthropicClientOptions());

        var feature = client.Features.Get<IJsonOutputFeature>();

        Assert.Multiple(() =>
        {
            Assert.That(feature, Is.Not.Null);
            Assert.That(feature, Is.SameAs(client));
        });
    }

    #endregion
}
