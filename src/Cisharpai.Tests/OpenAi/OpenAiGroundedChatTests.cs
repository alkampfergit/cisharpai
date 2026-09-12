using System.Net;
using System.Text.Json;
using Cisharpai.Features.Chat;
using Cisharpai.Models;
using Cisharpai.OpenAi;

namespace Cisharpai.Tests.OpenAi;

public sealed class OpenAiGroundedChatTests
{
    #region Response Fixtures

    private const string GroundedResponseWithCitations = """
        {
            "id": "resp-abc123",
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
            "id": "resp-abc456",
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

    private const string GroundedResponseMultipleCitations = """
        {
            "id": "resp-abc789",
            "model": "gpt-5-0513",
            "status": "completed",
            "output": [
                {
                    "type": "message",
                    "role": "assistant",
                    "content": [
                        {
                            "type": "output_text",
                            "text": "Paris is the capital of France and Berlin is the capital of Germany.",
                            "annotations": [
                                {
                                    "type": "file_citation",
                                    "file_id": "file-1",
                                    "filename": "doc-1",
                                    "start_index": 0,
                                    "end_index": 5
                                },
                                {
                                    "type": "file_citation",
                                    "file_id": "file-2",
                                    "filename": "doc-2",
                                    "start_index": 40,
                                    "end_index": 46
                                }
                            ]
                        }
                    ]
                }
            ],
            "usage": {
                "input_tokens": 150,
                "output_tokens": 25
            }
        }
        """;

    private const string GroundedResponseFailed = """
        {
            "id": "resp-fail",
            "model": "gpt-5-0513",
            "status": "failed",
            "output": [],
            "usage": {
                "input_tokens": 0,
                "output_tokens": 0
            }
        }
        """;

    #endregion

    private static ChatCompletionRequest CreateGpt5Request() =>
        new(Messages: [new LlmMessage(LlmRole.User, "What is the capital of France?")],
            Model: "gpt-5-0513");

    private static ChatCompletionRequest CreateLegacyRequest() =>
        new(Messages: [new LlmMessage(LlmRole.User, "What is the capital of France?")],
            Model: "gpt-4o");

    private static ChatCompletionRequest CreateReasoningRequest() =>
        new(Messages: [new LlmMessage(LlmRole.User, "What is the capital of France?")],
            Model: "o4-mini");

    private static GroundedChatOptions CreateOptionsWithKeyValueDocs() =>
        new(Documents:
        [
            new DocumentChunk(
                Id: "doc-1",
                Data: new Dictionary<string, string>
                {
                    ["title"] = "France",
                    ["snippet"] = "Paris is the capital of France."
                }),
            new DocumentChunk(
                Id: "doc-2",
                Data: new Dictionary<string, string>
                {
                    ["title"] = "Germany",
                    ["snippet"] = "Berlin is the capital of Germany."
                })
        ]);

    private static GroundedChatOptions CreateOptionsWithTextDocs() =>
        new(Documents:
        [
            new DocumentChunk(Id: "doc-1", Text: "Paris is the capital of France."),
            new DocumentChunk(Id: "doc-2", Text: "Berlin is the capital of Germany.")
        ]);

    private static async Task<(GroundedChatCompletionResponse response, string? capturedBody)> ExecuteGroundedChat(
        string responseJson,
        GroundedChatOptions? options = null,
        ChatCompletionRequest? request = null)
    {
        string? capturedBody = null;
        var handler = new MockHttpMessageHandler(async (req, _) =>
        {
            capturedBody = await req.Content!.ReadAsStringAsync(CancellationToken.None);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson, System.Text.Encoding.UTF8, "application/json")
            };
        });

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.openai.com/v1/") };
        var client = new OpenAiChatCompletionClient(httpClient, new OpenAiClientOptions());

        var response = await client.GetGroundedChatCompletionAsync(
            request ?? CreateGpt5Request(),
            options ?? CreateOptionsWithKeyValueDocs());

        return (response, capturedBody);
    }

    #region Feature Registration

    [Test]
    public void ExposesGroundedChatFeature()
    {
        using var httpClient = new HttpClient { BaseAddress = new Uri("https://api.openai.com/v1/") };
        var client = new OpenAiChatCompletionClient(httpClient, new OpenAiClientOptions());

        var feature = client.Features.Get<IGroundedChatFeature>();

        Assert.Multiple(() =>
        {
            Assert.That(feature, Is.Not.Null);
            Assert.That(feature, Is.SameAs(client));
        });
    }

    #endregion

    #region Model Gate — non-GPT-5 returns error

    [Test]
    public async Task GroundedChat_ReturnsError_ForLegacyModel()
    {
        var response = (await ExecuteGroundedChat(
            GroundedResponseNoCitations,
            request: CreateLegacyRequest())).response;

        Assert.Multiple(() =>
        {
            Assert.That(response.IsSuccess, Is.False);
            Assert.That(response.ErrorMessage, Does.Contain("Responses API"));
            Assert.That(response.ErrorMessage, Does.Contain("gpt-4o"));
        });
    }

    [Test]
    public async Task GroundedChat_ReturnsError_ForReasoningModel()
    {
        var response = (await ExecuteGroundedChat(
            GroundedResponseNoCitations,
            request: CreateReasoningRequest())).response;

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

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.openai.com/v1/") };
        var client = new OpenAiChatCompletionClient(httpClient, new OpenAiClientOptions());

        await client.GetGroundedChatCompletionAsync(CreateGpt5Request(), CreateOptionsWithTextDocs());

        Assert.That(requestUri, Does.Contain("responses"));
    }

    [Test]
    public async Task GroundedChat_IncludesInputFileItems()
    {
        var (_, capturedBody) = await ExecuteGroundedChat(GroundedResponseWithCitations);

        Assert.That(capturedBody, Is.Not.Null);
        var doc = JsonDocument.Parse(capturedBody!);
        var input = doc.RootElement.GetProperty("input");

        var inputFiles = input.EnumerateArray()
            .Where(e => e.TryGetProperty("type", out var t) && t.GetString() == "input_file")
            .ToList();

        Assert.That(inputFiles, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task GroundedChat_InputFileHasCorrectFilename()
    {
        var (_, capturedBody) = await ExecuteGroundedChat(GroundedResponseWithCitations);

        var doc = JsonDocument.Parse(capturedBody!);
        var input = doc.RootElement.GetProperty("input");
        var firstFile = input.EnumerateArray()
            .First(e => e.TryGetProperty("type", out var t) && t.GetString() == "input_file");

        Assert.That(firstFile.GetProperty("filename").GetString(), Is.EqualTo("doc-1"));
    }

    [Test]
    public async Task GroundedChat_InputFileHasBase64Data()
    {
        var (_, capturedBody) = await ExecuteGroundedChat(
            GroundedResponseWithCitations,
            options: CreateOptionsWithTextDocs());

        var doc = JsonDocument.Parse(capturedBody!);
        var input = doc.RootElement.GetProperty("input");
        var firstFile = input.EnumerateArray()
            .First(e => e.TryGetProperty("type", out var t) && t.GetString() == "input_file");

        var fileData = firstFile.GetProperty("file_data").GetString();
        Assert.That(fileData, Does.StartWith("data:text/plain;base64,"));

        var base64 = fileData!["data:text/plain;base64,".Length..];
        var decoded = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(base64));
        Assert.That(decoded, Is.EqualTo("Paris is the capital of France."));
    }

    [Test]
    public async Task GroundedChat_StructuredDocData_SerializedAsJson()
    {
        var (_, capturedBody) = await ExecuteGroundedChat(GroundedResponseWithCitations);

        var doc = JsonDocument.Parse(capturedBody!);
        var input = doc.RootElement.GetProperty("input");
        var firstFile = input.EnumerateArray()
            .First(e => e.TryGetProperty("type", out var t) && t.GetString() == "input_file");

        var fileData = firstFile.GetProperty("file_data").GetString()!;
        var base64 = fileData["data:text/plain;base64,".Length..];
        var decoded = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(base64));

        var parsedData = JsonDocument.Parse(decoded);
        Assert.Multiple(() =>
        {
            Assert.That(parsedData.RootElement.GetProperty("title").GetString(), Is.EqualTo("France"));
            Assert.That(parsedData.RootElement.GetProperty("snippet").GetString(), Is.EqualTo("Paris is the capital of France."));
        });
    }

    [Test]
    public async Task GroundedChat_InputFilesAppearBeforeMessages()
    {
        var (_, capturedBody) = await ExecuteGroundedChat(GroundedResponseWithCitations);

        var doc = JsonDocument.Parse(capturedBody!);
        var input = doc.RootElement.GetProperty("input");
        var items = input.EnumerateArray().ToList();

        var firstFileIndex = items.FindIndex(e => e.TryGetProperty("type", out var t) && t.GetString() == "input_file");
        var firstMessageIndex = items.FindIndex(e => e.TryGetProperty("role", out _));

        Assert.That(firstFileIndex, Is.LessThan(firstMessageIndex));
    }

    [Test]
    public async Task GroundedChat_GeneratesFilename_WhenDocIdIsNull()
    {
        var options = new GroundedChatOptions(
            Documents: [new DocumentChunk(Text: "Some content")]);

        var (_, capturedBody) = await ExecuteGroundedChat(
            GroundedResponseNoCitations,
            options: options);

        var doc = JsonDocument.Parse(capturedBody!);
        var input = doc.RootElement.GetProperty("input");
        var file = input.EnumerateArray()
            .First(e => e.TryGetProperty("type", out var t) && t.GetString() == "input_file");

        Assert.That(file.GetProperty("filename").GetString(), Is.EqualTo("document_0.txt"));
    }

    #endregion

    #region Response Mapping

    [Test]
    public async Task GroundedChat_MapsResponseContent()
    {
        var (response, _) = await ExecuteGroundedChat(GroundedResponseWithCitations);

        Assert.Multiple(() =>
        {
            Assert.That(response.IsSuccess, Is.True);
            Assert.That(response.Content, Is.EqualTo("The capital of France is Paris."));
        });
    }

    [Test]
    public async Task GroundedChat_MapsCitationsFromAnnotations()
    {
        var (response, _) = await ExecuteGroundedChat(GroundedResponseWithCitations);

        Assert.That(response.Citations, Has.Count.EqualTo(1));

        var citation = response.Citations[0];
        Assert.Multiple(() =>
        {
            Assert.That(citation.Start, Is.EqualTo(25));
            Assert.That(citation.End, Is.EqualTo(30));
            Assert.That(citation.Text, Is.EqualTo("Paris"));
            Assert.That(citation.Type, Is.EqualTo("file_citation"));
            Assert.That(citation.Sources, Has.Count.EqualTo(1));
            Assert.That(citation.Sources[0].Id, Is.EqualTo("doc-1"));
        });
    }

    [Test]
    public async Task GroundedChat_MapsMultipleCitations()
    {
        var (response, _) = await ExecuteGroundedChat(GroundedResponseMultipleCitations);

        Assert.That(response.Citations, Has.Count.EqualTo(2));
        Assert.Multiple(() =>
        {
            Assert.That(response.Citations[0].Sources[0].Id, Is.EqualTo("doc-1"));
            Assert.That(response.Citations[1].Sources[0].Id, Is.EqualTo("doc-2"));
        });
    }

    [Test]
    public async Task GroundedChat_EmptyCitations_WhenNoAnnotations()
    {
        var (response, _) = await ExecuteGroundedChat(GroundedResponseNoCitations);

        Assert.Multiple(() =>
        {
            Assert.That(response.IsSuccess, Is.True);
            Assert.That(response.Citations, Is.Empty);
        });
    }

    [Test]
    public async Task GroundedChat_PreservesDocumentChunkId_InCitationSource()
    {
        var (response, _) = await ExecuteGroundedChat(GroundedResponseWithCitations);

        Assert.That(response.Citations[0].Sources[0].Id, Is.EqualTo("doc-1"));
    }

    [Test]
    public async Task GroundedChat_MapsFailedStatus_ToError()
    {
        var (response, _) = await ExecuteGroundedChat(GroundedResponseFailed);

        Assert.Multiple(() =>
        {
            Assert.That(response.IsSuccess, Is.False);
            Assert.That(response.ErrorMessage, Does.Contain("failed"));
        });
    }

    #endregion

    #region Raw Response

    [Test]
    public async Task GroundedChat_IncludesRawResponse_WhenRequested()
    {
        var request = CreateGpt5Request() with { IncludeRawResponse = true };
        var (response, _) = await ExecuteGroundedChat(
            GroundedResponseWithCitations,
            request: request);

        Assert.Multiple(() =>
        {
            Assert.That(response.ChatCompletion.RawResponseJson, Is.Not.Null);
            Assert.That(response.ChatCompletion.RawRequestJson, Is.Not.Null);
        });
    }

    #endregion

    #region Validation

    [Test]
    public async Task GroundedChat_ReturnsError_WhenNoDocuments()
    {
        var options = new GroundedChatOptions(Documents: []);

        var (response, _) = await ExecuteGroundedChat(
            GroundedResponseNoCitations,
            options: options);

        Assert.Multiple(() =>
        {
            Assert.That(response.IsSuccess, Is.False);
            Assert.That(response.ErrorMessage, Does.Contain("document"));
        });
    }

    [Test]
    public async Task GroundedChat_ReturnsError_OnHttpFailure()
    {
        var handler = new MockHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)
            {
                Content = new StringContent("""{"error": {"message": "Server error"}}""")
            }));

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.openai.com/v1/") };
        var client = new OpenAiChatCompletionClient(httpClient, new OpenAiClientOptions());

        var response = await client.GetGroundedChatCompletionAsync(
            CreateGpt5Request(),
            CreateOptionsWithKeyValueDocs());

        Assert.That(response.IsSuccess, Is.False);
    }

    #endregion
}
