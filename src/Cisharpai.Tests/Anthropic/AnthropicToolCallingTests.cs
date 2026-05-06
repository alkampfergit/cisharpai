using System.Net;
using System.Text.Json;
using Cisharpai.Features.Chat;
using Cisharpai.Models;
using Cisharpai.Anthropic;

namespace Cisharpai.Tests.Anthropic;

public sealed class AnthropicToolCallingTests
{
    private static readonly JsonElement WeatherParameters = JsonDocument.Parse(
        """{"type":"object","properties":{"city":{"type":"string"}},"required":["city"]}""")
        .RootElement.Clone();

    #region Response Fixtures

    private const string ToolUseResponseJson = """
        {
            "model": "claude-haiku-4-5-20250814",
            "content": [
                {
                    "type": "tool_use",
                    "id": "toolu_abc123",
                    "name": "get_weather",
                    "input": {"city": "Paris"}
                }
            ],
            "usage": {
                "input_tokens": 50,
                "output_tokens": 20
            },
            "stop_reason": "tool_use"
        }
        """;

    private const string MultipleToolUseResponseJson = """
        {
            "model": "claude-haiku-4-5-20250814",
            "content": [
                {
                    "type": "text",
                    "text": "I'll check the weather for both cities."
                },
                {
                    "type": "tool_use",
                    "id": "toolu_1",
                    "name": "get_weather",
                    "input": {"city": "Paris"}
                },
                {
                    "type": "tool_use",
                    "id": "toolu_2",
                    "name": "get_weather",
                    "input": {"city": "London"}
                }
            ],
            "usage": {
                "input_tokens": 50,
                "output_tokens": 40
            },
            "stop_reason": "tool_use"
        }
        """;

    private const string TextResponseJson = """
        {
            "model": "claude-haiku-4-5-20250814",
            "content": [
                {
                    "type": "text",
                    "text": "The weather in Paris is sunny."
                }
            ],
            "usage": {
                "input_tokens": 80,
                "output_tokens": 10
            },
            "stop_reason": "end_turn"
        }
        """;

    #endregion

    private static ChatCompletionRequest CreateRequest() =>
        new(Messages: [new LlmMessage(LlmRole.User, "What's the weather in Paris?")],
            Model: "claude-haiku-4-5-20250814");

    private static ToolCallingOptions CreateToolOptions(ToolChoice? choice = null) =>
        new(Tools: [new ToolDefinition("get_weather", "Get current weather", WeatherParameters)],
            ToolChoice: choice);

    #region Request Serialization Tests

    [Test]
    public async Task ToolCalling_SendsToolsWithInputSchema()
    {
        string? capturedBody = null;
        var handler = new MockHttpMessageHandler(async (request, _) =>
        {
            capturedBody = await request.Content!.ReadAsStringAsync(CancellationToken.None);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(ToolUseResponseJson, System.Text.Encoding.UTF8, "application/json")
            };
        });

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.anthropic.com/v1/") };
        var client = new AnthropicChatCompletionClient(httpClient, new AnthropicClientOptions());

        await client.GetChatCompletionWithToolsAsync(CreateRequest(), CreateToolOptions());

        Assert.That(capturedBody, Is.Not.Null);
        var doc = JsonDocument.Parse(capturedBody!);
        var tools = doc.RootElement.GetProperty("tools");
        Assert.That(tools.GetArrayLength(), Is.EqualTo(1));

