using System.Net;
using System.Text.Json;
using Cisharpai.Features.Chat;
using Cisharpai.Models;
using Cisharpai.Azure.AzureOpenAi;

namespace Cisharpai.Tests.Azure.AzureOpenAi;

public sealed class AzureOpenAiGroundedChatTests
{
    #region Response Fixtures

    private const string GroundedResponseWithCitations = """
        {
            "id": "resp-azure-123",
            "model": "gpt-5-0513",
            "status": "completed",
            "output": [
                {
                    "type": "message",
                    "role": "assistant",
                    "content": [
                        {
                            "type": "output_text",
                            "text": "The capital of France is Paris.",
                            "annotations": [
                                {
                                    "type": "file_citation",
                                    "file_id": "file-abc",
                                    "index": 0
                                }
                            ]
                        }
                    ]
                }
            ],
            "usage": {
                "input_tokens": 100,
                "output_tokens": 20
            }
        }
        """;

    private const string GroundedResponseNoCitations = """
        {
            "id": "resp-azure-456",
            "model": "gpt-5-0513",
            "status": "completed",
            "output": [
                {
                    "type": "message",
                    "role": "assistant",
                    "content": [
                        {
                            "type": "output_text",
                            "text": "I don't have information about that."
                        }
                    ]
                }
            ],
            "usage": {
                "input_tokens": 80,
                "output_tokens": 10
            }
        }
        """;

    private const string GroundedResponseMultiBlock = """
        {
            "id": "resp-azure-multi",
            "model": "gpt-5-0513",
            "status": "completed",
            "output": [
                {
                    "type": "message",
                    "role": "assistant",
                    "content": [
                        {
                            "type": "output_text",
                            "text": "First block. ",
                            "annotations": [
                                {
                                    "type": "file_citation",
                                    "file_id": "file-abc",
                                    "index": 0
                                }
                            ]
                        },
                        {
                            "type": "output_text",
                            "text": "Second block.",
                            "annotations": [
                                {
                                    "type": "file_citation",
                                    "file_id": "file-def",
                                    "index": 1
                                }
                            ]
                        }
                    ]
                }
            ],
            "usage": {
                "input_tokens": 100,
                "output_tokens": 30
            }
        }
        """;

    #endregion

    private static AzureOpenAiClientOptions CreateGpt5Options() => new()
    {
        DeploymentName = "gpt-5-deployment",
        ApiKey = "test-key",
        ModelName = "gpt-5-0513"
    };

    private static AzureOpenAiClientOptions CreateLegacyOptions() => new()
    {
        DeploymentName = "gpt-4o-deployment",
        ApiKey = "test-key",
        ModelName = "gpt-4o"
    };

    private static ChatCompletionRequest CreateRequest() =>
        new(Messages: [new LlmMessage(LlmRole.User, "What is the capital of France?")]);

    private static ChatCompletionRequest CreateSystemOnlyRequest() =>
        new(Messages: [new LlmMessage(LlmRole.System, "You are a helpful assistant.")]);

    private static GroundedChatOptions CreateOptionsWithTextDocs() =>
        new(Documents:
        [
            new DocumentChunk(Id: "doc-1", Text: "Paris is the capital of France."),
            new DocumentChunk(Id: "doc-2", Text: "Berlin is the capital of Germany.")
        ]);

    private static GroundedChatOptions CreateOptionsWithKeyValueDocs() =>
        new(Documents:
        [
            new DocumentChunk(
                Id: "doc-1",
                Data: new Dictionary<string, string>
                {
                    ["title"] = "France",
                    ["snippet"] = "Paris is the capital of France."
                })
        ]);

    #region Feature Registration

    [Test]
    public void ExposesGroundedChatFeature()
    {
        using var httpClient = new HttpClient { BaseAddress = new Uri("https://test.openai.azure.com/") };
        var options = new AzureOpenAiClientOptions { DeploymentName = "test", ApiKey = "key" };
        var client = new AzureOpenAiChatCompletionClient(httpClient, options);

        var feature = client.Features.Get<IGroundedChatFeature>();

        Assert.Multiple(() =>
        {
            Assert.That(feature, Is.Not.Null);
            Assert.That(feature, Is.SameAs(client));
        });
    }

