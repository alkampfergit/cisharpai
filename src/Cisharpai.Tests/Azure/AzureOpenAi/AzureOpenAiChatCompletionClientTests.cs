using System.Net;
using System.Text;
using System.Text.Json;
using Cisharpai.Features.Chat;
using Cisharpai.Models;
using Cisharpai.Azure.AzureOpenAi;

namespace Cisharpai.Tests.Azure.AzureOpenAi;

public sealed class AzureOpenAiChatCompletionClientTests
{
    [Test]
    public async Task GetChatCompletionAsync_MapsResponseToSharedModel()
    {
        var handler = new MockHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(AzureResponseJson, System.Text.Encoding.UTF8, "application/json")
            }));

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://myresource.openai.azure.com/") };
        var options = new AzureOpenAiClientOptions
        {
            Endpoint = "https://myresource.openai.azure.com/",
            DeploymentName = "gpt-4",
            ApiVersion = "2024-10-21",
            ApiKey = "test-key"
        };
        var client = new AzureOpenAiChatCompletionClient(httpClient, options);

        var response = await client.GetChatCompletionAsync(new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "Hello")],
            Model: "gpt-4"));

        Assert.Multiple(() =>
        {
            Assert.That(response.Content, Is.EqualTo("Hello there!"));
            Assert.That(response.Model, Is.EqualTo("gpt-4"));
            Assert.That(response.PromptTokens, Is.EqualTo(10));
            Assert.That(response.CompletionTokens, Is.EqualTo(20));
            Assert.That(response.IsSuccess, Is.True);
            Assert.That(response.ErrorMessage, Is.Null);
        });
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

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://myresource.openai.azure.com/") };
        var options = new AzureOpenAiClientOptions
        {
            Endpoint = "https://myresource.openai.azure.com/",
            DeploymentName = "gpt-4",
            ApiVersion = "2024-10-21",
            ApiKey = "test-key"
        };
        var client = new AzureOpenAiChatCompletionClient(httpClient, options);

        var response = await client.GetChatCompletionAsync(new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "Hello")],
            Model: "gpt-4"));

        Assert.Multiple(() =>
        {
            Assert.That(response.IsSuccess, Is.False);
            Assert.That(response.ErrorMessage, Does.Contain("500"));
            Assert.That(response.Content, Is.EqualTo(string.Empty));
            Assert.That(response.Model, Is.EqualTo(string.Empty));
        });
    }

    [Test]
    public async Task GetChatCompletionAsync_FinishReasonLength_ReturnsFailureWithIncompleteReason()
    {
        var handler = new MockHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(AzureLengthResponseJson, System.Text.Encoding.UTF8, "application/json")
            }));

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://myresource.openai.azure.com/") };
        var options = new AzureOpenAiClientOptions
        {
            Endpoint = "https://myresource.openai.azure.com/",
            DeploymentName = "gpt-4",
            ApiVersion = "2024-10-21",
            ApiKey = "test-key"
        };
        var client = new AzureOpenAiChatCompletionClient(httpClient, options);

        var response = await client.GetChatCompletionAsync(new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "Write a long answer")],
            Model: "gpt-4",
            MaxTokens: 1));

        Assert.Multiple(() =>
        {
            Assert.That(response.IsSuccess, Is.False);
            Assert.That(response.Status, Is.EqualTo("length"));
            Assert.That(response.IncompleteReason, Is.EqualTo("length"));
            Assert.That(response.ErrorMessage, Does.Contain("finish_reason"));
            Assert.That(response.Content, Is.EqualTo("This"));
            Assert.That(response.Model, Is.EqualTo("gpt-4"));
            Assert.That(response.PromptTokens, Is.EqualTo(12));
            Assert.That(response.CompletionTokens, Is.EqualTo(1));
        });
    }

    [TestCase("gpt-4", false)]
    [TestCase("gpt-4o", false)]
    [TestCase("o1", false)]
    [TestCase("o3-mini", false)]
    [TestCase("o4-mini", false)]
    [TestCase("gpt-5", true)]
    [TestCase("gpt-5-turbo", true)]
    public async Task GetChatCompletionAsync_ModelRouting_UsesCorrectEndpoint(string model, bool expectResponsesApi)
    {
        string? capturedUri = null;
        var responseJson = expectResponsesApi ? Gpt5ResponseJson : AzureResponseJson;
        var handler = new MockHttpMessageHandler(async (req, _) =>
        {
            capturedUri = req.RequestUri?.PathAndQuery;
            return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson, System.Text.Encoding.UTF8, "application/json")
            };
        });

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://myresource.openai.azure.com/") };
        var options = new AzureOpenAiClientOptions
        {
            Endpoint = "https://myresource.openai.azure.com/",
            DeploymentName = model,
            ApiVersion = "2025-04-01-preview",
            ApiKey = "test-key"
        };
        var client = new AzureOpenAiChatCompletionClient(httpClient, options);

        await client.GetChatCompletionAsync(new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "Hello")],
            Model: model));

        if (expectResponsesApi)
        {
            Assert.That(capturedUri, Does.Contain("/responses"));
            Assert.That(capturedUri, Does.Not.Contain("/chat/completions"));
        }
        else
        {
            Assert.That(capturedUri, Does.Contain("/chat/completions"));
            Assert.That(capturedUri, Does.Not.Contain("/responses"));
        }
    }

    [Test]
    public async Task GetChatCompletionAsync_Gpt5Model_UsesResponsesApiEndpoint()
    {
        string? capturedUri = null;
        var handler = new MockHttpMessageHandler(async (req, _) =>
        {
            capturedUri = req.RequestUri?.PathAndQuery;
            return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(Gpt5ResponseJson, System.Text.Encoding.UTF8, "application/json")
            };
        });

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://myresource.openai.azure.com/") };
        var options = new AzureOpenAiClientOptions
        {
            Endpoint = "https://myresource.openai.azure.com/",
            DeploymentName = "gpt-5",
            ApiVersion = "2025-04-01-preview",
            ApiKey = "test-key"
        };
        var client = new AzureOpenAiChatCompletionClient(httpClient, options);

        var response = await client.GetChatCompletionAsync(new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "Hello")],
            Model: "gpt-5"));

        Assert.Multiple(() =>
        {
            Assert.That(capturedUri, Does.Contain("/responses"));
            Assert.That(capturedUri, Does.Not.Contain("/chat/completions"));
            Assert.That(response.IsSuccess, Is.True);
            Assert.That(response.Content, Is.EqualTo("Hello there!"));
            Assert.That(response.PromptTokens, Is.EqualTo(10));
            Assert.That(response.CompletionTokens, Is.EqualTo(5));
        });
    }

    [Test]
    public async Task GetChatCompletionAsync_Gpt5Model_IncludesTextVerbosity()
    {
        string? capturedBody = null;
        var handler = new MockHttpMessageHandler(async (req, _) =>
        {
            capturedBody = await req.Content!.ReadAsStringAsync(CancellationToken.None);
            return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(Gpt5ResponseJson, System.Text.Encoding.UTF8, "application/json")
            };
        });

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://myresource.openai.azure.com/") };
        var options = new AzureOpenAiClientOptions
        {
            Endpoint = "https://myresource.openai.azure.com/",
            DeploymentName = "gpt-5",
            ApiVersion = "2025-04-01-preview",
            ApiKey = "test-key",
            TextVerbosity = "high"
        };
        var client = new AzureOpenAiChatCompletionClient(httpClient, options);

        await client.GetChatCompletionAsync(new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "Hello")],
            Model: "gpt-5"));

        var doc = System.Text.Json.JsonDocument.Parse(capturedBody!);
        Assert.That(doc.RootElement.GetProperty("text").GetProperty("verbosity").GetString(), Is.EqualTo("high"));
    }

    [Test]
    public async Task GetChatCompletionAsync_OpaqueDeploymentName_WithModelNameGpt5_RoutesToResponsesApi()
    {
        string? capturedUri = null;
        var handler = new MockHttpMessageHandler(async (req, _) =>
        {
            capturedUri = req.RequestUri?.PathAndQuery;
            return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(Gpt5ResponseJson, System.Text.Encoding.UTF8, "application/json")
            };
        });

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://myresource.openai.azure.com/") };
        var options = new AzureOpenAiClientOptions
        {
            Endpoint = "https://myresource.openai.azure.com/",
            DeploymentName = "foo",
            ApiVersion = "2025-04-01-preview",
            ApiKey = "test-key",
            ModelName = "gpt-5"
        };
        var client = new AzureOpenAiChatCompletionClient(httpClient, options);

        await client.GetChatCompletionAsync(new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "Hello")],
            Model: "foo"));

        Assert.Multiple(() =>
        {
            Assert.That(capturedUri, Does.Contain("/deployments/foo/responses"));
            Assert.That(capturedUri, Does.Not.Contain("/chat/completions"));
        });
    }

    [Test]
    public async Task GetChatCompletionAsync_OpaqueDeploymentName_WithoutModelName_FallsBackToChatCompletions()
    {
        string? capturedUri = null;
        var handler = new MockHttpMessageHandler(async (req, _) =>
        {
            capturedUri = req.RequestUri?.PathAndQuery;
            return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(AzureResponseJson, System.Text.Encoding.UTF8, "application/json")
            };
        });

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://myresource.openai.azure.com/") };
        var options = new AzureOpenAiClientOptions
        {
            Endpoint = "https://myresource.openai.azure.com/",
            DeploymentName = "foo",
            ApiVersion = "2024-10-21",
            ApiKey = "test-key"
        };
        var client = new AzureOpenAiChatCompletionClient(httpClient, options);

        await client.GetChatCompletionAsync(new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "Hello")],
            Model: "foo"));

        Assert.That(capturedUri, Does.Contain("/chat/completions"));
    }

    [Test]
    public async Task GetChatCompletionAsync_ModelName_OverridesDeploymentNameHint()
    {
        // Deployment name suggests gpt-5 but ModelName explicitly says legacy gpt-4o
        string? capturedUri = null;
        var handler = new MockHttpMessageHandler(async (req, _) =>
        {
            capturedUri = req.RequestUri?.PathAndQuery;
            return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(AzureResponseJson, System.Text.Encoding.UTF8, "application/json")
            };
        });

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://myresource.openai.azure.com/") };
        var options = new AzureOpenAiClientOptions
        {
            Endpoint = "https://myresource.openai.azure.com/",
            DeploymentName = "gpt-5-misnamed",
            ApiVersion = "2024-10-21",
            ApiKey = "test-key",
            ModelName = "gpt-4o"
        };
        var client = new AzureOpenAiChatCompletionClient(httpClient, options);

        await client.GetChatCompletionAsync(new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "Hello")],
            Model: "gpt-5-misnamed"));

        Assert.That(capturedUri, Does.Contain("/chat/completions"));
    }

    [Test]
    public async Task GetChatCompletionAsync_ChatCompletions404_FallsBackToResponsesApi_AndCachesRoute()
    {
        const string notFoundJson = """{"error":{"code":"404","message":"Resource not found"}}""";
        var requestedUris = new List<string>();
        var handler = new MockHttpMessageHandler((req, _) =>
        {
            requestedUris.Add(req.RequestUri!.PathAndQuery);

            if (req.RequestUri.AbsolutePath.EndsWith("/chat/completions", StringComparison.Ordinal))
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)
                {
                    Content = new StringContent(notFoundJson, Encoding.UTF8, "application/json")
                });
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(Gpt5ResponseJson, Encoding.UTF8, "application/json")
            });
        });

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://myresource.openai.azure.com/") };
        var options = new AzureOpenAiClientOptions
        {
            Endpoint = "https://myresource.openai.azure.com/",
            DeploymentName = "opaque-deployment",
            ApiVersion = "2025-04-01-preview",
            ApiKey = "test-key"
        };
        var client = new AzureOpenAiChatCompletionClient(httpClient, options);
        var request = new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "Hello")],
            Model: "opaque-deployment");

        var firstResponse = await client.GetChatCompletionAsync(request);
        var secondResponse = await client.GetChatCompletionAsync(request);

        Assert.Multiple(() =>
        {
            Assert.That(firstResponse.IsSuccess, Is.True);
            Assert.That(firstResponse.Content, Is.EqualTo("Hello there!"));
            Assert.That(secondResponse.IsSuccess, Is.True);
            Assert.That(requestedUris, Is.EqualTo(ChatToResponsesFallbackUris));
        });
    }

    [Test]
    public async Task GetChatCompletionAsync_ResponsesApi404_FallsBackToChatCompletions_AndCachesRoute()
    {
        const string notFoundJson = """{"error":{"code":"404","message":"Resource not found"}}""";
        var requestedUris = new List<string>();
        var handler = new MockHttpMessageHandler((req, _) =>
        {
            requestedUris.Add(req.RequestUri!.PathAndQuery);

            if (req.RequestUri.AbsolutePath.EndsWith("/responses", StringComparison.Ordinal))
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)
                {
                    Content = new StringContent(notFoundJson, Encoding.UTF8, "application/json")
                });
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(AzureResponseJson, Encoding.UTF8, "application/json")
            });
        });

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://myresource.openai.azure.com/") };
        var options = new AzureOpenAiClientOptions
        {
            Endpoint = "https://myresource.openai.azure.com/",
            DeploymentName = "gpt-5-misleading",
            ApiVersion = "2025-04-01-preview",
            ApiKey = "test-key"
        };
        var client = new AzureOpenAiChatCompletionClient(httpClient, options);
        var request = new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "Hello")],
            Model: "gpt-5-misleading");

        var firstResponse = await client.GetChatCompletionAsync(request);
        var secondResponse = await client.GetChatCompletionAsync(request);

        Assert.Multiple(() =>
        {
            Assert.That(firstResponse.IsSuccess, Is.True);
            Assert.That(firstResponse.Content, Is.EqualTo("Hello there!"));
            Assert.That(secondResponse.IsSuccess, Is.True);
            Assert.That(requestedUris, Is.EqualTo(ResponsesToChatFallbackUris));
        });
    }

    [Test]
    public async Task GetChatCompletionAsync_FallbackRoute_IsSharedAcrossClientInstances()
    {
        const string notFoundJson = """{"error":{"code":"404","message":"Resource not found"}}""";
        var options = new AzureOpenAiClientOptions
        {
            Endpoint = "https://myresource.openai.azure.com/",
            DeploymentName = "shared-route-cache",
            ApiVersion = "2025-04-01-preview",
            ApiKey = "test-key"
        };
        var request = new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "Hello")],
            Model: "shared-route-cache");

        var firstUris = new List<string>();
        var firstHandler = new MockHttpMessageHandler((req, _) =>
        {
            firstUris.Add(req.RequestUri!.PathAndQuery);

            if (req.RequestUri.AbsolutePath.EndsWith("/chat/completions", StringComparison.Ordinal))
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)
                {
                    Content = new StringContent(notFoundJson, Encoding.UTF8, "application/json")
                });
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(Gpt5ResponseJson, Encoding.UTF8, "application/json")
            });
        });

        using var firstHttpClient = new HttpClient(firstHandler) { BaseAddress = new Uri("https://myresource.openai.azure.com/") };
        var firstClient = new AzureOpenAiChatCompletionClient(firstHttpClient, options);

        var firstResponse = await firstClient.GetChatCompletionAsync(request);

        var secondUris = new List<string>();
        var secondHandler = new MockHttpMessageHandler((req, _) =>
        {
            secondUris.Add(req.RequestUri!.PathAndQuery);

            if (req.RequestUri.AbsolutePath.EndsWith("/chat/completions", StringComparison.Ordinal))
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)
                {
                    Content = new StringContent(notFoundJson, Encoding.UTF8, "application/json")
                });
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(Gpt5ResponseJson, Encoding.UTF8, "application/json")
            });
        });

        using var secondHttpClient = new HttpClient(secondHandler) { BaseAddress = new Uri("https://myresource.openai.azure.com/") };
        var secondClient = new AzureOpenAiChatCompletionClient(secondHttpClient, options);

        var secondResponse = await secondClient.GetChatCompletionAsync(request);

        Assert.Multiple(() =>
        {
            Assert.That(firstResponse.IsSuccess, Is.True);
            Assert.That(secondResponse.IsSuccess, Is.True);
            Assert.That(firstUris, Is.EqualTo(SharedRouteCacheFirstClientUris));
            Assert.That(secondUris, Is.EqualTo(SharedRouteCacheSecondClientUris));
        });
    }

    [Test]
    public async Task GetChatCompletionWithToolsAsync_LegacyPayloadRejected_RetriesReasoningFormat()
    {
        const string unsupportedParameterJson = """
            {
              "error": {
                "message": "Unsupported parameter: 'max_tokens' is not supported with this model. Use 'max_completion_tokens' instead.",
                "type": "invalid_request_error",
                "param": "max_tokens",
                "code": "unsupported_parameter"
              }
            }
            """;
        const string toolCallResponseJson = """
            {
              "model": "o3-mini",
              "choices": [
                {
                  "message": {
                    "role": "assistant",
                    "tool_calls": [
                      {
                        "id": "call_1",
                        "type": "function",
                        "function": {
                          "name": "get_weather",
                          "arguments": "{\"city\":\"Paris\"}"
                        }
                      }
                    ]
                  },
                  "finish_reason": "tool_calls"
                }
              ],
              "usage": {
                "prompt_tokens": 10,
                "completion_tokens": 5
              }
            }
            """;

        var capturedBodies = new List<string>();
        var handler = new MockHttpMessageHandler(async (req, _) =>
        {
            var body = await req.Content!.ReadAsStringAsync(CancellationToken.None);
            capturedBodies.Add(body);

            if (body.Contains("\"max_tokens\"", StringComparison.Ordinal))
            {
                return new HttpResponseMessage(HttpStatusCode.BadRequest)
                {
                    Content = new StringContent(unsupportedParameterJson, Encoding.UTF8, "application/json")
                };
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(toolCallResponseJson, Encoding.UTF8, "application/json")
            };
        });

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://myresource.openai.azure.com/") };
        var options = new AzureOpenAiClientOptions
        {
            Endpoint = "https://myresource.openai.azure.com/",
            DeploymentName = "opaque-reasoning",
            ApiVersion = "2024-10-21",
            ApiKey = "test-key"
        };
        var client = new AzureOpenAiChatCompletionClient(httpClient, options);
        var toolOptions = new ToolCallingOptions(
            Tools:
            [
                new ToolDefinition(
                    "get_weather",
                    "Get the weather for a city",
                    JsonDocument.Parse("""{"type":"object","properties":{"city":{"type":"string"}},"required":["city"]}""").RootElement.Clone())
            ]);

        var response = await client.GetChatCompletionWithToolsAsync(
            new ChatCompletionRequest(
                Messages: [new LlmMessage(LlmRole.User, "What is the weather in Paris?")],
                Model: "opaque-reasoning",
                MaxTokens: 128),
            toolOptions);

        Assert.Multiple(() =>
        {
            Assert.That(response.IsSuccess, Is.True, response.ErrorMessage);
            Assert.That(response.ToolCalls, Is.Not.Null.And.Count.EqualTo(1));
            Assert.That(response.ToolCalls![0].FunctionName, Is.EqualTo("get_weather"));
            Assert.That(capturedBodies, Has.Count.EqualTo(2));
            Assert.That(capturedBodies[0], Does.Contain("\"max_tokens\""));
            Assert.That(capturedBodies[1], Does.Contain("max_completion_tokens"));
        });
    }

    [Test]
    public async Task GetChatCompletionWithToolsAsync_FallbackShape_IsSharedAcrossClientInstances()
    {
        const string unsupportedParameterJson = """
            {
                "error": {
                    "message": "Unsupported parameter: 'max_tokens' is not supported with this model. Use 'max_completion_tokens' instead.",
                    "type": "invalid_request_error",
                    "param": "max_tokens",
                    "code": "unsupported_parameter"
                }
            }
            """;
        const string toolCallResponseJson = """
            {
                "model": "o3-mini",
                "choices": [
                    {
                        "message": {
                            "role": "assistant",
                            "tool_calls": [
                                {
                                    "id": "call_1",
                                    "type": "function",
                                    "function": {
                                        "name": "get_weather",
                                        "arguments": "{\"city\":\"Paris\"}"
                                    }
                                }
                            ]
                        },
                        "finish_reason": "tool_calls"
                    }
                ],
                "usage": {
                    "prompt_tokens": 10,
                    "completion_tokens": 5
                }
            }
            """;

        var options = new AzureOpenAiClientOptions
        {
            Endpoint = "https://myresource.openai.azure.com/",
            DeploymentName = "shared-shape-cache",
            ApiVersion = "2024-10-21",
            ApiKey = "test-key"
        };
        var toolOptions = new ToolCallingOptions(
            Tools:
            [
                new ToolDefinition(
                    "get_weather",
                    "Get the weather for a city",
                    JsonDocument.Parse("""{"type":"object","properties":{"city":{"type":"string"}},"required":["city"]}""").RootElement.Clone())
            ]);
        var request = new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "What is the weather in Paris?")],
            Model: "shared-shape-cache",
            MaxTokens: 128);

        var firstBodies = new List<string>();
        var firstHandler = new MockHttpMessageHandler(async (req, _) =>
        {
            var body = await req.Content!.ReadAsStringAsync(CancellationToken.None);
            firstBodies.Add(body);

            if (body.Contains("\"max_tokens\"", StringComparison.Ordinal))
            {
                return new HttpResponseMessage(HttpStatusCode.BadRequest)
                {
                    Content = new StringContent(unsupportedParameterJson, Encoding.UTF8, "application/json")
                };
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(toolCallResponseJson, Encoding.UTF8, "application/json")
            };
        });

        using var firstHttpClient = new HttpClient(firstHandler) { BaseAddress = new Uri("https://myresource.openai.azure.com/") };
        var firstClient = new AzureOpenAiChatCompletionClient(firstHttpClient, options);

        var firstResponse = await firstClient.GetChatCompletionWithToolsAsync(request, toolOptions);

        var secondBodies = new List<string>();
        var secondHandler = new MockHttpMessageHandler(async (req, _) =>
        {
            var body = await req.Content!.ReadAsStringAsync(CancellationToken.None);
            secondBodies.Add(body);

            if (body.Contains("\"max_tokens\"", StringComparison.Ordinal))
            {
                return new HttpResponseMessage(HttpStatusCode.BadRequest)
                {
                    Content = new StringContent(unsupportedParameterJson, Encoding.UTF8, "application/json")
                };
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(toolCallResponseJson, Encoding.UTF8, "application/json")
            };
        });

        using var secondHttpClient = new HttpClient(secondHandler) { BaseAddress = new Uri("https://myresource.openai.azure.com/") };
        var secondClient = new AzureOpenAiChatCompletionClient(secondHttpClient, options);

        var secondResponse = await secondClient.GetChatCompletionWithToolsAsync(request, toolOptions);

        Assert.Multiple(() =>
        {
            Assert.That(firstResponse.IsSuccess, Is.True, firstResponse.ErrorMessage);
            Assert.That(secondResponse.IsSuccess, Is.True, secondResponse.ErrorMessage);
            Assert.That(firstBodies, Has.Count.EqualTo(2));
            Assert.That(firstBodies[0], Does.Contain("\"max_tokens\""));
            Assert.That(firstBodies[1], Does.Contain("max_completion_tokens"));
            Assert.That(secondBodies, Has.Count.EqualTo(1));
            Assert.That(secondBodies[0], Does.Contain("max_completion_tokens"));
            Assert.That(secondBodies[0], Does.Not.Contain("\"max_tokens\""));
        });
    }

    [Test]
    public async Task GetChatCompletionAsync_Gpt5Model_RequestUsesInputField()
    {
        string? capturedBody = null;
        var handler = new MockHttpMessageHandler(async (req, _) =>
        {
            capturedBody = await req.Content!.ReadAsStringAsync(CancellationToken.None);
            return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(Gpt5ResponseJson, System.Text.Encoding.UTF8, "application/json")
            };
        });

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://myresource.openai.azure.com/") };
        var options = new AzureOpenAiClientOptions
        {
            Endpoint = "https://myresource.openai.azure.com/",
            DeploymentName = "gpt-5",
            ApiVersion = "2025-04-01-preview",
            ApiKey = "test-key"
        };
        var client = new AzureOpenAiChatCompletionClient(httpClient, options);

        await client.GetChatCompletionAsync(new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "Hello")],
            Model: "gpt-5"));

        var doc = System.Text.Json.JsonDocument.Parse(capturedBody!);
        Assert.Multiple(() =>
        {
            Assert.That(doc.RootElement.TryGetProperty("input", out _), Is.True, "Should use 'input' field");
            Assert.That(doc.RootElement.TryGetProperty("messages", out _), Is.False, "Should not use 'messages' field");
        });
    }

    [Test]
    public async Task GetChatCompletionWithJsonOutputAsync_Gpt5Model_UsesResponsesApiWithTextFormat()
    {
        string? capturedBody = null;
        string? capturedUri = null;
        var handler = new MockHttpMessageHandler(async (req, _) =>
        {
            capturedUri = req.RequestUri?.PathAndQuery;
            capturedBody = await req.Content!.ReadAsStringAsync(CancellationToken.None);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(Gpt5JsonResponseJson, Encoding.UTF8, "application/json")
            };
        });

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://myresource.openai.azure.com/") };
        var options = new AzureOpenAiClientOptions
        {
            Endpoint = "https://myresource.openai.azure.com/",
            DeploymentName = "gpt-5",
            ApiVersion = "2025-04-01-preview",
            ApiKey = "test-key"
        };
        var client = new AzureOpenAiChatCompletionClient(httpClient, options);

        var jsonOptions = new JsonOutputOptions(Mode: JsonOutputMode.JsonMode);

        var response = await client.GetChatCompletionWithJsonOutputAsync(
            new ChatCompletionRequest(
                Messages: [new LlmMessage(LlmRole.User, "Give me a person")],
                Model: "gpt-5"),
            jsonOptions);

        Assert.That(capturedBody, Is.Not.Null);
        var doc = JsonDocument.Parse(capturedBody!);
        var format = doc.RootElement.GetProperty("text").GetProperty("format");
        Assert.Multiple(() =>
        {
            Assert.That(capturedUri, Does.Contain("/responses"));
            Assert.That(format.GetProperty("type").GetString(), Is.EqualTo("json_object"));
            Assert.That(response.Content, Is.EqualTo("{\"name\":\"John\"}"));
            Assert.That(response.IsSuccess, Is.True);
        });
    }

    [Test]
    public async Task GetChatCompletionWithJsonOutputAsync_Gpt5Model_JsonSchema_SetsSchemaInTextFormat()
    {
        string? capturedBody = null;
        var handler = new MockHttpMessageHandler(async (req, _) =>
        {
            capturedBody = await req.Content!.ReadAsStringAsync(CancellationToken.None);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(Gpt5JsonResponseJson, Encoding.UTF8, "application/json")
            };
        });

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://myresource.openai.azure.com/") };
        var options = new AzureOpenAiClientOptions
        {
            Endpoint = "https://myresource.openai.azure.com/",
            DeploymentName = "gpt-5",
            ApiVersion = "2025-04-01-preview",
            ApiKey = "test-key",
            TextVerbosity = "low"
        };
        var client = new AzureOpenAiChatCompletionClient(httpClient, options);

        var jsonOptions = new JsonOutputOptions(
            Mode: JsonOutputMode.JsonSchema,
            SchemaName: "person",
            SchemaDescription: "A person",
            Strict: true,
            JsonSchema: PersonJsonSchema);

        await client.GetChatCompletionWithJsonOutputAsync(
            new ChatCompletionRequest(
                Messages: [new LlmMessage(LlmRole.User, "Give me a person")],
                Model: "gpt-5"),
            jsonOptions);

        var doc = JsonDocument.Parse(capturedBody!);
        var text = doc.RootElement.GetProperty("text");
        var format = text.GetProperty("format");
        Assert.Multiple(() =>
        {
            Assert.That(text.GetProperty("verbosity").GetString(), Is.EqualTo("low"));
            Assert.That(format.GetProperty("type").GetString(), Is.EqualTo("json_schema"));
            Assert.That(format.GetProperty("name").GetString(), Is.EqualTo("person"));
            Assert.That(format.GetProperty("description").GetString(), Is.EqualTo("A person"));
            Assert.That(format.GetProperty("strict").GetBoolean(), Is.True);
            Assert.That(format.TryGetProperty("schema", out _), Is.True);
        });
    }

    [Test]
    public async Task GetChatCompletionAsync_Gpt5Model_IncludeRawResponse_ReturnsRawJson()
    {
        var handler = new MockHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(Gpt5ResponseJson, Encoding.UTF8, "application/json")
            }));

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://myresource.openai.azure.com/") };
        var options = new AzureOpenAiClientOptions
        {
            Endpoint = "https://myresource.openai.azure.com/",
            DeploymentName = "gpt-5",
            ApiVersion = "2025-04-01-preview",
            ApiKey = "test-key"
        };
        var client = new AzureOpenAiChatCompletionClient(httpClient, options);

        var response = await client.GetChatCompletionAsync(new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "Hello")],
            Model: "gpt-5",
            IncludeRawResponse: true));

        Assert.Multiple(() =>
        {
            Assert.That(response.IsSuccess, Is.True);
            Assert.That(response.RawResponseJson, Is.Not.Null);
            Assert.That(response.RawResponseJson, Does.Contain("\"output_text\""));
            Assert.That(response.RawRequestJson, Is.Not.Null);
            Assert.That(response.RawRequestJson, Does.Contain("\"input\""));
        });
    }

    [Test]
    public async Task GetChatCompletionAsync_Gpt5Model_IncompleteResponse_ReturnsErrorWithReason()
    {
        var handler = new MockHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(Gpt5IncompleteResponseJson, Encoding.UTF8, "application/json")
            }));

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://myresource.openai.azure.com/") };
        var options = new AzureOpenAiClientOptions
        {
            Endpoint = "https://myresource.openai.azure.com/",
            DeploymentName = "gpt-5",
            ApiVersion = "2025-04-01-preview",
            ApiKey = "test-key"
        };
        var client = new AzureOpenAiChatCompletionClient(httpClient, options);

        var response = await client.GetChatCompletionAsync(new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "Long answer")],
            Model: "gpt-5",
            MaxTokens: 1));

        Assert.Multiple(() =>
        {
            Assert.That(response.IsSuccess, Is.False);
            Assert.That(response.Status, Is.EqualTo("incomplete"));
            Assert.That(response.IncompleteReason, Is.EqualTo("max_output_tokens"));
            Assert.That(response.ErrorMessage, Does.Contain("max_output_tokens"));
        });
    }

    [Test]
    public async Task GetChatCompletionAsync_Gpt5Model_FailedStatus_ReturnsErrorResponse()
    {
        var handler = new MockHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(Gpt5FailedResponseJson, Encoding.UTF8, "application/json")
            }));

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://myresource.openai.azure.com/") };
        var options = new AzureOpenAiClientOptions
        {
            Endpoint = "https://myresource.openai.azure.com/",
            DeploymentName = "gpt-5",
            ApiVersion = "2025-04-01-preview",
            ApiKey = "test-key"
        };
        var client = new AzureOpenAiChatCompletionClient(httpClient, options);

        var response = await client.GetChatCompletionAsync(new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "Hello")],
            Model: "gpt-5"));

        Assert.Multiple(() =>
        {
            Assert.That(response.IsSuccess, Is.False);
            Assert.That(response.Status, Is.EqualTo("failed"));
            Assert.That(response.IncompleteReason, Is.Null);
            Assert.That(response.ErrorMessage, Does.Contain("failed"));
        });
    }

    [Test]
    public async Task GetChatCompletionAsync_Gpt5Model_RefusalContent_PopulatesRefusal()
    {
        var handler = new MockHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(Gpt5RefusalResponseJson, Encoding.UTF8, "application/json")
            }));

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://myresource.openai.azure.com/") };
        var options = new AzureOpenAiClientOptions
        {
            Endpoint = "https://myresource.openai.azure.com/",
            DeploymentName = "gpt-5",
            ApiVersion = "2025-04-01-preview",
            ApiKey = "test-key"
        };
        var client = new AzureOpenAiChatCompletionClient(httpClient, options);

        var response = await client.GetChatCompletionAsync(new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "Refused topic")],
            Model: "gpt-5"));

        Assert.Multiple(() =>
        {
            Assert.That(response.Refusal, Is.EqualTo("I cannot help with that."));
            Assert.That(response.Content, Is.EqualTo(string.Empty));
            Assert.That(response.IsSuccess, Is.True);
        });
    }

    [Test]
    public async Task GetChatCompletionAsync_Gpt5Model_ReasoningEffortFromOptions_IsSent()
    {
        string? capturedBody = null;
        var handler = new MockHttpMessageHandler(async (req, _) =>
        {
            capturedBody = await req.Content!.ReadAsStringAsync(CancellationToken.None);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(Gpt5ResponseJson, Encoding.UTF8, "application/json")
            };
        });

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://myresource.openai.azure.com/") };
        var options = new AzureOpenAiClientOptions
        {
            Endpoint = "https://myresource.openai.azure.com/",
            DeploymentName = "gpt-5",
            ApiVersion = "2025-04-01-preview",
            ApiKey = "test-key",
            ReasoningEffort = "high"
        };
        var client = new AzureOpenAiChatCompletionClient(httpClient, options);

        await client.GetChatCompletionAsync(new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "Think")],
            Model: "gpt-5"));

        var doc = JsonDocument.Parse(capturedBody!);
        Assert.That(doc.RootElement.GetProperty("reasoning").GetProperty("effort").GetString(), Is.EqualTo("high"));
    }

    [Test]
    public async Task GetChatCompletionStreamAsync_Gpt5Model_StreamsTextDeltasAndFinalChunk()
    {
        var bytes = Encoding.UTF8.GetBytes(Gpt5StreamSse);
        var handler = new MockHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StreamContent(new MemoryStream(bytes))
            }));

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://myresource.openai.azure.com/") };
        var options = new AzureOpenAiClientOptions
        {
            Endpoint = "https://myresource.openai.azure.com/",
            DeploymentName = "gpt-5",
            ApiVersion = "2025-04-01-preview",
            ApiKey = "test-key"
        };
        var client = new AzureOpenAiChatCompletionClient(httpClient, options);

        var feature = client.Features.Get<IStreamingChatFeature>()!;
        var chunks = new List<ChatCompletionChunk>();
        await foreach (var chunk in feature.GetChatCompletionStreamAsync(new ChatCompletionRequest(
                           Messages: [new LlmMessage(LlmRole.User, "Stream")],
                           Model: "gpt-5")))
        {
            chunks.Add(chunk);
        }

        var textChunks = chunks.Where(c => !string.IsNullOrEmpty(c.Content)).ToList();
        var combined = string.Concat(textChunks.Select(c => c.Content));
        var finalChunk = chunks.FirstOrDefault(c => c.FinishReason is not null);

        Assert.Multiple(() =>
        {
            Assert.That(combined, Is.EqualTo("Hello, world!"));
            Assert.That(finalChunk, Is.Not.Null);
            Assert.That(finalChunk!.FinishReason, Is.EqualTo("completed"));
            Assert.That(finalChunk.Model, Is.EqualTo("gpt-5"));
            Assert.That(finalChunk.PromptTokens, Is.EqualTo(10));
            Assert.That(finalChunk.CompletionTokens, Is.EqualTo(5));
        });
    }

    [Test]
    public async Task GetChatCompletionStreamAsync_Gpt5Model_TargetsResponsesEndpointWithStreamFlag()
    {
        string? capturedUri = null;
        string? capturedBody = null;
        var bytes = Encoding.UTF8.GetBytes(Gpt5StreamSse);
        var handler = new MockHttpMessageHandler(async (req, _) =>
        {
            capturedUri = req.RequestUri?.PathAndQuery;
            capturedBody = await req.Content!.ReadAsStringAsync(CancellationToken.None);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StreamContent(new MemoryStream(bytes))
            };
        });

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://myresource.openai.azure.com/") };
        var options = new AzureOpenAiClientOptions
        {
            Endpoint = "https://myresource.openai.azure.com/",
            DeploymentName = "gpt-5",
            ApiVersion = "2025-04-01-preview",
            ApiKey = "test-key",
            TextVerbosity = "high",
            ReasoningEffort = "medium"
        };
        var client = new AzureOpenAiChatCompletionClient(httpClient, options);

        var feature = client.Features.Get<IStreamingChatFeature>()!;
        await foreach (var _ in feature.GetChatCompletionStreamAsync(new ChatCompletionRequest(
                           Messages: [new LlmMessage(LlmRole.User, "Stream")],
                           Model: "gpt-5"))) { }

        var doc = JsonDocument.Parse(capturedBody!);
        Assert.Multiple(() =>
        {
            Assert.That(capturedUri, Does.Contain("/responses"));
            Assert.That(capturedUri, Does.Not.Contain("/chat/completions"));
            Assert.That(doc.RootElement.GetProperty("stream").GetBoolean(), Is.True);
            Assert.That(doc.RootElement.GetProperty("text").GetProperty("verbosity").GetString(), Is.EqualTo("high"));
            Assert.That(doc.RootElement.GetProperty("reasoning").GetProperty("effort").GetString(), Is.EqualTo("medium"));
        });
    }

    private static readonly string[] ChatToResponsesFallbackUris =
    [
        "/openai/deployments/opaque-deployment/chat/completions?api-version=2025-04-01-preview",
        "/openai/deployments/opaque-deployment/responses?api-version=2025-04-01-preview",
        "/openai/deployments/opaque-deployment/responses?api-version=2025-04-01-preview"
    ];

    private static readonly string[] ResponsesToChatFallbackUris =
    [
        "/openai/deployments/gpt-5-misleading/responses?api-version=2025-04-01-preview",
        "/openai/deployments/gpt-5-misleading/chat/completions?api-version=2025-04-01-preview",
        "/openai/deployments/gpt-5-misleading/chat/completions?api-version=2025-04-01-preview"
    ];

    private static readonly string[] SharedRouteCacheFirstClientUris =
    [
        "/openai/deployments/shared-route-cache/chat/completions?api-version=2025-04-01-preview",
        "/openai/deployments/shared-route-cache/responses?api-version=2025-04-01-preview"
    ];

    private static readonly string[] SharedRouteCacheSecondClientUris =
    [
        "/openai/deployments/shared-route-cache/responses?api-version=2025-04-01-preview"
    ];

    private const string PersonJsonSchema =
        """{"type":"object","properties":{"name":{"type":"string"}},"required":["name"],"additionalProperties":false}""";

    private const string AzureResponseJson = """
        {
            "model": "gpt-4",
            "choices": [
                {
                    "message": {
                        "role": "assistant",
                        "content": "Hello there!"
                    }
                }
            ],
            "usage": {
                "prompt_tokens": 10,
                "completion_tokens": 20
            }
        }
        """;

    private const string AzureLengthResponseJson = """
        {
            "model": "gpt-4",
            "choices": [
                {
                    "message": {
                        "role": "assistant",
                        "content": "This"
                    },
                    "finish_reason": "length"
                }
            ],
            "usage": {
                "prompt_tokens": 12,
                "completion_tokens": 1
            }
        }
        """;

    private const string Gpt5ResponseJson = """
        {
            "id": "resp_001",
            "model": "gpt-5",
            "status": "completed",
            "output": [
                {
                    "type": "message",
                    "role": "assistant",
                    "content": [
                        {
                            "type": "output_text",
                            "text": "Hello there!"
                        }
                    ]
                }
            ],
            "usage": {
                "input_tokens": 10,
                "output_tokens": 5
            }
        }
        """;

    private const string Gpt5JsonResponseJson = """
        {
            "id": "resp_json_001",
            "model": "gpt-5",
            "status": "completed",
            "output": [
                {
                    "type": "message",
                    "role": "assistant",
                    "content": [
                        {
                            "type": "output_text",
                            "text": "{\"name\":\"John\"}"
                        }
                    ]
                }
            ],
            "usage": {
                "input_tokens": 12,
                "output_tokens": 4
            }
        }
        """;

    private const string Gpt5IncompleteResponseJson = """
        {
            "id": "resp_inc_001",
            "model": "gpt-5",
            "status": "incomplete",
            "incomplete_details": { "reason": "max_output_tokens" },
            "output": [],
            "usage": {
                "input_tokens": 12,
                "output_tokens": 1
            }
        }
        """;

    private const string Gpt5FailedResponseJson = """
        {
            "id": "resp_failed_001",
            "model": "gpt-5",
            "status": "failed",
            "output": [],
            "usage": {
                "input_tokens": 5,
                "output_tokens": 0
            }
        }
        """;

    private const string Gpt5RefusalResponseJson = """
        {
            "id": "resp_ref_001",
            "model": "gpt-5",
            "status": "completed",
            "output": [
                {
                    "type": "message",
                    "role": "assistant",
                    "content": [
                        {
                            "type": "refusal",
                            "refusal": "I cannot help with that."
                        }
                    ]
                }
            ],
            "usage": {
                "input_tokens": 8,
                "output_tokens": 0
            }
        }
        """;

    private const string Gpt5StreamSse = """
        data: {"type":"response.output_text.delta","delta":"Hello"}

        data: {"type":"response.output_text.delta","delta":", world!"}

        data: {"type":"response.completed","response":{"model":"gpt-5","status":"completed","usage":{"input_tokens":10,"output_tokens":5}}}

        data: [DONE]
        """;
}
