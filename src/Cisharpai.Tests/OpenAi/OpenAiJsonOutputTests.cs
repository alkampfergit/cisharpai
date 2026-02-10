using System.Net;
using System.Text.Json;
using Cisharpai.Features.Chat;
using Cisharpai.Models;
using Cisharpai.OpenAi;

namespace Cisharpai.Tests.OpenAi;

public sealed class OpenAiJsonOutputTests
{
    private const string JsonSchemaString =
        """{"type":"object","properties":{"name":{"type":"string"},"age":{"type":"integer"}},"required":["name","age"],"additionalProperties":false}""";

    #region Response Fixtures

    private const string ChatCompletionsJsonResponseJson = """
        {
            "model": "gpt-4o-2024-08-06",
            "choices": [
                {
                    "message": {
                        "role": "assistant",
                        "content": "{\"name\":\"John\",\"age\":30}"
                    }
                }
            ],
            "usage": {
                "prompt_tokens": 15,
                "completion_tokens": 10
            }
        }
        """;

    private const string ChatCompletionsRefusalResponseJson = """
        {
            "model": "gpt-4o-2024-08-06",
            "choices": [
                {
                    "message": {
                        "role": "assistant",
                        "content": null,
                        "refusal": "I cannot assist with that request."
                    }
                }
            ],
            "usage": {
                "prompt_tokens": 15,
                "completion_tokens": 5
            }
        }
        """;

    private const string ResponsesApiJsonResponseJson = """
        {
            "id": "resp_json_001",
            "model": "gpt-5-20250801",
            "status": "completed",
            "output": [
                {
                    "type": "message",
                    "role": "assistant",
                    "content": [
                        {
                            "type": "output_text",
                            "text": "{\"name\":\"John\",\"age\":30}"
                        }
                    ]
                }
            ],
            "usage": {
                "input_tokens": 15,
                "output_tokens": 10
            }
        }
        """;

    private const string ResponsesApiRefusalResponseJson = """
        {
            "id": "resp_refusal_001",
            "model": "gpt-5-20250801",
            "status": "completed",
            "output": [
                {
                    "type": "message",
                    "role": "assistant",
                    "content": [
                        {
                            "type": "refusal",
                            "refusal": "I cannot assist with that request."
                        }
                    ]
                }
            ],
            "usage": {
                "input_tokens": 15,
                "output_tokens": 5
            }
        }
        """;

    #endregion

    #region JSON Mode Tests

    [Test]
    public async Task JsonMode_LegacyModel_SetsResponseFormatJsonObject()
    {
        string? capturedBody = null;
        var handler = new MockHttpMessageHandler(async (request, _) =>
        {
            capturedBody = await request.Content!.ReadAsStringAsync();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(ChatCompletionsJsonResponseJson, System.Text.Encoding.UTF8, "application/json")
            };
        });

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.openai.com/v1/") };
        var client = new OpenAiChatCompletionClient(httpClient, new OpenAiClientOptions());

