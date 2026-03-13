using System.Net;
using System.Text.Json;
using Cisharpai.Models;
using Cisharpai.Cohere;

namespace Cisharpai.Tests.Cohere;

public sealed class CohereChatCompletionTests
{
    private const string SuccessResponseJson = """
        {
            "id": "abc-123",
            "finish_reason": "COMPLETE",
            "message": {
                "role": "assistant",
                "content": [
                    {
                        "type": "text",
                        "text": "Hello! How can I help you?"
                    }
                ]
            },
            "usage": {
                "billed_units": {
                    "input_tokens": 17,
                    "output_tokens": 12
                },
                "tokens": {
                    "input_tokens": 215,
                    "output_tokens": 12
                }
            }
        }
        """;

    private const string MultiBlockResponseJson = """
        {
            "id": "abc-456",
            "finish_reason": "COMPLETE",
            "message": {
                "role": "assistant",
                "content": [
                    {
                        "type": "text",
                        "text": "First part. "
                    },
                    {
                        "type": "text",
                        "text": "Second part."
                    }
                ]
            },
            "usage": {
                "billed_units": {
                    "input_tokens": 10,
                    "output_tokens": 8
                },
                "tokens": {
                    "input_tokens": 100,
                    "output_tokens": 8
                }
            }
        }
        """;

    #region Request Mapping Tests

    [Test]
    public async Task GetChatCompletionAsync_MapsRequestCorrectly()
    {
        string? capturedBody = null;
        var handler = new MockHttpMessageHandler(async (request, _) =>
        {
            capturedBody = await request.Content!.ReadAsStringAsync();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(SuccessResponseJson, System.Text.Encoding.UTF8, "application/json")
            };
        });

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.cohere.com/v2/") };
        var client = new CohereChatCompletionClient(httpClient, new CohereClientOptions());

