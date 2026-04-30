using System.Net;
using System.Text.Json;
using Cisharpai.Features.Chat;
using Cisharpai.Models;
using Cisharpai.Cohere;

namespace Cisharpai.Tests.Cohere;

public sealed class CohereToolCallingTests
{
    private static readonly JsonElement WeatherParameters = JsonDocument.Parse(
        """{"type":"object","properties":{"city":{"type":"string"}},"required":["city"]}""")
        .RootElement.Clone();

    #region Response Fixtures

    private const string ToolCallResponseJson = """
        {
            "id": "abc-123",
            "finish_reason": "TOOL_CALL",
            "message": {
                "role": "assistant",
                "content": [],
                "tool_calls": [
                    {
                        "id": "call_abc123",
                        "type": "function",
                        "function": {
                            "name": "get_weather",
                            "arguments": "{\"city\":\"Paris\"}"
                        }
                    }
                ]
            },
            "usage": {
                "billed_units": {
                    "input_tokens": 50,
                    "output_tokens": 20
                },
                "tokens": {
                    "input_tokens": 50,
                    "output_tokens": 20
                }
            }
        }
        """;

    private const string TextResponseJson = """
        {
            "id": "abc-456",
            "finish_reason": "COMPLETE",
            "message": {
                "role": "assistant",
                "content": [
                    {
                        "type": "text",
                        "text": "The weather in Paris is sunny."
                    }
                ]
            },
            "usage": {
                "billed_units": {
                    "input_tokens": 80,
                    "output_tokens": 10
                },
                "tokens": {
                    "input_tokens": 80,
                    "output_tokens": 10
                }
            }
        }
        """;

    #endregion

    private static ChatCompletionRequest CreateRequest() =>
        new(Messages: [new LlmMessage(LlmRole.User, "What's the weather in Paris?")],
            Model: "command-a-03-2025");

    private static ToolCallingOptions CreateToolOptions(ToolChoice? choice = null) =>
        new(Tools: [new ToolDefinition("get_weather", "Get current weather", WeatherParameters)],
            ToolChoice: choice);

    #region Request Serialization Tests

    [Test]
    public async Task ToolCalling_SendsToolsArray_WithSnakeCase()
    {
        string? capturedBody = null;
        var handler = new MockHttpMessageHandler(async (request, _) =>
        {
            capturedBody = await request.Content!.ReadAsStringAsync();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(ToolCallResponseJson, System.Text.Encoding.UTF8, "application/json")
            };
        });

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.cohere.com/v2/") };
        var client = new CohereChatCompletionClient(httpClient, new CohereClientOptions());

        await client.GetChatCompletionWithToolsAsync(CreateRequest(), CreateToolOptions());

        Assert.That(capturedBody, Is.Not.Null);
        var doc = JsonDocument.Parse(capturedBody!);
        var tools = doc.RootElement.GetProperty("tools");
        Assert.That(tools.GetArrayLength(), Is.EqualTo(1));

        var tool = tools[0];
        Assert.That(tool.GetProperty("type").GetString(), Is.EqualTo("function"));