        var tool = tools[0];
        // Anthropic uses top-level name/description/input_schema (no function wrapper)
        Assert.Multiple(() =>
        {
            Assert.That(tool.GetProperty("name").GetString(), Is.EqualTo("get_weather"));
            Assert.That(tool.GetProperty("description").GetString(), Is.EqualTo("Get current weather"));
            Assert.That(tool.GetProperty("input_schema").GetProperty("type").GetString(), Is.EqualTo("object"));

            // Should NOT have a "function" wrapper or "type":"function"
            Assert.That(tool.TryGetProperty("type", out _), Is.False);
            Assert.That(tool.TryGetProperty("function", out _), Is.False);
        });
    }

    [Test]
    public async Task ToolCalling_ToolChoiceAuto_SendsAutoType()
    {
        string? capturedBody = null;
        var handler = new MockHttpMessageHandler(async (request, _) =>
        {
            capturedBody = await request.Content!.ReadAsStringAsync(CancellationToken.None);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(TextResponseJson, System.Text.Encoding.UTF8, "application/json")
            };
        });

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.anthropic.com/v1/") };
        var client = new AnthropicChatCompletionClient(httpClient, new AnthropicClientOptions());

        await client.GetChatCompletionWithToolsAsync(CreateRequest(), CreateToolOptions(ToolChoice.Auto));

        var doc = JsonDocument.Parse(capturedBody!);
        var toolChoice = doc.RootElement.GetProperty("tool_choice");
        Assert.That(toolChoice.GetProperty("type").GetString(), Is.EqualTo("auto"));
    }

    [Test]
    public async Task ToolCalling_ToolChoiceRequired_SendsAnyType()
    {
        string? capturedBody = null;
        var handler = new MockHttpMessageHandler(async (request, _) =>
        {
            capturedBody = await request.Content!.ReadAsStringAsync(CancellationToken.None);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(ToolUseResponseJson, System.Text.Encoding.UTF8, "application/json")
            };
        });

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.anthropic.com/v1/") };
        var client = new AnthropicChatCompletionClient(httpClient, new AnthropicClientOptions());

        await client.GetChatCompletionWithToolsAsync(CreateRequest(), CreateToolOptions(ToolChoice.Required));

        var doc = JsonDocument.Parse(capturedBody!);
        var toolChoice = doc.RootElement.GetProperty("tool_choice");
        Assert.That(toolChoice.GetProperty("type").GetString(), Is.EqualTo("any"));
    }

    [Test]
    public async Task ToolCalling_ToolChoiceSpecific_SendsToolTypeWithName()
    {
        string? capturedBody = null;
        var handler = new MockHttpMessageHandler(async (request, _) =>
        {
            capturedBody = await request.Content!.ReadAsStringAsync(CancellationToken.None);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(ToolUseResponseJson, System.Text.Encoding.UTF8, "application/json")
            };
        });

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.anthropic.com/v1/") };
        var client = new AnthropicChatCompletionClient(httpClient, new AnthropicClientOptions());

        await client.GetChatCompletionWithToolsAsync(CreateRequest(), CreateToolOptions(ToolChoice.Specific("get_weather")));

        var doc = JsonDocument.Parse(capturedBody!);
        var toolChoice = doc.RootElement.GetProperty("tool_choice");
        Assert.Multiple(() =>
        {
            Assert.That(toolChoice.GetProperty("type").GetString(), Is.EqualTo("tool"));
            Assert.That(toolChoice.GetProperty("name").GetString(), Is.EqualTo("get_weather"));
        });
    }

    [Test]
    public async Task ToolCalling_NullToolChoice_OmitsToolChoice()
    {
        string? capturedBody = null;
        var handler = new MockHttpMessageHandler(async (request, _) =>
        {
            capturedBody = await request.Content!.ReadAsStringAsync(CancellationToken.None);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(ToolUseResponseJson, System.Text.Encoding.UTF8, "application/json")
            };
        });

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.anthropic.com/v1/") };
        var client = new AnthropicChatCompletionClient(httpClient, new AnthropicClientOptions());

        await client.GetChatCompletionWithToolsAsync(CreateRequest(), CreateToolOptions(null));

        var doc = JsonDocument.Parse(capturedBody!);
        Assert.That(doc.RootElement.TryGetProperty("tool_choice", out _), Is.False);
    }

    [Test]
    public async Task ToolCalling_ToolChoiceNone_OmitsToolChoice()
    {
        string? capturedBody = null;
        var handler = new MockHttpMessageHandler(async (request, _) =>
        {
            capturedBody = await request.Content!.ReadAsStringAsync(CancellationToken.None);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(TextResponseJson, System.Text.Encoding.UTF8, "application/json")
            };
        });

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.anthropic.com/v1/") };
        var client = new AnthropicChatCompletionClient(httpClient, new AnthropicClientOptions());

        await client.GetChatCompletionWithToolsAsync(CreateRequest(), CreateToolOptions(ToolChoice.None));

        var doc = JsonDocument.Parse(capturedBody!);
        // Anthropic doesn't have a "none" tool_choice type, so we omit it
        Assert.That(doc.RootElement.TryGetProperty("tool_choice", out _), Is.False);
    }

    #endregion

    #region Response Deserialization Tests

    [Test]
    public async Task ToolCalling_ResponseWithToolUse_MapsToToolCallingResponse()
    {
        var handler = new MockHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(ToolUseResponseJson, System.Text.Encoding.UTF8, "application/json")
            }));

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.anthropic.com/v1/") };
        var client = new AnthropicChatCompletionClient(httpClient, new AnthropicClientOptions());

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
            Assert.That(toolCall.Id, Is.EqualTo("toolu_abc123"));
            Assert.That(toolCall.FunctionName, Is.EqualTo("get_weather"));
            // Anthropic args are already JSON objects, not strings
            Assert.That(toolCall.Arguments.GetProperty("city").GetString(), Is.EqualTo("Paris"));
        });
    }

    [Test]
    public async Task ToolCalling_MultipleToolUse_MapsAll()
    {
        var handler = new MockHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(MultipleToolUseResponseJson, System.Text.Encoding.UTF8, "application/json")
            }));

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.anthropic.com/v1/") };
        var client = new AnthropicChatCompletionClient(httpClient, new AnthropicClientOptions());

        var response = await client.GetChatCompletionWithToolsAsync(CreateRequest(), CreateToolOptions());

        Assert.Multiple(() =>
        {
            Assert.That(response.ToolCalls!, Has.Count.EqualTo(2));
            Assert.That(response.ToolCalls![0].Id, Is.EqualTo("toolu_1"));
            Assert.That(response.ToolCalls![1].Id, Is.EqualTo("toolu_2"));
            // Also has text content
            Assert.That(response.Content, Is.EqualTo("I'll check the weather for both cities."));
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

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.anthropic.com/v1/") };
        var client = new AnthropicChatCompletionClient(httpClient, new AnthropicClientOptions());

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
    public async Task ToolCalling_ToolResult_ConvertedToUserRoleWithToolResultBlock()
    {
        string? capturedBody = null;
        var handler = new MockHttpMessageHandler(async (request, _) =>
        {
            capturedBody = await request.Content!.ReadAsStringAsync(CancellationToken.None);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(TextResponseJson, System.Text.Encoding.UTF8, "application/json")
            };
        });

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.anthropic.com/v1/") };
        var client = new AnthropicChatCompletionClient(httpClient, new AnthropicClientOptions());

        var args = JsonDocument.Parse("""{"city":"Paris"}""").RootElement.Clone();
        var messages = new List<LlmMessage>
        {
            new(LlmRole.User, "What's the weather?"),
            new(LlmRole.Assistant, "", ToolCalls: [new ToolCall("toolu_1", "get_weather", args)]),
            new(LlmRole.Tool, "Sunny, 22C", ToolCallId: "toolu_1")
        };

        var request = new ChatCompletionRequest(Messages: messages, Model: "claude-haiku-4-5-20250814");
        await client.GetChatCompletionWithToolsAsync(request, CreateToolOptions());

        var doc = JsonDocument.Parse(capturedBody!);
        var msgs = doc.RootElement.GetProperty("messages");

        // Tool result: converted to user role with content blocks
        var toolResultMsg = msgs[2];
        Assert.That(toolResultMsg.GetProperty("role").GetString(), Is.EqualTo("user"));
        var contentBlocks = toolResultMsg.GetProperty("content");
        Assert.That(contentBlocks.GetArrayLength(), Is.EqualTo(1));

        var block = contentBlocks[0];
        Assert.Multiple(() =>
        {
            Assert.That(block.GetProperty("type").GetString(), Is.EqualTo("tool_result"));
            Assert.That(block.GetProperty("tool_use_id").GetString(), Is.EqualTo("toolu_1"));
            Assert.That(block.GetProperty("content").GetString(), Is.EqualTo("Sunny, 22C"));
        });
    }

    [Test]
    public async Task ToolCalling_AssistantWithToolCalls_SerializesAsToolUseBlocks()
    {
        string? capturedBody = null;
        var handler = new MockHttpMessageHandler(async (request, _) =>
        {
            capturedBody = await request.Content!.ReadAsStringAsync(CancellationToken.None);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(TextResponseJson, System.Text.Encoding.UTF8, "application/json")
            };
        });

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.anthropic.com/v1/") };
        var client = new AnthropicChatCompletionClient(httpClient, new AnthropicClientOptions());

        var args = JsonDocument.Parse("""{"city":"Paris"}""").RootElement.Clone();
        var messages = new List<LlmMessage>
        {
            new(LlmRole.User, "What's the weather?"),
            new(LlmRole.Assistant, "", ToolCalls: [new ToolCall("toolu_1", "get_weather", args)]),
            new(LlmRole.Tool, "Sunny, 22C", ToolCallId: "toolu_1")
        };

        var request = new ChatCompletionRequest(Messages: messages, Model: "claude-haiku-4-5-20250814");
        await client.GetChatCompletionWithToolsAsync(request, CreateToolOptions());

        var doc = JsonDocument.Parse(capturedBody!);
        var msgs = doc.RootElement.GetProperty("messages");

        // Assistant with tool calls: content blocks with tool_use
        var assistantMsg = msgs[1];
        Assert.That(assistantMsg.GetProperty("role").GetString(), Is.EqualTo("assistant"));
        var contentBlocks = assistantMsg.GetProperty("content");
        Assert.That(contentBlocks.GetArrayLength(), Is.EqualTo(1));

        var block = contentBlocks[0];
        Assert.Multiple(() =>
        {
            Assert.That(block.GetProperty("type").GetString(), Is.EqualTo("tool_use"));
            Assert.That(block.GetProperty("id").GetString(), Is.EqualTo("toolu_1"));
            Assert.That(block.GetProperty("name").GetString(), Is.EqualTo("get_weather"));
            Assert.That(block.GetProperty("input").GetProperty("city").GetString(), Is.EqualTo("Paris"));
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

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.anthropic.com/v1/") };
        var client = new AnthropicChatCompletionClient(httpClient, new AnthropicClientOptions());

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
    public void AnthropicChatCompletionClient_ExposesToolCallingFeature()
    {
        using var httpClient = new HttpClient { BaseAddress = new Uri("https://api.anthropic.com/v1/") };
        var client = new AnthropicChatCompletionClient(httpClient, new AnthropicClientOptions());

        var feature = client.Features.Get<IToolCallingFeature>();

        Assert.Multiple(() =>
        {
            Assert.That(feature, Is.Not.Null);
            Assert.That(feature, Is.SameAs(client));
        });
    }

    #endregion
}
