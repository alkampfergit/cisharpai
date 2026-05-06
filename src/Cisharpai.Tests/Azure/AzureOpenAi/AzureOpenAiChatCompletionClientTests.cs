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
    public async Task GetChatCompletionAsync_OpaqueDeploymentName_WithModelFamilyGpt5_RoutesToResponsesApi()
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
            ModelFamily = "gpt-5"
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
    public async Task GetChatCompletionAsync_OpaqueDeploymentName_WithoutModelFamily_FallsBackToChatCompletions()
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
    public async Task GetChatCompletionAsync_ModelFamily_OverridesDeploymentNameHint()
    {
        // Deployment name suggests gpt-5 but ModelFamily explicitly says legacy gpt-4o
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
            ModelFamily = "gpt-4o"
        };
        var client = new AzureOpenAiChatCompletionClient(httpClient, options);

        await client.GetChatCompletionAsync(new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "Hello")],
            Model: "gpt-5-misnamed"));

        Assert.That(capturedUri, Does.Contain("/chat/completions"));
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