        var request = new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "Give me a person")],
            Model: "gpt-4o-2024-08-06");

        var jsonOptions = new JsonOutputOptions(Mode: JsonOutputMode.JsonMode);

        await client.GetChatCompletionWithJsonOutputAsync(request, jsonOptions);

        Assert.That(capturedBody, Is.Not.Null);
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
                Content = new StringContent(ChatCompletionsJsonResponseJson, System.Text.Encoding.UTF8, "application/json")
            };
        });

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.openai.com/v1/") };
        var client = new OpenAiChatCompletionClient(httpClient, new OpenAiClientOptions());

        var request = new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "Give me a person")],
            Model: "o3-mini");

        var jsonOptions = new JsonOutputOptions(Mode: JsonOutputMode.JsonMode);

        await client.GetChatCompletionWithJsonOutputAsync(request, jsonOptions);

        Assert.That(capturedBody, Is.Not.Null);
        var doc = JsonDocument.Parse(capturedBody!);
        var responseFormat = doc.RootElement.GetProperty("response_format");
        Assert.That(responseFormat.GetProperty("type").GetString(), Is.EqualTo("json_object"));
    }

    [Test]
    public async Task JsonMode_Gpt5Model_SetsTextFormatJsonObject()
    {
        string? capturedBody = null;
        var handler = new MockHttpMessageHandler(async (request, _) =>
        {
            capturedBody = await request.Content!.ReadAsStringAsync();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(ResponsesApiJsonResponseJson, System.Text.Encoding.UTF8, "application/json")
            };
        });

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.openai.com/v1/") };
        var client = new OpenAiChatCompletionClient(httpClient, new OpenAiClientOptions());

        var request = new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "Give me a person")],
            Model: "gpt-5");

        var jsonOptions = new JsonOutputOptions(Mode: JsonOutputMode.JsonMode);

        await client.GetChatCompletionWithJsonOutputAsync(request, jsonOptions);

        Assert.That(capturedBody, Is.Not.Null);
        var doc = JsonDocument.Parse(capturedBody!);
        var text = doc.RootElement.GetProperty("text");
        var format = text.GetProperty("format");
        Assert.That(format.GetProperty("type").GetString(), Is.EqualTo("json_object"));
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
                Content = new StringContent(ChatCompletionsJsonResponseJson, System.Text.Encoding.UTF8, "application/json")
            };
        });

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.openai.com/v1/") };
        var client = new OpenAiChatCompletionClient(httpClient, new OpenAiClientOptions());

        var request = new ChatCompletionRequest(
            Messages:
            [
                new LlmMessage(LlmRole.System, "Be helpful"),
                new LlmMessage(LlmRole.User, "Give me a person")
            ],
            Model: "gpt-4o-2024-08-06");

        var jsonOptions = new JsonOutputOptions(Mode: JsonOutputMode.JsonMode);

        await client.GetChatCompletionWithJsonOutputAsync(request, jsonOptions);

        Assert.That(capturedBody, Is.Not.Null);
        var doc = JsonDocument.Parse(capturedBody!);
        var messages = doc.RootElement.GetProperty("messages");
        var systemContent = messages[0].GetProperty("content").GetString();
        Assert.That(systemContent, Is.EqualTo("Be helpful Respond in JSON."));
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
                Content = new StringContent(ChatCompletionsJsonResponseJson, System.Text.Encoding.UTF8, "application/json")
            };
        });

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.openai.com/v1/") };
        var client = new OpenAiChatCompletionClient(httpClient, new OpenAiClientOptions());

        var request = new ChatCompletionRequest(
            Messages:
            [
                new LlmMessage(LlmRole.System, "Return JSON data"),
                new LlmMessage(LlmRole.User, "Give me a person")
            ],
            Model: "gpt-4o-2024-08-06");

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
                Content = new StringContent(ChatCompletionsJsonResponseJson, System.Text.Encoding.UTF8, "application/json")
            };
        });

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.openai.com/v1/") };
        var client = new OpenAiChatCompletionClient(httpClient, new OpenAiClientOptions());

        var request = new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "Give me a person")],
            Model: "gpt-4o-2024-08-06");

        var jsonOptions = new JsonOutputOptions(Mode: JsonOutputMode.JsonMode);

        await client.GetChatCompletionWithJsonOutputAsync(request, jsonOptions);

        Assert.That(capturedBody, Is.Not.Null);
        var doc = JsonDocument.Parse(capturedBody!);
        var messages = doc.RootElement.GetProperty("messages");
        Assert.That(messages.GetArrayLength(), Is.EqualTo(2));
        Assert.That(messages[0].GetProperty("role").GetString(), Is.EqualTo("system"));
        Assert.That(messages[0].GetProperty("content").GetString(), Is.EqualTo("Respond in JSON."));
        Assert.That(messages[1].GetProperty("role").GetString(), Is.EqualTo("user"));
    }

    #endregion

    #region Structured Outputs (JSON Schema) Tests

    [Test]
    public async Task JsonSchema_LegacyModel_SetsResponseFormatJsonSchema()
    {
        string? capturedBody = null;
        var handler = new MockHttpMessageHandler(async (request, _) =>
        {
            capturedBody = await request.Content!.ReadAsStringAsync();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(ChatCompletionsJsonResponseJson, System.Text.Encoding.UTF8, "application/json")
            };
        });

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.openai.com/v1/") };
        var client = new OpenAiChatCompletionClient(httpClient, new OpenAiClientOptions());

        var request = new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "Give me a person")],
            Model: "gpt-4o-2024-08-06");

        var jsonOptions = new JsonOutputOptions(
            Mode: JsonOutputMode.JsonSchema,
            SchemaName: "person",
            JsonSchema: JsonSchemaString);

        await client.GetChatCompletionWithJsonOutputAsync(request, jsonOptions);

        Assert.That(capturedBody, Is.Not.Null);
        var doc = JsonDocument.Parse(capturedBody!);
        var responseFormat = doc.RootElement.GetProperty("response_format");
        Assert.That(responseFormat.GetProperty("type").GetString(), Is.EqualTo("json_schema"));

        var jsonSchema = responseFormat.GetProperty("json_schema");
        Assert.That(jsonSchema.GetProperty("name").GetString(), Is.EqualTo("person"));
        Assert.That(jsonSchema.GetProperty("strict").GetBoolean(), Is.True);
        Assert.That(jsonSchema.TryGetProperty("schema", out var schema), Is.True);
        Assert.That(schema.GetProperty("type").GetString(), Is.EqualTo("object"));
    }

    [Test]
    public async Task JsonSchema_ReasoningModel_SetsResponseFormatJsonSchema()
    {
        string? capturedBody = null;
        var handler = new MockHttpMessageHandler(async (request, _) =>
        {
            capturedBody = await request.Content!.ReadAsStringAsync();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(ChatCompletionsJsonResponseJson, System.Text.Encoding.UTF8, "application/json")
            };
        });

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.openai.com/v1/") };
        var client = new OpenAiChatCompletionClient(httpClient, new OpenAiClientOptions());

        var request = new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "Give me a person")],
            Model: "o3-mini");

        var jsonOptions = new JsonOutputOptions(
            Mode: JsonOutputMode.JsonSchema,
            SchemaName: "person",
            JsonSchema: JsonSchemaString);

        await client.GetChatCompletionWithJsonOutputAsync(request, jsonOptions);

        Assert.That(capturedBody, Is.Not.Null);
        var doc = JsonDocument.Parse(capturedBody!);
        var responseFormat = doc.RootElement.GetProperty("response_format");
        Assert.That(responseFormat.GetProperty("type").GetString(), Is.EqualTo("json_schema"));

        var jsonSchema = responseFormat.GetProperty("json_schema");
        Assert.That(jsonSchema.GetProperty("name").GetString(), Is.EqualTo("person"));
        Assert.That(jsonSchema.GetProperty("strict").GetBoolean(), Is.True);
    }

    [Test]
    public async Task JsonSchema_Gpt5Model_SetsTextFormatJsonSchema()
    {
        string? capturedBody = null;
        var handler = new MockHttpMessageHandler(async (request, _) =>
        {
            capturedBody = await request.Content!.ReadAsStringAsync();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(ResponsesApiJsonResponseJson, System.Text.Encoding.UTF8, "application/json")
            };
        });

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.openai.com/v1/") };
        var client = new OpenAiChatCompletionClient(httpClient, new OpenAiClientOptions());

        var request = new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "Give me a person")],
            Model: "gpt-5");

        var jsonOptions = new JsonOutputOptions(
            Mode: JsonOutputMode.JsonSchema,
            SchemaName: "person",
            JsonSchema: JsonSchemaString);

        await client.GetChatCompletionWithJsonOutputAsync(request, jsonOptions);

        Assert.That(capturedBody, Is.Not.Null);
        var doc = JsonDocument.Parse(capturedBody!);
        var text = doc.RootElement.GetProperty("text");
        var format = text.GetProperty("format");
        Assert.That(format.GetProperty("type").GetString(), Is.EqualTo("json_schema"));
        Assert.That(format.GetProperty("name").GetString(), Is.EqualTo("person"));
        Assert.That(format.GetProperty("strict").GetBoolean(), Is.True);
        Assert.That(format.TryGetProperty("schema", out var schema), Is.True);
        Assert.That(schema.GetProperty("type").GetString(), Is.EqualTo("object"));
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
                Content = new StringContent(ChatCompletionsJsonResponseJson, System.Text.Encoding.UTF8, "application/json")
            };
        });

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.openai.com/v1/") };
        var client = new OpenAiChatCompletionClient(httpClient, new OpenAiClientOptions());

        var request = new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "Give me a person")],
            Model: "gpt-4o-2024-08-06");

        var jsonOptions = new JsonOutputOptions(
            Mode: JsonOutputMode.JsonSchema,
            SchemaName: "person",
            JsonSchema: JsonSchemaString);

        await client.GetChatCompletionWithJsonOutputAsync(request, jsonOptions);

        Assert.That(capturedBody, Is.Not.Null);
        var doc = JsonDocument.Parse(capturedBody!);
        var schema = doc.RootElement
            .GetProperty("response_format")
            .GetProperty("json_schema")
            .GetProperty("schema");

        // Verify it's a JSON object, not a serialized string
        Assert.That(schema.ValueKind, Is.EqualTo(JsonValueKind.Object));
        Assert.That(schema.GetProperty("properties").GetProperty("name").GetProperty("type").GetString(), Is.EqualTo("string"));
        Assert.That(schema.GetProperty("properties").GetProperty("age").GetProperty("type").GetString(), Is.EqualTo("integer"));
        Assert.That(schema.GetProperty("additionalProperties").GetBoolean(), Is.False);
    }

    [Test]
    public async Task JsonSchema_WithDescription_IncludesDescription()
    {
        string? capturedBody = null;
        var handler = new MockHttpMessageHandler(async (request, _) =>
        {
            capturedBody = await request.Content!.ReadAsStringAsync();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(ChatCompletionsJsonResponseJson, System.Text.Encoding.UTF8, "application/json")
            };
        });

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.openai.com/v1/") };
        var client = new OpenAiChatCompletionClient(httpClient, new OpenAiClientOptions());

        var request = new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "Give me a person")],
            Model: "gpt-4o-2024-08-06");

        var jsonOptions = new JsonOutputOptions(
            Mode: JsonOutputMode.JsonSchema,
            SchemaName: "person",
            SchemaDescription: "A person with name and age",
            JsonSchema: JsonSchemaString);

        await client.GetChatCompletionWithJsonOutputAsync(request, jsonOptions);

        Assert.That(capturedBody, Is.Not.Null);
        var doc = JsonDocument.Parse(capturedBody!);
        var jsonSchema = doc.RootElement
            .GetProperty("response_format")
            .GetProperty("json_schema");
        Assert.That(jsonSchema.GetProperty("description").GetString(), Is.EqualTo("A person with name and age"));
    }

    [Test]
    public async Task JsonSchema_StrictFalse_SetsStrictFalse()
    {
        string? capturedBody = null;
        var handler = new MockHttpMessageHandler(async (request, _) =>
        {
            capturedBody = await request.Content!.ReadAsStringAsync();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(ChatCompletionsJsonResponseJson, System.Text.Encoding.UTF8, "application/json")
            };
        });

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.openai.com/v1/") };
        var client = new OpenAiChatCompletionClient(httpClient, new OpenAiClientOptions());

        var request = new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "Give me a person")],
            Model: "gpt-4o-2024-08-06");

        var jsonOptions = new JsonOutputOptions(
            Mode: JsonOutputMode.JsonSchema,
            SchemaName: "person",
            JsonSchema: JsonSchemaString,
            Strict: false);

        await client.GetChatCompletionWithJsonOutputAsync(request, jsonOptions);

        Assert.That(capturedBody, Is.Not.Null);
        var doc = JsonDocument.Parse(capturedBody!);
        var jsonSchema = doc.RootElement
            .GetProperty("response_format")
            .GetProperty("json_schema");
        Assert.That(jsonSchema.GetProperty("strict").GetBoolean(), Is.False);
    }

    #endregion

    #region Refusal Tests

    [Test]
    public async Task StructuredOutput_ChatCompletions_WithRefusal_MapsToResponse()
    {
        var handler = new MockHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(ChatCompletionsRefusalResponseJson, System.Text.Encoding.UTF8, "application/json")
            }));

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.openai.com/v1/") };
        var client = new OpenAiChatCompletionClient(httpClient, new OpenAiClientOptions());

        var request = new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "Give me something dangerous")],
            Model: "gpt-4o-2024-08-06");

        var jsonOptions = new JsonOutputOptions(
            Mode: JsonOutputMode.JsonSchema,
            SchemaName: "person",
            JsonSchema: JsonSchemaString);

        var response = await client.GetChatCompletionWithJsonOutputAsync(request, jsonOptions);

        Assert.That(response.Refusal, Is.EqualTo("I cannot assist with that request."));
        Assert.That(response.Content, Is.Empty);
    }

    [Test]
    public async Task StructuredOutput_ResponsesApi_WithRefusal_MapsToResponse()
    {
        var handler = new MockHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(ResponsesApiRefusalResponseJson, System.Text.Encoding.UTF8, "application/json")
            }));

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.openai.com/v1/") };
        var client = new OpenAiChatCompletionClient(httpClient, new OpenAiClientOptions());

        var request = new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "Give me something dangerous")],
            Model: "gpt-5");

        var jsonOptions = new JsonOutputOptions(
            Mode: JsonOutputMode.JsonSchema,
            SchemaName: "person",
            JsonSchema: JsonSchemaString);

        var response = await client.GetChatCompletionWithJsonOutputAsync(request, jsonOptions);

        Assert.That(response.Refusal, Is.EqualTo("I cannot assist with that request."));
        Assert.That(response.Content, Is.Empty);
    }

    [Test]
    public async Task StructuredOutput_NoRefusal_ContentPopulated()
    {
        var handler = new MockHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(ChatCompletionsJsonResponseJson, System.Text.Encoding.UTF8, "application/json")
            }));

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.openai.com/v1/") };
        var client = new OpenAiChatCompletionClient(httpClient, new OpenAiClientOptions());

        var request = new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "Give me a person")],
            Model: "gpt-4o-2024-08-06");

        var jsonOptions = new JsonOutputOptions(
            Mode: JsonOutputMode.JsonSchema,
            SchemaName: "person",
            JsonSchema: JsonSchemaString);

        var response = await client.GetChatCompletionWithJsonOutputAsync(request, jsonOptions);

        Assert.That(response.Content, Is.EqualTo("{\"name\":\"John\",\"age\":30}"));
        Assert.That(response.Refusal, Is.Null);
        Assert.That(response.IsSuccess, Is.True);
    }

    #endregion

    #region Invalid Schema Tests

    [Test]
    public async Task JsonSchema_WithInvalidJson_ReturnsErrorResponse()
    {
        var handler = new MockHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(ChatCompletionsJsonResponseJson, System.Text.Encoding.UTF8, "application/json")
            }));

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.openai.com/v1/") };
        var client = new OpenAiChatCompletionClient(httpClient, new OpenAiClientOptions());

        var invalidJson = "not valid json {{{";
        var request = new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "Give me a person")],
            Model: "gpt-4o-2024-08-06");

        var jsonOptions = new JsonOutputOptions(
            Mode: JsonOutputMode.JsonSchema,
            SchemaName: "person",
            JsonSchema: invalidJson);

        var response = await client.GetChatCompletionWithJsonOutputAsync(request, jsonOptions);

        Assert.That(response.IsSuccess, Is.False);
        Assert.That(response.ErrorMessage, Does.Contain(invalidJson));
        Assert.That(response.ErrorMessage, Does.Contain("invalid JSON"));
    }

    #endregion

    #region Feature Discovery Tests

    [Test]
    public void Features_GetJsonOutputFeature_ReturnsSelf()
    {
        using var httpClient = new HttpClient { BaseAddress = new Uri("https://api.openai.com/v1/") };
        var client = new OpenAiChatCompletionClient(httpClient, new OpenAiClientOptions());

        var feature = client.Features.Get<IJsonOutputFeature>();

        Assert.That(feature, Is.Not.Null);
        Assert.That(feature, Is.SameAs(client));
    }

    #endregion
}