    #endregion

    #region Model Gate

    [Test]
    public async Task GroundedChat_ReturnsError_ForLegacyDeployment()
    {
        var handler = new MockHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(GroundedResponseNoCitations, System.Text.Encoding.UTF8, "application/json")
            }));

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://test.openai.azure.com/") };
        var client = new AzureOpenAiChatCompletionClient(httpClient, CreateLegacyOptions());

        var response = await client.GetGroundedChatCompletionAsync(
            CreateRequest(),
            CreateOptionsWithTextDocs());

        Assert.Multiple(() =>
        {
            Assert.That(response.IsSuccess, Is.False);
            Assert.That(response.ErrorMessage, Does.Contain("Responses API"));
        });
    }

    #endregion

    #region Request Building

    [Test]
    public async Task GroundedChat_SendsToResponsesEndpoint()
    {
        string? requestUri = null;
        var handler = new MockHttpMessageHandler(async (req, _) =>
        {
            requestUri = req.RequestUri?.PathAndQuery;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(GroundedResponseWithCitations, System.Text.Encoding.UTF8, "application/json")
            };
        });

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://test.openai.azure.com/") };
        var client = new AzureOpenAiChatCompletionClient(httpClient, CreateGpt5Options());

        await client.GetGroundedChatCompletionAsync(CreateRequest(), CreateOptionsWithTextDocs());

        Assert.That(requestUri, Does.Contain("responses"));
    }

    [Test]
    public async Task GroundedChat_InputFilesNestedInUserMessageContent()
    {
        string? capturedBody = null;
        var handler = new MockHttpMessageHandler(async (req, _) =>
        {
            capturedBody = await req.Content!.ReadAsStringAsync(CancellationToken.None);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(GroundedResponseWithCitations, System.Text.Encoding.UTF8, "application/json")
            };
        });

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://test.openai.azure.com/") };
        var client = new AzureOpenAiChatCompletionClient(httpClient, CreateGpt5Options());

        await client.GetGroundedChatCompletionAsync(CreateRequest(), CreateOptionsWithTextDocs());

        Assert.That(capturedBody, Is.Not.Null);
        var doc = JsonDocument.Parse(capturedBody!);
        var input = doc.RootElement.GetProperty("input");

        var userMessage = input.EnumerateArray()
            .First(e => e.TryGetProperty("role", out var r) && r.GetString() == "user");
        var content = userMessage.GetProperty("content");

        var inputFiles = content.EnumerateArray()
            .Where(e => e.TryGetProperty("type", out var t) && t.GetString() == "input_file")
            .ToList();

        Assert.That(inputFiles, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task GroundedChat_NoTopLevelInputFileItems()
    {
        string? capturedBody = null;
        var handler = new MockHttpMessageHandler(async (req, _) =>
        {
            capturedBody = await req.Content!.ReadAsStringAsync(CancellationToken.None);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(GroundedResponseWithCitations, System.Text.Encoding.UTF8, "application/json")
            };
        });

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://test.openai.azure.com/") };
        var client = new AzureOpenAiChatCompletionClient(httpClient, CreateGpt5Options());

        await client.GetGroundedChatCompletionAsync(CreateRequest(), CreateOptionsWithTextDocs());

        var doc = JsonDocument.Parse(capturedBody!);
        var input = doc.RootElement.GetProperty("input");

        var topLevelInputFiles = input.EnumerateArray()
            .Where(e => e.TryGetProperty("type", out var t) && t.GetString() == "input_file")
            .ToList();

        Assert.That(topLevelInputFiles, Is.Empty);
    }

    #endregion

    #region Response Mapping

    [Test]
    public async Task GroundedChat_MapsResponseContent()
    {
        var handler = new MockHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(GroundedResponseWithCitations, System.Text.Encoding.UTF8, "application/json")
            }));

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://test.openai.azure.com/") };
        var client = new AzureOpenAiChatCompletionClient(httpClient, CreateGpt5Options());

        var response = await client.GetGroundedChatCompletionAsync(
            CreateRequest(),
            CreateOptionsWithTextDocs());

        Assert.Multiple(() =>
        {
            Assert.That(response.IsSuccess, Is.True);
            Assert.That(response.Content, Is.EqualTo("The capital of France is Paris."));
        });
    }

    [Test]
    public async Task GroundedChat_MapsCitationsFromAnnotations()
    {
        var handler = new MockHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(GroundedResponseWithCitations, System.Text.Encoding.UTF8, "application/json")
            }));

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://test.openai.azure.com/") };
        var client = new AzureOpenAiChatCompletionClient(httpClient, CreateGpt5Options());

        var response = await client.GetGroundedChatCompletionAsync(
            CreateRequest(),
            CreateOptionsWithTextDocs());

        Assert.That(response.Citations, Has.Count.EqualTo(1));
        var citation = response.Citations[0];
        Assert.Multiple(() =>
        {
            Assert.That(citation.Text, Is.Empty, "file_citation has no character offsets; text should be empty");
            Assert.That(citation.Start, Is.EqualTo(0));
            Assert.That(citation.End, Is.EqualTo(0));
            Assert.That(citation.Sources[0].Id, Is.EqualTo("doc-1"));
        });
    }

    [Test]
    public async Task GroundedChat_PopulatesSourceData_ForStructuredDocuments()
    {
        var handler = new MockHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(GroundedResponseWithCitations, System.Text.Encoding.UTF8, "application/json")
            }));

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://test.openai.azure.com/") };
        var client = new AzureOpenAiChatCompletionClient(httpClient, CreateGpt5Options());

        var response = await client.GetGroundedChatCompletionAsync(
            CreateRequest(),
            CreateOptionsWithKeyValueDocs());

        var source = response.Citations[0].Sources[0];
        Assert.Multiple(() =>
        {
            Assert.That(source.Data, Is.Not.Null);
            Assert.That(source.Data!["title"], Is.EqualTo("France"));
            Assert.That(source.Data["snippet"], Is.EqualTo("Paris is the capital of France."));
        });
    }

    [Test]
    public async Task GroundedChat_EmptyCitations_WhenNoAnnotations()
    {
        var handler = new MockHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(GroundedResponseNoCitations, System.Text.Encoding.UTF8, "application/json")
            }));

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://test.openai.azure.com/") };
        var client = new AzureOpenAiChatCompletionClient(httpClient, CreateGpt5Options());

        var response = await client.GetGroundedChatCompletionAsync(
            CreateRequest(),
            CreateOptionsWithTextDocs());

        Assert.Multiple(() =>
        {
            Assert.That(response.IsSuccess, Is.True);
            Assert.That(response.Citations, Is.Empty);
        });
    }

    #endregion

    #region Route Fallback

    [Test]
    public async Task GroundedChat_ReturnsError_WhenFallsBackToChatCompletions()
    {
        var handler = new MockHttpMessageHandler((req, _) =>
        {
            if (req.RequestUri?.PathAndQuery.Contains("responses") == true)
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)
                {
                    Content = new StringContent("""{"error": {"message": "Resource not found"}}""")
                });
            }
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(GroundedResponseNoCitations, System.Text.Encoding.UTF8, "application/json")
            });
        });

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://test.openai.azure.com/") };
        var client = new AzureOpenAiChatCompletionClient(httpClient, CreateGpt5Options());

        var response = await client.GetGroundedChatCompletionAsync(
            CreateRequest(),
            CreateOptionsWithTextDocs());

        Assert.Multiple(() =>
        {
            Assert.That(response.IsSuccess, Is.False);
            Assert.That(response.ErrorMessage, Does.Contain("Responses API"));
        });
    }

    [Test]
    public async Task GroundedChat_FallbackPreservesRawResponseBody()
    {
        const string errorBody = """{"error": {"message": "Resource not found"}}""";
        var handler = new MockHttpMessageHandler((req, _) =>
        {
            if (req.RequestUri?.PathAndQuery.Contains("responses") == true)
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)
                {
                    Content = new StringContent(errorBody)
                });
            }
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(GroundedResponseNoCitations, System.Text.Encoding.UTF8, "application/json")
            });
        });

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://test.openai.azure.com/") };
        var client = new AzureOpenAiChatCompletionClient(httpClient, CreateGpt5Options());

        var response = await client.GetGroundedChatCompletionAsync(
            CreateRequest(),
            CreateOptionsWithTextDocs());

        Assert.That(response.ChatCompletion.RawResponseJson, Is.Not.Null);
    }

    [Test]
    public async Task GroundedChat_FallbackPersistsRoutingMode_SubsequentCallsReturnError()
    {
        var callCount = 0;
        var handler = new MockHttpMessageHandler((req, _) =>
        {
            callCount++;
            if (req.RequestUri?.PathAndQuery.Contains("responses") == true)
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)
                {
                    Content = new StringContent("""{"error": {"message": "Resource not found"}}""")
                });
            }
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(GroundedResponseNoCitations, System.Text.Encoding.UTF8, "application/json")
            });
        });

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://test.openai.azure.com/") };
        var client = new AzureOpenAiChatCompletionClient(httpClient, CreateGpt5Options());

        var firstResponse = await client.GetGroundedChatCompletionAsync(
            CreateRequest(), CreateOptionsWithTextDocs());
        Assert.That(firstResponse.IsSuccess, Is.False);

        var secondResponse = await client.GetGroundedChatCompletionAsync(
            CreateRequest(), CreateOptionsWithTextDocs());

        Assert.Multiple(() =>
        {
            Assert.That(secondResponse.IsSuccess, Is.False);
            Assert.That(secondResponse.ErrorMessage, Does.Contain("Responses API"));
            Assert.That(callCount, Is.EqualTo(1),
                "Second call should not re-probe the Responses API endpoint");
        });
    }

    #endregion

    #region Validation

    [Test]
    public async Task GroundedChat_ReturnsError_WhenNoDocuments()
    {
        var handler = new MockHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(GroundedResponseNoCitations, System.Text.Encoding.UTF8, "application/json")
            }));

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://test.openai.azure.com/") };
        var client = new AzureOpenAiChatCompletionClient(httpClient, CreateGpt5Options());

        var response = await client.GetGroundedChatCompletionAsync(
            CreateRequest(),
            new GroundedChatOptions(Documents: []));

        Assert.Multiple(() =>
        {
            Assert.That(response.IsSuccess, Is.False);
            Assert.That(response.ErrorMessage, Does.Contain("document"));
        });
    }

    [Test]
    public async Task GroundedChat_ReturnsError_WhenNoUserMessage()
    {
        var handler = new MockHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(GroundedResponseNoCitations, System.Text.Encoding.UTF8, "application/json")
            }));

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://test.openai.azure.com/") };
        var client = new AzureOpenAiChatCompletionClient(httpClient, CreateGpt5Options());

        var response = await client.GetGroundedChatCompletionAsync(
            CreateSystemOnlyRequest(),
            CreateOptionsWithTextDocs());

        Assert.Multiple(() =>
        {
            Assert.That(response.IsSuccess, Is.False);
            Assert.That(response.ErrorMessage, Does.Contain("user message"));
        });
    }

    #endregion

    #region GPT-5 gate bypass (Issue 1)

    [Test]
    public async Task GroundedChat_ReturnsError_WhenRoutingCachedAsResponsesApi_ButModelIsNotGpt5()
    {
        var callCount = 0;
        var handler = new MockHttpMessageHandler((req, _) =>
        {
            callCount++;
            if (req.RequestUri?.PathAndQuery.Contains("responses") == true)
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(GroundedResponseWithCitations, System.Text.Encoding.UTF8, "application/json")
                });
            }
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"id":"chatcmpl-1","model":"gpt-4o","choices":[{"index":0,"message":{"role":"assistant","content":"Hello"},"finish_reason":"stop"}],"usage":{"prompt_tokens":10,"completion_tokens":5}}""", System.Text.Encoding.UTF8, "application/json")
            });
        });

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://test.openai.azure.com/") };
        var legacyOptions = new AzureOpenAiClientOptions
        {
            DeploymentName = "gpt-4o-deployment",
            ApiKey = "test-key",
            ModelName = "gpt-4o"
        };
        var client = new AzureOpenAiChatCompletionClient(httpClient, legacyOptions);

        await client.GetChatCompletionAsync(
            new ChatCompletionRequest(Messages: [new LlmMessage(LlmRole.User, "Hi")]));

        var groundedResponse = await client.GetGroundedChatCompletionAsync(
            CreateRequest(),
            CreateOptionsWithTextDocs());

        Assert.Multiple(() =>
        {
            Assert.That(groundedResponse.IsSuccess, Is.False);
            Assert.That(groundedResponse.ErrorMessage, Does.Contain("GPT-5"));
        });
    }

    #endregion

    #region Multi-block response (Issue 2)

    [Test]
    public async Task GroundedChat_MultiBlock_ConcatenatesAllOutputTextBlocks()
    {
        var handler = new MockHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(GroundedResponseMultiBlock, System.Text.Encoding.UTF8, "application/json")
            }));

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://test.openai.azure.com/") };
        var client = new AzureOpenAiChatCompletionClient(httpClient, CreateGpt5Options());

        var response = await client.GetGroundedChatCompletionAsync(
            CreateRequest(),
            CreateOptionsWithTextDocs());

        Assert.Multiple(() =>
        {
            Assert.That(response.IsSuccess, Is.True);
            Assert.That(response.Content, Is.EqualTo("First block. Second block."));
        });
    }

    [Test]
    public async Task GroundedChat_MultiBlock_CollectsCitationsFromAllBlocks()
    {
        var handler = new MockHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(GroundedResponseMultiBlock, System.Text.Encoding.UTF8, "application/json")
            }));

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://test.openai.azure.com/") };
        var client = new AzureOpenAiChatCompletionClient(httpClient, CreateGpt5Options());

        var response = await client.GetGroundedChatCompletionAsync(
            CreateRequest(),
            CreateOptionsWithTextDocs());

        Assert.That(response.Citations, Has.Count.EqualTo(2));
        Assert.Multiple(() =>
        {
            Assert.That(response.Citations[0].Sources[0].Id, Is.EqualTo("doc-1"));
            Assert.That(response.Citations[1].Sources[0].Id, Is.EqualTo("doc-2"));
        });
    }

    #endregion

    #region Data cloning and id-less document lookup (Issues 5-6)

    [Test]
    public async Task GroundedChat_IdLessDocument_PopulatesSourceData()
    {
        var handler = new MockHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(GroundedResponseWithCitations, System.Text.Encoding.UTF8, "application/json")
            }));

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://test.openai.azure.com/") };
        var client = new AzureOpenAiChatCompletionClient(httpClient, CreateGpt5Options());

        var options = new GroundedChatOptions(
            Documents:
            [
                new DocumentChunk(
                    Data: new Dictionary<string, string>
                    {
                        ["title"] = "France",
                        ["snippet"] = "Paris is the capital."
                    })
            ]);

        var response = await client.GetGroundedChatCompletionAsync(CreateRequest(), options);

        var source = response.Citations[0].Sources[0];
        Assert.Multiple(() =>
        {
            Assert.That(source.Data, Is.Not.Null);
            Assert.That(source.Data!["title"], Is.EqualTo("France"));
        });
    }

    [Test]
    public async Task GroundedChat_SourceData_IsImmutableCopy()
    {
        var handler = new MockHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(GroundedResponseWithCitations, System.Text.Encoding.UTF8, "application/json")
            }));

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://test.openai.azure.com/") };
        var client = new AzureOpenAiChatCompletionClient(httpClient, CreateGpt5Options());

        var mutableData = new Dictionary<string, string>
        {
            ["title"] = "France",
            ["snippet"] = "Paris is the capital."
        };
        var options = new GroundedChatOptions(
            Documents: [new DocumentChunk(Id: "doc-1", Data: mutableData)]);

        var response = await client.GetGroundedChatCompletionAsync(CreateRequest(), options);

        mutableData["title"] = "MUTATED";

        var source = response.Citations[0].Sources[0];
        Assert.That(source.Data!["title"], Is.EqualTo("France"),
            "CitationSource.Data should be a defensive copy, not alias the caller's dictionary");
    }

    #endregion
}
