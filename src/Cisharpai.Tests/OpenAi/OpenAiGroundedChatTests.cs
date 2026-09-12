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

    private const string GroundedResponseWithLegacyAnnotations = """
        {
            "id": "resp-abc123-legacy",
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
                                    "file_id": "file-abc",
                                    "index": 0
                                },
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
                "input_tokens": 150,
                "output_tokens": 25
            }
        }
        """;

    private const string GroundedResponseWithOpaqueFileId = """
        {
            "id": "resp-opaque",
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
                                    "file_id": "file-opaque-abc123",
                                    "index": 99
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

    private const string GroundedResponseMultiBlock = """
        {
            "id": "resp-multi",
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

    private const string GroundedResponseNegativeIndex = """
        {
            "id": "resp-neg",
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
                                    "start_index": -1,
                                    "end_index": 5
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

    private static ChatCompletionRequest CreateSystemOnlyRequest() =>
        new(Messages: [new LlmMessage(LlmRole.System, "You are a helpful assistant.")],
            Model: "gpt-5-0513");

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
    public async Task GroundedChat_InputFilesNestedInUserMessageContent()
    {
        var (_, capturedBody) = await ExecuteGroundedChat(GroundedResponseWithCitations);

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
        var (_, capturedBody) = await ExecuteGroundedChat(GroundedResponseWithCitations);

        var doc = JsonDocument.Parse(capturedBody!);
        var input = doc.RootElement.GetProperty("input");

        var topLevelInputFiles = input.EnumerateArray()
            .Where(e => e.TryGetProperty("type", out var t) && t.GetString() == "input_file")
            .ToList();

        Assert.That(topLevelInputFiles, Is.Empty);
    }

    [Test]
    public async Task GroundedChat_InputFileHasCorrectFilename()
    {
        var (_, capturedBody) = await ExecuteGroundedChat(GroundedResponseWithCitations);

        var doc = JsonDocument.Parse(capturedBody!);
        var input = doc.RootElement.GetProperty("input");
        var userMessage = input.EnumerateArray()
            .First(e => e.TryGetProperty("role", out var r) && r.GetString() == "user");
        var firstFile = userMessage.GetProperty("content").EnumerateArray()
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
        var userMessage = input.EnumerateArray()
            .First(e => e.TryGetProperty("role", out var r) && r.GetString() == "user");
        var firstFile = userMessage.GetProperty("content").EnumerateArray()
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
        var userMessage = input.EnumerateArray()
            .First(e => e.TryGetProperty("role", out var r) && r.GetString() == "user");
        var firstFile = userMessage.GetProperty("content").EnumerateArray()
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
    public async Task GroundedChat_InputFilesAppearBeforeTextInContent()
    {
        var (_, capturedBody) = await ExecuteGroundedChat(GroundedResponseWithCitations);

        var doc = JsonDocument.Parse(capturedBody!);
        var input = doc.RootElement.GetProperty("input");
        var userMessage = input.EnumerateArray()
            .First(e => e.TryGetProperty("role", out var r) && r.GetString() == "user");
        var contentItems = userMessage.GetProperty("content").EnumerateArray().ToList();

        var firstFileIndex = contentItems.FindIndex(e => e.TryGetProperty("type", out var t) && t.GetString() == "input_file");
        var textIndex = contentItems.FindIndex(e => e.TryGetProperty("type", out var t) && t.GetString() == "input_text");

        Assert.That(firstFileIndex, Is.LessThan(textIndex));
    }

    [Test]
    public async Task GroundedChat_UserMessageTextConvertedToInputText()
    {
        var (_, capturedBody) = await ExecuteGroundedChat(GroundedResponseWithCitations);

        var doc = JsonDocument.Parse(capturedBody!);
        var input = doc.RootElement.GetProperty("input");
        var userMessage = input.EnumerateArray()
            .First(e => e.TryGetProperty("role", out var r) && r.GetString() == "user");
        var textPart = userMessage.GetProperty("content").EnumerateArray()
            .First(e => e.TryGetProperty("type", out var t) && t.GetString() == "input_text");

        Assert.That(textPart.GetProperty("text").GetString(), Is.EqualTo("What is the capital of France?"));
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
        var userMessage = input.EnumerateArray()
            .First(e => e.TryGetProperty("role", out var r) && r.GetString() == "user");
        var file = userMessage.GetProperty("content").EnumerateArray()
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
    public async Task GroundedChat_MapsCitationsFromAnnotations_IndexResolvesDocument()
    {
        var (response, _) = await ExecuteGroundedChat(GroundedResponseWithCitations);

        Assert.That(response.Citations, Has.Count.EqualTo(1));

        var citation = response.Citations[0];
        Assert.Multiple(() =>
        {
            Assert.That(citation.Text, Is.Empty, "file_citation has no character offsets; text should be empty");
            Assert.That(citation.Start, Is.EqualTo(0));
            Assert.That(citation.End, Is.EqualTo(0));
            Assert.That(citation.Type, Is.EqualTo("file_citation"));
            Assert.That(citation.Sources, Has.Count.EqualTo(1));
            Assert.That(citation.Sources[0].Id, Is.EqualTo("doc-1"));
        });
    }

    [Test]
    public async Task GroundedChat_MapsCitations_WithLegacyOffsets()
    {
        var (response, _) = await ExecuteGroundedChat(GroundedResponseWithLegacyAnnotations);

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
    public async Task GroundedChat_MapsCitations_OpaqueFileIdFallsBackToRawId()
    {
        var (response, _) = await ExecuteGroundedChat(GroundedResponseWithOpaqueFileId);

        Assert.That(response.Citations, Has.Count.EqualTo(1));

        var citation = response.Citations[0];
        Assert.Multiple(() =>
        {
            Assert.That(citation.Text, Is.Empty);
            Assert.That(citation.Sources[0].Id, Is.EqualTo("file-opaque-abc123"));
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
    public async Task GroundedChat_PopulatesSourceData_ForStructuredDocuments()
    {
        var (response, _) = await ExecuteGroundedChat(GroundedResponseWithCitations);

        var source = response.Citations[0].Sources[0];
        Assert.Multiple(() =>
        {
            Assert.That(source.Data, Is.Not.Null);
            Assert.That(source.Data!["title"], Is.EqualTo("France"));
            Assert.That(source.Data["snippet"], Is.EqualTo("Paris is the capital of France."));
        });
    }

    [Test]
    public async Task GroundedChat_SourceDataIsNull_ForTextDocuments()
    {
        var (response, _) = await ExecuteGroundedChat(
            GroundedResponseWithCitations,
            options: CreateOptionsWithTextDocs());

        var source = response.Citations[0].Sources[0];
        Assert.That(source.Data, Is.Null);
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
    public async Task GroundedChat_ReturnsError_WhenNoUserMessage()
    {
        var (response, _) = await ExecuteGroundedChat(
            GroundedResponseNoCitations,
            request: CreateSystemOnlyRequest());

        Assert.Multiple(() =>
        {
            Assert.That(response.IsSuccess, Is.False);
            Assert.That(response.ErrorMessage, Does.Contain("user message"));
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

    #region Multi-block response (Issue 2)

    [Test]
    public async Task GroundedChat_MultiBlock_ConcatenatesAllOutputTextBlocks()
    {
        var (response, _) = await ExecuteGroundedChat(GroundedResponseMultiBlock);

        Assert.Multiple(() =>
        {
            Assert.That(response.IsSuccess, Is.True);
            Assert.That(response.Content, Is.EqualTo("First block. Second block."));
        });
    }

    [Test]
    public async Task GroundedChat_MultiBlock_CollectsCitationsFromAllBlocks()
    {
        var (response, _) = await ExecuteGroundedChat(GroundedResponseMultiBlock);

        Assert.That(response.Citations, Has.Count.EqualTo(2));
        Assert.Multiple(() =>
        {
            Assert.That(response.Citations[0].Sources[0].Id, Is.EqualTo("doc-1"));
            Assert.That(response.Citations[1].Sources[0].Id, Is.EqualTo("doc-2"));
        });
    }

    #endregion

    #region Content parts conversion (Issue 3)

    [Test]
    public async Task GroundedChat_ContentParts_ConvertedToResponsesApiFormat()
    {
        var request = new ChatCompletionRequest(
            Messages: [LlmMessage.WithBase64Image("What is this?", "dGVzdA==", "image/png")],
            Model: "gpt-5-0513");

        var (_, capturedBody) = await ExecuteGroundedChat(
            GroundedResponseNoCitations,
            request: request);

        var doc = JsonDocument.Parse(capturedBody!);
        var input = doc.RootElement.GetProperty("input");
        var userMessage = input.EnumerateArray()
            .First(e => e.TryGetProperty("role", out var r) && r.GetString() == "user");
        var content = userMessage.GetProperty("content");

        var types = content.EnumerateArray()
            .Select(e => e.GetProperty("type").GetString())
            .ToList();

        Assert.Multiple(() =>
        {
            Assert.That(types, Does.Not.Contain("text"), "Chat Completions 'text' type should be converted");
            Assert.That(types, Does.Not.Contain("image_url"), "Chat Completions 'image_url' type should be converted");
            Assert.That(types, Does.Contain("input_text"));
            Assert.That(types, Does.Contain("input_image"));
        });
    }

    #endregion

    #region Negative index guard (Issue 4)

    [Test]
    public async Task GroundedChat_NegativeStartIndex_DoesNotThrow()
    {
        var (response, _) = await ExecuteGroundedChat(GroundedResponseNegativeIndex);

        Assert.Multiple(() =>
        {
            Assert.That(response.IsSuccess, Is.True);
            Assert.That(response.Citations, Has.Count.EqualTo(1));
            Assert.That(response.Citations[0].Text, Is.Empty);
            Assert.That(response.Citations[0].Start, Is.EqualTo(0));
            Assert.That(response.Citations[0].End, Is.EqualTo(0));
        });
    }

    #endregion

    #region Data cloning and id-less document lookup (Issues 5-6)

    [Test]
    public async Task GroundedChat_IdLessDocument_PopulatesSourceData()
    {
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

        var (response, _) = await ExecuteGroundedChat(GroundedResponseWithCitations, options: options);

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
        var mutableData = new Dictionary<string, string>
        {
            ["title"] = "France",
            ["snippet"] = "Paris is the capital."
        };
        var options = new GroundedChatOptions(
            Documents: [new DocumentChunk(Id: "doc-1", Data: mutableData)]);

        var (response, _) = await ExecuteGroundedChat(GroundedResponseWithCitations, options: options);

        mutableData["title"] = "MUTATED";

        var source = response.Citations[0].Sources[0];
        Assert.That(source.Data!["title"], Is.EqualTo("France"),
            "CitationSource.Data should be a defensive copy, not alias the caller's dictionary");
    }

    #endregion
}
