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
                                    "filename": "doc-1",
                                    "start_index": 25,
                                    "end_index": 30
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
    public async Task GroundedChat_IncludesInputFileItems()
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

        var inputFiles = input.EnumerateArray()
            .Where(e => e.TryGetProperty("type", out var t) && t.GetString() == "input_file")
            .ToList();

        Assert.That(inputFiles, Has.Count.EqualTo(2));
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
            Assert.That(citation.Start, Is.EqualTo(25));
            Assert.That(citation.End, Is.EqualTo(30));
            Assert.That(citation.Text, Is.EqualTo("Paris"));
            Assert.That(citation.Sources[0].Id, Is.EqualTo("doc-1"));
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

    #endregion
}
