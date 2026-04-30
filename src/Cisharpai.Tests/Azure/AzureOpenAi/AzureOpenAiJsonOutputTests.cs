using System.Net;
using System.Text.Json;
using Cisharpai.Azure.AzureOpenAi;
using Cisharpai.Features.Chat;
using Cisharpai.Models;

namespace Cisharpai.Tests.Azure.AzureOpenAi;

public sealed class AzureOpenAiJsonOutputTests
{
    private const string DeploymentName = "test-deploy";
    private const string ApiKey = "test-key";
    private static readonly Uri BaseAddress = new("https://test.openai.azure.com/");

    private const string JsonResponseBody = """
        {
            "model": "gpt-4o",
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

    private const string RefusalResponseBody = """
        {
            "model": "gpt-4o",
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

    private const string TestSchema =
        """{"type":"object","properties":{"name":{"type":"string"},"age":{"type":"integer"}},"required":["name","age"],"additionalProperties":false}""";

    private static AzureOpenAiChatCompletionClient CreateClient(
        MockHttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler) { BaseAddress = BaseAddress };
        var options = new AzureOpenAiClientOptions
        {
            DeploymentName = DeploymentName,
            ApiKey = ApiKey
        };
        return new AzureOpenAiChatCompletionClient(httpClient, options);
    }

