using System.Net;
using System.Text.Json;
using Cisharpai.Features.Chat;
using Cisharpai.Models;
using Cisharpai.Azure.AzureAiInference;

namespace Cisharpai.Tests.Azure.AzureAiInference;

public sealed class AzureAiInferenceToolCallingTests
{
    private const string ModelId = "gpt-4o";
    private const string ApiVersion = "2024-05-01-preview";

    private static readonly JsonElement WeatherParameters = JsonDocument.Parse(
        """{"type":"object","properties":{"city":{"type":"string"},"unit":{"type":"string","enum":["celsius","fahrenheit"]}},"required":["city"]}""")
        .RootElement.Clone();

    #region Response Fixtures

    private const string ToolCallResponseJson = """
        {
            "id": "chatcmpl-123",
            "object": "chat.completion",
            "created": 1700000000,
            "model": "gpt-4o",
            "choices": [
                {
                    "index": 0,
                    "message": {
                        "role": "assistant",
                        "content": null,
                        "tool_calls": [
                            {
                                "id": "call_abc123",
                                "type": "function",
                                "function": {
                                    "name": "get_weather",
                                    "arguments": "{\"city\":\"Paris\",\"unit\":\"celsius\"}"
                                }
                            }
                        ]
                    },
                    "finish_reason": "tool_calls"
                }
            ],
            "usage": {
                "prompt_tokens": 50,
                "completion_tokens": 20,
                "total_tokens": 70
            }
        }
        """;

    private const string MultipleToolCallsResponseJson = """
        {
            "id": "chatcmpl-456",
            "object": "chat.completion",
            "created": 1700000000,
            "model": "gpt-4o",
            "choices": [
                {
                    "index": 0,
                    "message": {
                        "role": "assistant",
                        "content": null,
                        "tool_calls": [
                            {
                                "id": "call_1",
                                "type": "function",
                                "function": {
                                    "name": "get_weather",
                                    "arguments": "{\"city\":\"Paris\"}"
                                }
                            },
                            {
                                "id": "call_2",
                                "type": "function",
                                "function": {
                                    "name": "get_weather",
                                    "arguments": "{\"city\":\"London\"}"
                                }
                            }
                        ]
                    },
                    "finish_reason": "tool_calls"
                }
            ],
            "usage": {
                "prompt_tokens": 50,
                "completion_tokens": 30,
                "total_tokens": 80
            }
        }
        """;

    private const string TextResponseJson = """
        {
            "id": "chatcmpl-789",
            "object": "chat.completion",
            "created": 1700000000,
            "model": "gpt-4o",
            "choices": [
                {
                    "index": 0,
                    "message": {
                        "role": "assistant",
                        "content": "The weather in Paris is sunny."
                    },
                    "finish_reason": "stop"
                }
            ],
            "usage": {
                "prompt_tokens": 80,
                "completion_tokens": 10,
                "total_tokens": 90
            }
        }
        """;

    #endregion

    private static AzureAiInferenceClientOptions CreateOptions() =>
        new() { ModelId = ModelId, ApiKey = "test-key", Endpoint = "https://test.inference.azure.com/" };

    private static ChatCompletionRequest CreateRequest() =>
        new(Messages: [new LlmMessage(LlmRole.User, "What's the weather in Paris?")]);

    private static ToolCallingOptions CreateToolOptions(ToolChoice? choice = null) =>
        new(Tools: [new ToolDefinition("get_weather", "Get current weather", WeatherParameters)],
            ToolChoice: choice);

    #region Request Serialization Tests