        var function = tool.GetProperty("function");
        Assert.Multiple(() =>
        {
            Assert.That(function.GetProperty("name").GetString(), Is.EqualTo("get_weather"));
            Assert.That(function.GetProperty("description").GetString(), Is.EqualTo("Get current weather"));
        });
    }

    [Test]
    public async Task ToolCalling_ToolChoiceAuto_SendsUppercaseAUTO()
    {
        string? capturedBody = null;
        var handler = new MockHttpMessageHandler(async (request, _) =>
        {
            capturedBody = await request.Content!.ReadAsStringAsync();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(TextResponseJson, System.Text.Encoding.UTF8, "application/json")
            };
        });

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.cohere.com/v2/") };
        var client = new CohereChatCompletionClient(httpClient, new CohereClientOptions());

        await client.GetChatCompletionWithToolsAsync(CreateRequest(), CreateToolOptions(ToolChoice.Auto));

        var doc = JsonDocument.Parse(capturedBody!);
        Assert.That(doc.RootElement.GetProperty("tool_choice").GetString(), Is.EqualTo("AUTO"));
    }

    [Test]
    public async Task ToolCalling_ToolChoiceNone_SendsUppercaseNONE()
    {
        string? capturedBody = null;
        var handler = new MockHttpMessageHandler(async (request, _) =>
        {
            capturedBody = await request.Content!.ReadAsStringAsync();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(TextResponseJson, System.Text.Encoding.UTF8, "application/json")
            };
        });

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.cohere.com/v2/") };
        var client = new CohereChatCompletionClient(httpClient, new CohereClientOptions());

        await client.GetChatCompletionWithToolsAsync(CreateRequest(), CreateToolOptions(ToolChoice.None));

        var doc = JsonDocument.Parse(capturedBody!);
        Assert.That(doc.RootElement.GetProperty("tool_choice").GetString(), Is.EqualTo("NONE"));
    }

    [Test]
    public async Task ToolCalling_ToolChoiceRequired_SendsUppercaseREQUIRED()
    {
        string? capturedBody = null;
        var handler = new MockHttpMessageHandler(async (request, _) =>
        {
            capturedBody = await request.Content!.ReadAsStringAsync();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(ToolCallResponseJson, System.Text.Encoding.UTF8, "application/json")
            };
        });

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.cohere.com/v2/") };
        var client = new CohereChatCompletionClient(httpClient, new CohereClientOptions());

        await client.GetChatCompletionWithToolsAsync(CreateRequest(), CreateToolOptions(ToolChoice.Required));

        var doc = JsonDocument.Parse(capturedBody!);
        Assert.That(doc.RootElement.GetProperty("tool_choice").GetString(), Is.EqualTo("REQUIRED"));
    }

    [Test]
    public async Task ToolCalling_ToolChoiceSpecific_DegradesTo_REQUIRED()
    {
        string? capturedBody = null;
        var handler = new MockHttpMessageHandler(async (request, _) =>
        {
            capturedBody = await request.Content!.ReadAsStringAsync();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(ToolCallResponseJson, System.Text.Encoding.UTF8, "application/json")
            };
        });

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.cohere.com/v2/") };
        var client = new CohereChatCompletionClient(httpClient, new CohereClientOptions());

        await client.GetChatCompletionWithToolsAsync(CreateRequest(), CreateToolOptions(ToolChoice.Specific("get_weather")));

        var doc = JsonDocument.Parse(capturedBody!);
        // Cohere doesn't support Specific; degrades to REQUIRED
        Assert.That(doc.RootElement.GetProperty("tool_choice").GetString(), Is.EqualTo("REQUIRED"));
    }

    [Test]
    public async Task ToolCalling_StrictToolsTrue_WhenAllToolsStrict()
    {
        string? capturedBody = null;
        var handler = new MockHttpMessageHandler(async (request, _) =>
        {
            capturedBody = await request.Content!.ReadAsStringAsync();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(ToolCallResponseJson, System.Text.Encoding.UTF8, "application/json")
            };
        });

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.cohere.com/v2/") };
        var client = new CohereChatCompletionClient(httpClient, new CohereClientOptions());

        // Default Strict=true
        await client.GetChatCompletionWithToolsAsync(CreateRequest(), CreateToolOptions());

        var doc = JsonDocument.Parse(capturedBody!);
        Assert.That(doc.RootElement.GetProperty("strict_tools").GetBoolean(), Is.True);
    }

    [Test]
    public async Task ToolCalling_StrictToolsOmitted_WhenNotAllToolsStrict()
    {
        string? capturedBody = null;
        var handler = new MockHttpMessageHandler(async (request, _) =>
        {
            capturedBody = await request.Content!.ReadAsStringAsync();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(ToolCallResponseJson, System.Text.Encoding.UTF8, "application/json")
            };
        });

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.cohere.com/v2/") };
        var client = new CohereChatCompletionClient(httpClient, new CohereClientOptions());

        var toolOptions = new ToolCallingOptions(
            Tools: [new ToolDefinition("get_weather", "Get weather", WeatherParameters, Strict: false)]);

        await client.GetChatCompletionWithToolsAsync(CreateRequest(), toolOptions);

        var doc = JsonDocument.Parse(capturedBody!);
        Assert.That(doc.RootElement.TryGetProperty("strict_tools", out _), Is.False);
    }

    #endregion

    #region Response Deserialization Tests

    [Test]
    public async Task ToolCalling_ResponseWithToolCalls_MapsToToolCallingResponse()
    {
        var handler = new MockHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(ToolCallResponseJson, System.Text.Encoding.UTF8, "application/json")
            }));

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.cohere.com/v2/") };
        var client = new CohereChatCompletionClient(httpClient, new CohereClientOptions());

        var response = await client.GetChatCompletionWithToolsAsync(CreateRequest(), CreateToolOptions());

        Assert.Multiple(() =>
        {
            Assert.That(response.IsSuccess, Is.True);
            Assert.That(response.ToolCalls, Is.Not.Null);
            Assert.That(response.ToolCalls!, Has.Count.EqualTo(1));
        });

        var toolCall = response.ToolCalls[0];
        Assert.Multiple(() =>
        {
            Assert.That(toolCall.Id, Is.EqualTo("call_abc123"));
            Assert.That(toolCall.FunctionName, Is.EqualTo("get_weather"));
            Assert.That(toolCall.Arguments.GetProperty("city").GetString(), Is.EqualTo("Paris"));
        });
    }

    [Test]
    public async Task ToolCalling_TextResponse_ToolCallsNull()
    {
        var handler = new MockHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(TextResponseJson, System.Text.Encoding.UTF8, "application/json")
            }));

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.cohere.com/v2/") };
        var client = new CohereChatCompletionClient(httpClient, new CohereClientOptions());

        var response = await client.GetChatCompletionWithToolsAsync(CreateRequest(), CreateToolOptions());

        Assert.Multiple(() =>
        {
            Assert.That(response.IsSuccess, Is.True);
            Assert.That(response.ToolCalls, Is.Null);
            Assert.That(response.Content, Is.EqualTo("The weather in Paris is sunny."));
        });
    }

    #endregion

    #region Multi-turn Tool Message Tests

    [Test]
    public async Task ToolCalling_ToolResultMessage_SerializesAsToolRole_WithSnakeCase()
    {
        string? capturedBody = null;
        var handler = new MockHttpMessageHandler(async (request, _) =>
        {
            capturedBody = await request.Content!.ReadAsStringAsync();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(TextResponseJson, System.Text.Encoding.UTF8, "application/json")
            };
        });

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.cohere.com/v2/") };
        var client = new CohereChatCompletionClient(httpClient, new CohereClientOptions());

        var args = JsonDocument.Parse("""{"city":"Paris"}""").RootElement.Clone();
        var messages = new List<LlmMessage>
        {
            new(LlmRole.User, "What's the weather?"),
            new(LlmRole.Assistant, "", ToolCalls: [new ToolCall("call_1", "get_weather", args)]),
            new(LlmRole.Tool, "Sunny, 22C", ToolCallId: "call_1")
        };

        var request = new ChatCompletionRequest(Messages: messages, Model: "command-a-03-2025");
        await client.GetChatCompletionWithToolsAsync(request, CreateToolOptions());

        var doc = JsonDocument.Parse(capturedBody!);
        var msgs = doc.RootElement.GetProperty("messages");

        // Tool result message (snake_case naming)
        var toolMsg = msgs[2];
        Assert.Multiple(() =>
        {
            Assert.That(toolMsg.GetProperty("role").GetString(), Is.EqualTo("tool"));
            Assert.That(toolMsg.GetProperty("content").GetString(), Is.EqualTo("Sunny, 22C"));
            Assert.That(toolMsg.GetProperty("tool_call_id").GetString(), Is.EqualTo("call_1"));
        });
    }

    [Test]
    public async Task ToolCalling_AssistantWithToolCalls_SerializesToolCallsArray_WithSnakeCase()
    {
        string? capturedBody = null;
        var handler = new MockHttpMessageHandler(async (request, _) =>
        {
            capturedBody = await request.Content!.ReadAsStringAsync();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(TextResponseJson, System.Text.Encoding.UTF8, "application/json")
            };
        });

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.cohere.com/v2/") };
        var client = new CohereChatCompletionClient(httpClient, new CohereClientOptions());

        var args = JsonDocument.Parse("""{"city":"Paris"}""").RootElement.Clone();
        var messages = new List<LlmMessage>
        {
            new(LlmRole.User, "What's the weather?"),
            new(LlmRole.Assistant, "", ToolCalls: [new ToolCall("call_1", "get_weather", args)]),
            new(LlmRole.Tool, "Sunny, 22C", ToolCallId: "call_1")
        };

        var request = new ChatCompletionRequest(Messages: messages, Model: "command-a-03-2025");
        await client.GetChatCompletionWithToolsAsync(request, CreateToolOptions());

        var doc = JsonDocument.Parse(capturedBody!);
        var msgs = doc.RootElement.GetProperty("messages");

        // Assistant message with tool_calls (snake_case)
        var assistantMsg = msgs[1];
        Assert.That(assistantMsg.GetProperty("role").GetString(), Is.EqualTo("assistant"));
        var toolCalls = assistantMsg.GetProperty("tool_calls");
        Assert.Multiple(() =>
        {
            Assert.That(toolCalls.GetArrayLength(), Is.EqualTo(1));
            Assert.That(toolCalls[0].GetProperty("id").GetString(), Is.EqualTo("call_1"));
            Assert.That(toolCalls[0].GetProperty("function").GetProperty("name").GetString(), Is.EqualTo("get_weather"));
        });
    }

    #endregion

    #region Error Handling Tests

    [Test]
    public async Task ToolCalling_HttpError_ReturnsErrorResponse()
    {
        var handler = new MockHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)
            {
                Content = new StringContent("""{"error":"server error"}""", System.Text.Encoding.UTF8, "application/json")
            }));

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.cohere.com/v2/") };
        var client = new CohereChatCompletionClient(httpClient, new CohereClientOptions());

        var response = await client.GetChatCompletionWithToolsAsync(CreateRequest(), CreateToolOptions());

        Assert.Multiple(() =>
        {
            Assert.That(response.IsSuccess, Is.False);
            Assert.That(response.ErrorMessage, Is.Not.Null.And.Not.Empty);
        });
    }

    #endregion

    #region Feature Discovery Tests

    [Test]
    public void CohereChatCompletionClient_ExposesToolCallingFeature()
    {
        using var httpClient = new HttpClient { BaseAddress = new Uri("https://api.cohere.com/v2/") };
        var client = new CohereChatCompletionClient(httpClient, new CohereClientOptions());

        var feature = client.Features.Get<IToolCallingFeature>();

        Assert.Multiple(() =>
        {
            Assert.That(feature, Is.Not.Null);
            Assert.That(feature, Is.SameAs(client));
        });
    }

    #endregion
}