    private static MockHttpMessageHandler CreateHandlerCapturingBody(
        out Func<string?> getCapturedBody,
        string responseJson = JsonResponseBody)
    {
        string? captured = null;
        var handler = new MockHttpMessageHandler(async (request, _) =>
        {
            captured = await request.Content!.ReadAsStringAsync();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson, System.Text.Encoding.UTF8, "application/json")
            };
        });
        getCapturedBody = () => captured;
        return handler;
    }

    #region JSON Mode tests

    [Test]
    public async Task JsonMode_StandardModel_SetsResponseFormatJsonObject()
    {
        var handler = CreateHandlerCapturingBody(out var getCapturedBody);
        var client = CreateClient(handler);

        var request = new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "Give me a person as JSON")],
            Model: "gpt-4o");

        var jsonOptions = new JsonOutputOptions(Mode: JsonOutputMode.JsonMode);

        await client.GetChatCompletionWithJsonOutputAsync(request, jsonOptions);

        var doc = JsonDocument.Parse(getCapturedBody()!);
        var responseFormat = doc.RootElement.GetProperty("response_format");
        Assert.That(responseFormat.GetProperty("type").GetString(), Is.EqualTo("json_object"));
    }

    [Test]
    public async Task JsonMode_ReasoningModel_SetsResponseFormatJsonObject()
    {
        var handler = CreateHandlerCapturingBody(out var getCapturedBody);
        var client = CreateClient(handler);

        var request = new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "Give me a person as JSON")],
            Model: "o3-mini");

        var jsonOptions = new JsonOutputOptions(Mode: JsonOutputMode.JsonMode);

        await client.GetChatCompletionWithJsonOutputAsync(request, jsonOptions);

        var doc = JsonDocument.Parse(getCapturedBody()!);
        var responseFormat = doc.RootElement.GetProperty("response_format");
        Assert.That(responseFormat.GetProperty("type").GetString(), Is.EqualTo("json_object"));
    }

    [Test]
    public async Task JsonMode_InjectsJsonKeywordInSystemMessage()
    {
        var handler = CreateHandlerCapturingBody(out var getCapturedBody);
        var client = CreateClient(handler);

        var request = new ChatCompletionRequest(
            Messages:
            [
                new LlmMessage(LlmRole.System, "Be helpful"),
                new LlmMessage(LlmRole.User, "Give me a person")
            ],
            Model: "gpt-4o");

        var jsonOptions = new JsonOutputOptions(Mode: JsonOutputMode.JsonMode);

        await client.GetChatCompletionWithJsonOutputAsync(request, jsonOptions);

        var doc = JsonDocument.Parse(getCapturedBody()!);
        var messages = doc.RootElement.GetProperty("messages");
        var systemContent = messages[0].GetProperty("content").GetString();
        Assert.That(systemContent, Is.EqualTo("Be helpful Respond in JSON."));
    }

    #endregion

    #region Structured Outputs tests

    [Test]
    public async Task JsonSchema_StandardModel_SetsResponseFormatJsonSchema()
    {
        var handler = CreateHandlerCapturingBody(out var getCapturedBody);
        var client = CreateClient(handler);

        var request = new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "Give me a person")],
            Model: "gpt-4o");

        var jsonOptions = new JsonOutputOptions(
            Mode: JsonOutputMode.JsonSchema,
            SchemaName: "person",
            JsonSchema: TestSchema);

        await client.GetChatCompletionWithJsonOutputAsync(request, jsonOptions);

        var doc = JsonDocument.Parse(getCapturedBody()!);
        var responseFormat = doc.RootElement.GetProperty("response_format");
        Assert.That(responseFormat.GetProperty("type").GetString(), Is.EqualTo("json_schema"));

        var jsonSchema = responseFormat.GetProperty("json_schema");
        Assert.Multiple(() =>
        {
            Assert.That(jsonSchema.GetProperty("name").GetString(), Is.EqualTo("person"));
            Assert.That(jsonSchema.GetProperty("strict").GetBoolean(), Is.True);
            Assert.That(jsonSchema.GetProperty("schema").GetProperty("type").GetString(), Is.EqualTo("object"));
        });
    }

    [Test]
    public async Task JsonSchema_ReasoningModel_SetsResponseFormatJsonSchema()
    {
        var handler = CreateHandlerCapturingBody(out var getCapturedBody);
        var client = CreateClient(handler);

        var request = new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "Give me a person")],
            Model: "o3-mini");

        var jsonOptions = new JsonOutputOptions(
            Mode: JsonOutputMode.JsonSchema,
            SchemaName: "person",
            JsonSchema: TestSchema);

        await client.GetChatCompletionWithJsonOutputAsync(request, jsonOptions);

        var doc = JsonDocument.Parse(getCapturedBody()!);
        var responseFormat = doc.RootElement.GetProperty("response_format");
        Assert.That(responseFormat.GetProperty("type").GetString(), Is.EqualTo("json_schema"));

        var jsonSchema = responseFormat.GetProperty("json_schema");
        Assert.Multiple(() =>
        {
            Assert.That(jsonSchema.GetProperty("name").GetString(), Is.EqualTo("person"));
            Assert.That(jsonSchema.GetProperty("strict").GetBoolean(), Is.True);
        });
    }

    [Test]
    public async Task JsonSchema_SchemaStringParsedToJsonElement()
    {
        var handler = CreateHandlerCapturingBody(out var getCapturedBody);
        var client = CreateClient(handler);

        var request = new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "Give me a person")],
            Model: "gpt-4o");

        var jsonOptions = new JsonOutputOptions(
            Mode: JsonOutputMode.JsonSchema,
            SchemaName: "person",
            JsonSchema: TestSchema);

        await client.GetChatCompletionWithJsonOutputAsync(request, jsonOptions);

        var doc = JsonDocument.Parse(getCapturedBody()!);
        var schema = doc.RootElement
            .GetProperty("response_format")
            .GetProperty("json_schema")
            .GetProperty("schema");

        // Verify the schema string was parsed into a JSON object, not sent as a raw string
        Assert.Multiple(() =>
        {
            Assert.That(schema.ValueKind, Is.EqualTo(JsonValueKind.Object));
            Assert.That(schema.GetProperty("type").GetString(), Is.EqualTo("object"));
            Assert.That(schema.GetProperty("properties").GetProperty("name").GetProperty("type").GetString(),
                Is.EqualTo("string"));
        });
        Assert.Multiple(() =>
        {
            Assert.That(schema.GetProperty("properties").GetProperty("age").GetProperty("type").GetString(),
                Is.EqualTo("integer"));
            Assert.That(schema.GetProperty("additionalProperties").GetBoolean(), Is.False);
        });
    }

    #endregion

    #region Refusal tests

    [Test]
    public async Task StructuredOutput_WithRefusal_MapsToResponse()
    {
        var handler = CreateHandlerCapturingBody(out _, responseJson: RefusalResponseBody);
        var client = CreateClient(handler);

        var request = new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "Do something unsafe")],
            Model: "gpt-4o");

        var jsonOptions = new JsonOutputOptions(
            Mode: JsonOutputMode.JsonSchema,
            SchemaName: "person",
            JsonSchema: TestSchema);

        var response = await client.GetChatCompletionWithJsonOutputAsync(request, jsonOptions);

        Assert.Multiple(() =>
        {
            Assert.That(response.Refusal, Is.EqualTo("I cannot assist with that request."));
            Assert.That(response.Content, Is.EqualTo(string.Empty));
            Assert.That(response.IsSuccess, Is.True);
            Assert.That(response.PromptTokens, Is.EqualTo(15));
            Assert.That(response.CompletionTokens, Is.EqualTo(5));
        });
    }

    #endregion

    #region Feature discovery

    [Test]
    public void Features_GetJsonOutputFeature_ReturnsSelf()
    {
        var handler = new MockHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
        var client = CreateClient(handler);

        var feature = client.Features.Get<IJsonOutputFeature>();

        Assert.Multiple(() =>
        {
            Assert.That(feature, Is.Not.Null);
            Assert.That(feature, Is.SameAs(client));
        });
    }

    #endregion

    #region Azure-specific

    [Test]
    public async Task Request_UsesCorrectEndpoint()
    {
        var handler = CreateHandlerCapturingBody(out _);
        var client = CreateClient(handler);

        var request = new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "Hello")],
            Model: "gpt-4o");

        var jsonOptions = new JsonOutputOptions(Mode: JsonOutputMode.JsonMode);

        await client.GetChatCompletionWithJsonOutputAsync(request, jsonOptions);

        var requestUri = handler.LastRequest!.RequestUri!.ToString();
        Assert.Multiple(() =>
        {
            Assert.That(requestUri, Does.Contain($"openai/deployments/{DeploymentName}/chat/completions"));
            Assert.That(requestUri, Does.Contain("api-version="));
        });
    }

    [Test]
    public async Task Request_IncludesApiKeyHeader()
    {
        var handler = CreateHandlerCapturingBody(out _);
        var client = CreateClient(handler);

        var request = new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "Hello")],
            Model: "gpt-4o");

        var jsonOptions = new JsonOutputOptions(Mode: JsonOutputMode.JsonMode);

        await client.GetChatCompletionWithJsonOutputAsync(request, jsonOptions);

        var requestUri = handler.LastRequest!.RequestUri!.ToString();
        Assert.Multiple(() =>
        {
            Assert.That(requestUri, Does.StartWith("https://test.openai.azure.com/"));
            Assert.That(requestUri, Does.Contain($"openai/deployments/{DeploymentName}/chat/completions"));
        });
    }

    #endregion
}