        var request = new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "Hello")],
            Model: "command-a-03-2025",
            Temperature: 0.5,
            MaxTokens: 100);

        await client.GetChatCompletionAsync(request);

        Assert.That(capturedBody, Is.Not.Null);
        var doc = JsonDocument.Parse(capturedBody!);
        Assert.Multiple(() =>
        {
            Assert.That(doc.RootElement.GetProperty("model").GetString(), Is.EqualTo("command-a-03-2025"));
            Assert.That(doc.RootElement.GetProperty("temperature").GetDouble(), Is.EqualTo(0.5));
            Assert.That(doc.RootElement.GetProperty("max_tokens").GetInt32(), Is.EqualTo(100));
        });
    }

    [Test]
    public async Task GetChatCompletionAsync_MapsMessagesWithRoles()
    {
        string? capturedBody = null;
        var handler = new MockHttpMessageHandler(async (request, _) =>
        {
            capturedBody = await request.Content!.ReadAsStringAsync();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(SuccessResponseJson, System.Text.Encoding.UTF8, "application/json")
            };
        });

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.cohere.com/v2/") };
        var client = new CohereChatCompletionClient(httpClient, new CohereClientOptions());

        var request = new ChatCompletionRequest(
            Messages:
            [
                new LlmMessage(LlmRole.System, "Be helpful"),
                new LlmMessage(LlmRole.User, "Hello"),
                new LlmMessage(LlmRole.Assistant, "Hi there!"),
                new LlmMessage(LlmRole.User, "How are you?")
            ],
            Model: "command-a-03-2025");

        await client.GetChatCompletionAsync(request);

        Assert.That(capturedBody, Is.Not.Null);
        var doc = JsonDocument.Parse(capturedBody!);
        var messages = doc.RootElement.GetProperty("messages");
        Assert.Multiple(() =>
        {
            Assert.That(messages.GetArrayLength(), Is.EqualTo(4));
            Assert.That(messages[0].GetProperty("role").GetString(), Is.EqualTo("system"));
            Assert.That(messages[0].GetProperty("content").GetString(), Is.EqualTo("Be helpful"));
            Assert.That(messages[1].GetProperty("role").GetString(), Is.EqualTo("user"));
            Assert.That(messages[2].GetProperty("role").GetString(), Is.EqualTo("assistant"));
            Assert.That(messages[3].GetProperty("role").GetString(), Is.EqualTo("user"));
        });
    }

    [Test]
    public async Task GetChatCompletionAsync_SystemMessage_IncludedInMessages()
    {
        string? capturedBody = null;
        var handler = new MockHttpMessageHandler(async (request, _) =>
        {
            capturedBody = await request.Content!.ReadAsStringAsync();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(SuccessResponseJson, System.Text.Encoding.UTF8, "application/json")
            };
        });

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.cohere.com/v2/") };
        var client = new CohereChatCompletionClient(httpClient, new CohereClientOptions());

        var request = new ChatCompletionRequest(
            Messages:
            [
                new LlmMessage(LlmRole.System, "You are a helpful assistant"),
                new LlmMessage(LlmRole.User, "Hello")
            ],
            Model: "command-a-03-2025");

        await client.GetChatCompletionAsync(request);

        Assert.That(capturedBody, Is.Not.Null);
        var doc = JsonDocument.Parse(capturedBody!);
        var messages = doc.RootElement.GetProperty("messages");

        // System message should be in messages array (not a separate field)
        Assert.Multiple(() =>
        {
            Assert.That(messages[0].GetProperty("role").GetString(), Is.EqualTo("system"));
            Assert.That(messages[0].GetProperty("content").GetString(), Is.EqualTo("You are a helpful assistant"));

            // No separate "system" field
            Assert.That(doc.RootElement.TryGetProperty("system", out _), Is.False);
        });
    }

    [Test]
    public async Task GetChatCompletionAsync_NullableFieldsOmitted()
    {
        string? capturedBody = null;
        var handler = new MockHttpMessageHandler(async (request, _) =>
        {
            capturedBody = await request.Content!.ReadAsStringAsync();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(SuccessResponseJson, System.Text.Encoding.UTF8, "application/json")
            };
        });

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.cohere.com/v2/") };
        var client = new CohereChatCompletionClient(httpClient, new CohereClientOptions());

        var request = new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "Hello")],
            Model: "command-a-03-2025");

        await client.GetChatCompletionAsync(request);

        Assert.That(capturedBody, Is.Not.Null);
        var doc = JsonDocument.Parse(capturedBody!);
        Assert.Multiple(() =>
        {
            Assert.That(doc.RootElement.TryGetProperty("temperature", out _), Is.False);
            Assert.That(doc.RootElement.TryGetProperty("max_tokens", out _), Is.False);
            Assert.That(doc.RootElement.TryGetProperty("response_format", out _), Is.False);
        });
    }

    [Test]
    public async Task GetChatCompletionAsync_UsesSnakeCaseNaming()
    {
        string? capturedBody = null;
        var handler = new MockHttpMessageHandler(async (request, _) =>
        {
            capturedBody = await request.Content!.ReadAsStringAsync();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(SuccessResponseJson, System.Text.Encoding.UTF8, "application/json")
            };
        });

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.cohere.com/v2/") };
        var client = new CohereChatCompletionClient(httpClient, new CohereClientOptions());

        var request = new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "Hello")],
            Model: "command-a-03-2025",
            MaxTokens: 100);

        await client.GetChatCompletionAsync(request);

        Assert.Multiple(() =>
        {
            Assert.That(capturedBody, Is.Not.Null);
            // Verify snake_case: max_tokens, not maxTokens
            Assert.That(capturedBody, Does.Contain("\"max_tokens\""));
            Assert.That(capturedBody, Does.Not.Contain("\"maxTokens\""));
            Assert.That(capturedBody, Does.Not.Contain("\"MaxTokens\""));
        });
    }

    [Test]
    public async Task GetChatCompletionAsync_PostsToChatEndpoint()
    {
        Uri? capturedUri = null;
        var handler = new MockHttpMessageHandler((request, _) =>
        {
            capturedUri = request.RequestUri;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(SuccessResponseJson, System.Text.Encoding.UTF8, "application/json")
            });
        });

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.cohere.com/v2/") };
        var client = new CohereChatCompletionClient(httpClient, new CohereClientOptions());

        var request = new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "Hello")],
            Model: "command-a-03-2025");

        await client.GetChatCompletionAsync(request);

        Assert.Multiple(() =>
        {
            Assert.That(capturedUri, Is.Not.Null);
            Assert.That(capturedUri!.ToString(), Does.EndWith("chat"));
        });
    }

    #endregion

    #region Response Mapping Tests

    [Test]
    public async Task GetChatCompletionAsync_MapsResponseCorrectly()
    {
        var handler = new MockHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(SuccessResponseJson, System.Text.Encoding.UTF8, "application/json")
            }));

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.cohere.com/v2/") };
        var client = new CohereChatCompletionClient(httpClient, new CohereClientOptions());

        var request = new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "Hello")],
            Model: "command-a-03-2025");

        var response = await client.GetChatCompletionAsync(request);

        Assert.Multiple(() =>
        {
            Assert.That(response.IsSuccess, Is.True);
            Assert.That(response.Content, Is.EqualTo("Hello! How can I help you?"));
            Assert.That(response.Model, Is.EqualTo("command-a-03-2025"));
            Assert.That(response.PromptTokens, Is.EqualTo(215));
            Assert.That(response.CompletionTokens, Is.EqualTo(12));
        });
    }

    [Test]
    public async Task GetChatCompletionAsync_JoinsMultipleContentBlocks()
    {
        var handler = new MockHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(MultiBlockResponseJson, System.Text.Encoding.UTF8, "application/json")
            }));

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.cohere.com/v2/") };
        var client = new CohereChatCompletionClient(httpClient, new CohereClientOptions());

        var request = new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "Hello")],
            Model: "command-a-03-2025");

        var response = await client.GetChatCompletionAsync(request);

        Assert.That(response.Content, Is.EqualTo("First part. Second part."));
    }

    [Test]
    public async Task GetChatCompletionAsync_ModelFromRequest_PassedThrough()
    {
        var handler = new MockHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(SuccessResponseJson, System.Text.Encoding.UTF8, "application/json")
            }));

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.cohere.com/v2/") };
        var client = new CohereChatCompletionClient(httpClient, new CohereClientOptions());

        var request = new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "Hello")],
            Model: "command-r-plus-08-2024");

        var response = await client.GetChatCompletionAsync(request);

        Assert.That(response.Model, Is.EqualTo("command-r-plus-08-2024"));
    }

    #endregion

    #region Error Handling Tests

    [Test]
    public async Task GetChatCompletionAsync_HttpError_ReturnsError()
    {
        var handler = new MockHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)
            {
                Content = new StringContent("{\"message\":\"Internal error\"}", System.Text.Encoding.UTF8, "application/json")
            }));

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.cohere.com/v2/") };
        var client = new CohereChatCompletionClient(httpClient, new CohereClientOptions());

        var request = new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "Hello")],
            Model: "command-a-03-2025");

        var response = await client.GetChatCompletionAsync(request);

        Assert.Multiple(() =>
        {
            Assert.That(response.IsSuccess, Is.False);
            Assert.That(response.ErrorMessage, Is.Not.Null.And.Not.Empty);
        });
    }

    #endregion

    #region Raw Response Tests

    [Test]
    public async Task GetChatCompletionAsync_WithRawResponse_IncludesRawJson()
    {
        var handler = new MockHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(SuccessResponseJson, System.Text.Encoding.UTF8, "application/json")
            }));

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.cohere.com/v2/") };
        var client = new CohereChatCompletionClient(httpClient, new CohereClientOptions());

        var request = new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "Hello")],
            Model: "command-a-03-2025",
            IncludeRawResponse: true);

        var response = await client.GetChatCompletionAsync(request);

        Assert.Multiple(() =>
        {
            Assert.That(response.RawResponseJson, Is.Not.Null.And.Not.Empty);
            Assert.That(response.RawRequestJson, Is.Not.Null.And.Not.Empty);
        });
    }

    [Test]
    public async Task GetChatCompletionAsync_WithoutRawResponse_RawJsonIsNull()
    {
        var handler = new MockHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(SuccessResponseJson, System.Text.Encoding.UTF8, "application/json")
            }));

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.cohere.com/v2/") };
        var client = new CohereChatCompletionClient(httpClient, new CohereClientOptions());

        var request = new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "Hello")],
            Model: "command-a-03-2025",
            IncludeRawResponse: false);

        var response = await client.GetChatCompletionAsync(request);

        Assert.Multiple(() =>
        {
            Assert.That(response.RawResponseJson, Is.Null);
            Assert.That(response.RawRequestJson, Is.Null);
        });
    }

    #endregion
}