    [Test]
    public async Task ToolCalling_SendsToolsArray()
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

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://test.inference.azure.com/") };
        var client = new AzureAiInferenceChatCompletionClient(httpClient, CreateOptions());

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
            Assert.That(function.GetProperty("strict").GetBoolean(), Is.True);
            Assert.That(function.GetProperty("parameters").GetProperty("type").GetString(), Is.EqualTo("object"));
        });
    }

    [Test]
    public async Task ToolCalling_ToolChoiceAuto_SendsAutoString()
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

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://test.inference.azure.com/") };
        var client = new AzureAiInferenceChatCompletionClient(httpClient, CreateOptions());

        await client.GetChatCompletionWithToolsAsync(CreateRequest(), CreateToolOptions(ToolChoice.Auto));

        var doc = JsonDocument.Parse(capturedBody!);
        Assert.That(doc.RootElement.GetProperty("tool_choice").GetString(), Is.EqualTo("auto"));
    }

    [Test]
    public async Task ToolCalling_ToolChoiceNone_SendsNoneString()
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

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://test.inference.azure.com/") };
        var client = new AzureAiInferenceChatCompletionClient(httpClient, CreateOptions());

        await client.GetChatCompletionWithToolsAsync(CreateRequest(), CreateToolOptions(ToolChoice.None));

        var doc = JsonDocument.Parse(capturedBody!);
        Assert.That(doc.RootElement.GetProperty("tool_choice").GetString(), Is.EqualTo("none"));
    }

    [Test]
    public async Task ToolCalling_ToolChoiceRequired_SendsRequiredString()
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

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://test.inference.azure.com/") };
        var client = new AzureAiInferenceChatCompletionClient(httpClient, CreateOptions());

        await client.GetChatCompletionWithToolsAsync(CreateRequest(), CreateToolOptions(ToolChoice.Required));

        var doc = JsonDocument.Parse(capturedBody!);
        Assert.That(doc.RootElement.GetProperty("tool_choice").GetString(), Is.EqualTo("required"));
    }

    [Test]
    public async Task ToolCalling_ToolChoiceSpecific_SendsFunctionObject()
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

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://test.inference.azure.com/") };
        var client = new AzureAiInferenceChatCompletionClient(httpClient, CreateOptions());

        await client.GetChatCompletionWithToolsAsync(CreateRequest(), CreateToolOptions(ToolChoice.Specific("get_weather")));

        var doc = JsonDocument.Parse(capturedBody!);
        var toolChoice = doc.RootElement.GetProperty("tool_choice");
        Assert.Multiple(() =>
        {
            Assert.That(toolChoice.GetProperty("type").GetString(), Is.EqualTo("function"));
            Assert.That(toolChoice.GetProperty("function").GetProperty("name").GetString(), Is.EqualTo("get_weather"));
        });
    }

    [Test]
    public async Task ToolCalling_NullToolChoice_OmitsToolChoiceFromRequest()
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

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://test.inference.azure.com/") };
        var client = new AzureAiInferenceChatCompletionClient(httpClient, CreateOptions());

        await client.GetChatCompletionWithToolsAsync(CreateRequest(), CreateToolOptions(null));

        var doc = JsonDocument.Parse(capturedBody!);
        Assert.That(doc.RootElement.TryGetProperty("tool_choice", out _), Is.False);
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

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://test.inference.azure.com/") };
        var client = new AzureAiInferenceChatCompletionClient(httpClient, CreateOptions());

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
            Assert.That(toolCall.Arguments.GetProperty("unit").GetString(), Is.EqualTo("celsius"));
        });
    }

    [Test]
    public async Task ToolCalling_MultipleToolCalls_MapsAll()
    {
        var handler = new MockHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(MultipleToolCallsResponseJson, System.Text.Encoding.UTF8, "application/json")
            }));

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://test.inference.azure.com/") };
        var client = new AzureAiInferenceChatCompletionClient(httpClient, CreateOptions());

        var response = await client.GetChatCompletionWithToolsAsync(CreateRequest(), CreateToolOptions());

        Assert.Multiple(() =>
        {
            Assert.That(response.ToolCalls!, Has.Count.EqualTo(2));
            Assert.That(response.ToolCalls[0].Id, Is.EqualTo("call_1"));
            Assert.That(response.ToolCalls[1].Id, Is.EqualTo("call_2"));
            Assert.That(response.ToolCalls[0].Arguments.GetProperty("city").GetString(), Is.EqualTo("Paris"));
            Assert.That(response.ToolCalls[1].Arguments.GetProperty("city").GetString(), Is.EqualTo("London"));
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

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://test.inference.azure.com/") };
        var client = new AzureAiInferenceChatCompletionClient(httpClient, CreateOptions());

        var response = await client.GetChatCompletionWithToolsAsync(CreateRequest(), CreateToolOptions());

        Assert.Multiple(() =>
        {
            Assert.That(response.IsSuccess, Is.True);
            Assert.That(response.ToolCalls, Is.Null);
            Assert.That(response.Content, Is.EqualTo("The weather in Paris is sunny."));
        });
    }

    [Test]
    public async Task ToolCalling_UsageTokensPopulated()
    {
        var handler = new MockHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(ToolCallResponseJson, System.Text.Encoding.UTF8, "application/json")
            }));

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://test.inference.azure.com/") };
        var client = new AzureAiInferenceChatCompletionClient(httpClient, CreateOptions());

        var response = await client.GetChatCompletionWithToolsAsync(CreateRequest(), CreateToolOptions());

        Assert.Multiple(() =>
        {
            Assert.That(response.ChatCompletion.PromptTokens, Is.EqualTo(50));
            Assert.That(response.ChatCompletion.CompletionTokens, Is.EqualTo(20));
        });
    }

    #endregion

    #region Multi-turn Tool Message Tests

    [Test]
    public async Task ToolCalling_ToolResultMessage_SerializesAsToolRole()
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

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://test.inference.azure.com/") };
        var client = new AzureAiInferenceChatCompletionClient(httpClient, CreateOptions());

        var args = JsonDocument.Parse("""{"city":"Paris"}""").RootElement.Clone();
        var messages = new List<LlmMessage>
        {
            new(LlmRole.User, "What's the weather?"),
            new(LlmRole.Assistant, "", ToolCalls: [new ToolCall("call_1", "get_weather", args)]),
            new(LlmRole.Tool, "Sunny, 22C", ToolCallId: "call_1")
        };

        var request = new ChatCompletionRequest(Messages: messages);
        await client.GetChatCompletionWithToolsAsync(request, CreateToolOptions());

        var doc = JsonDocument.Parse(capturedBody!);
        var msgs = doc.RootElement.GetProperty("messages");

        // Tool result message
        var toolMsg = msgs[2];
        Assert.Multiple(() =>
        {
            Assert.That(toolMsg.GetProperty("role").GetString(), Is.EqualTo("tool"));
            Assert.That(toolMsg.GetProperty("content").GetString(), Is.EqualTo("Sunny, 22C"));
            Assert.That(toolMsg.GetProperty("tool_call_id").GetString(), Is.EqualTo("call_1"));
        });
    }

    [Test]
    public async Task ToolCalling_AssistantWithToolCalls_SerializesToolCallsArray()
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

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://test.inference.azure.com/") };
        var client = new AzureAiInferenceChatCompletionClient(httpClient, CreateOptions());

        var args = JsonDocument.Parse("""{"city":"Paris"}""").RootElement.Clone();
        var messages = new List<LlmMessage>
        {
            new(LlmRole.User, "What's the weather?"),
            new(LlmRole.Assistant, "", ToolCalls: [new ToolCall("call_1", "get_weather", args)]),
            new(LlmRole.Tool, "Sunny, 22C", ToolCallId: "call_1")
        };

        var request = new ChatCompletionRequest(Messages: messages);
        await client.GetChatCompletionWithToolsAsync(request, CreateToolOptions());

        var doc = JsonDocument.Parse(capturedBody!);
        var msgs = doc.RootElement.GetProperty("messages");

        // Assistant message with tool_calls
        var assistantMsg = msgs[1];
        Assert.That(assistantMsg.GetProperty("role").GetString(), Is.EqualTo("assistant"));
        var toolCalls = assistantMsg.GetProperty("tool_calls");
        Assert.Multiple(() =>
        {
            Assert.That(toolCalls.GetArrayLength(), Is.EqualTo(1));
            Assert.That(toolCalls[0].GetProperty("id").GetString(), Is.EqualTo("call_1"));
            Assert.That(toolCalls[0].GetProperty("type").GetString(), Is.EqualTo("function"));
            Assert.That(toolCalls[0].GetProperty("function").GetProperty("name").GetString(), Is.EqualTo("get_weather"));
            Assert.That(toolCalls[0].GetProperty("function").GetProperty("arguments").GetString(), Is.EqualTo("""{"city":"Paris"}"""));
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

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://test.inference.azure.com/") };
        var client = new AzureAiInferenceChatCompletionClient(httpClient, CreateOptions());

        var response = await client.GetChatCompletionWithToolsAsync(CreateRequest(), CreateToolOptions());

        Assert.Multiple(() =>
        {
            Assert.That(response.IsSuccess, Is.False);
            Assert.That(response.ErrorMessage, Is.Not.Null.And.Not.Empty);
        });
    }

    [Test]
    public async Task ToolCalling_InvalidToolDefinition_ReturnsErrorResponse()
    {
        var handler = new MockHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(ToolCallResponseJson, System.Text.Encoding.UTF8, "application/json")
            }));

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://test.inference.azure.com/") };
        var client = new AzureAiInferenceChatCompletionClient(httpClient, CreateOptions());

        var invalidOptions = new ToolCallingOptions(Tools: []);

        var response = await client.GetChatCompletionWithToolsAsync(CreateRequest(), invalidOptions);

        Assert.That(response.IsSuccess, Is.False);
    }

    #endregion

    #region Feature Discovery Tests

    [Test]
    public void AzureAiInferenceChatCompletionClient_ExposesToolCallingFeature()
    {
        using var httpClient = new HttpClient { BaseAddress = new Uri("https://test.inference.azure.com/") };
        var options = new AzureAiInferenceClientOptions { ModelId = "test-model", ApiKey = "key" };
        var client = new AzureAiInferenceChatCompletionClient(httpClient, options);

        var feature = client.Features.Get<IToolCallingFeature>();

        Assert.Multiple(() =>
        {
            Assert.That(feature, Is.Not.Null);
            Assert.That(feature, Is.SameAs(client));
        });
    }

    #endregion

    #region Strict Flag Tests

    [Test]
    public async Task ToolCalling_StrictFalse_SetsStrictFalseOnTool()
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

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://test.inference.azure.com/") };
        var client = new AzureAiInferenceChatCompletionClient(httpClient, CreateOptions());

        var toolOptions = new ToolCallingOptions(
            Tools: [new ToolDefinition("get_weather", "Get weather", WeatherParameters, Strict: false)]);

        await client.GetChatCompletionWithToolsAsync(CreateRequest(), toolOptions);

        var doc = JsonDocument.Parse(capturedBody!);
        var strict = doc.RootElement.GetProperty("tools")[0]
            .GetProperty("function").GetProperty("strict").GetBoolean();
        Assert.That(strict, Is.False);
    }

    #endregion

    #region Endpoint/Model Verification Tests

    [Test]
    public async Task ToolCalling_UsesCorrectEndpoint()
    {
        Uri? capturedUri = null;
        var handler = new MockHttpMessageHandler(async (request, _) =>
        {
            capturedUri = request.RequestUri;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(ToolCallResponseJson, System.Text.Encoding.UTF8, "application/json")
            };
        });

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://test.inference.azure.com/") };
        var client = new AzureAiInferenceChatCompletionClient(httpClient, CreateOptions());

        await client.GetChatCompletionWithToolsAsync(CreateRequest(), CreateToolOptions());

        Assert.Multiple(() =>
        {
            Assert.That(capturedUri, Is.Not.Null);
            Assert.That(capturedUri!.PathAndQuery, Does.Contain("models/chat/completions"));
            Assert.That(capturedUri.PathAndQuery, Does.Contain($"api-version={ApiVersion}"));
        });
    }

    [Test]
    public async Task ToolCalling_IncludesModelInRequest()
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

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://test.inference.azure.com/") };
        var client = new AzureAiInferenceChatCompletionClient(httpClient, CreateOptions());

        await client.GetChatCompletionWithToolsAsync(CreateRequest(), CreateToolOptions());

        var doc = JsonDocument.Parse(capturedBody!);
        Assert.That(doc.RootElement.GetProperty("model").GetString(), Is.EqualTo(ModelId));
    }

    #endregion
}
