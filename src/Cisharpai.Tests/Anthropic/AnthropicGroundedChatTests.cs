using System.Net;
using System.Text.Json;
using Cisharpai.Features.Chat;
using Cisharpai.Models;
using Cisharpai.Anthropic;

namespace Cisharpai.Tests.Anthropic;

public sealed class AnthropicGroundedChatTests
{
    #region Response Fixtures

    private const string GroundedResponseWithCitations = """
        {
            "model": "claude-sonnet-4-20250514",
            "content": [
                {
                    "type": "text",
                    "text": "The capital of France is Paris.",
                    "citations": [
                        {
                            "type": "char_location",
                            "cited_text": "Paris is the capital of France.",
                            "document_index": 0,
                            "document_title": "doc-1",
                            "start_char_index": 0,
                            "end_char_index": 31
                        }
                    ]
                }
            ],
            "usage": {
                "input_tokens": 50,
                "output_tokens": 15
            },
            "stop_reason": "end_turn"
        }
        """;

    private const string GroundedResponseMultipleBlocks = """
        {
            "model": "claude-sonnet-4-20250514",
            "content": [
                {
                    "type": "text",
                    "text": "Paris is the capital. ",
                    "citations": [
                        {
                            "type": "char_location",
                            "cited_text": "Paris is the capital of France.",
                            "document_index": 0,
                            "document_title": "doc-1",
                            "start_char_index": 0,
                            "end_char_index": 31
                        }
                    ]
                },
                {
                    "type": "text",
                    "text": "Berlin is the capital of Germany.",
                    "citations": [
                        {
                            "type": "char_location",
                            "cited_text": "Berlin is the capital of Germany.",
                            "document_index": 1,
                            "document_title": "doc-2",
                            "start_char_index": 0,
                            "end_char_index": 33
                        }
                    ]
                }
            ],
            "usage": {
                "input_tokens": 80,
                "output_tokens": 25
            },
            "stop_reason": "end_turn"
        }
        """;

    private const string GroundedResponseNoCitations = """
        {
            "model": "claude-sonnet-4-20250514",
            "content": [
                {
                    "type": "text",
                    "text": "I don't have information about that."
                }
            ],
            "usage": {
                "input_tokens": 30,
                "output_tokens": 10
            },
            "stop_reason": "end_turn"
        }
        """;

    #endregion

    private static ChatCompletionRequest CreateRequest() =>
        new(Messages: [new LlmMessage(LlmRole.User, "What is the capital of France?")],
            Model: "claude-sonnet-4-20250514");

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

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.anthropic.com/v1/") };
        var client = new AnthropicChatCompletionClient(httpClient, new AnthropicClientOptions());

        var response = await client.GetGroundedChatCompletionAsync(
            request ?? CreateRequest(),
            options ?? CreateOptionsWithTextDocs());

        return (response, capturedBody);
    }

    #region Request Building Tests

    [Test]
    public async Task GroundedChat_InjectsDocumentBlocks_IntoUserMessageContent()
    {
        var (_, capturedBody) = await ExecuteGroundedChat(GroundedResponseWithCitations);

        Assert.That(capturedBody, Is.Not.Null);
        var doc = JsonDocument.Parse(capturedBody!);
        var messages = doc.RootElement.GetProperty("messages");
        var lastMessage = messages[messages.GetArrayLength() - 1];
        var content = lastMessage.GetProperty("content");

        Assert.That(content.ValueKind, Is.EqualTo(JsonValueKind.Array));

        var documentBlocks = content.EnumerateArray()
            .Where(b => b.GetProperty("type").GetString() == "document")
            .ToList();
        Assert.That(documentBlocks, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task GroundedChat_MapsPlainTextDocument_ToTextSource()
    {
        var (_, capturedBody) = await ExecuteGroundedChat(
            GroundedResponseWithCitations,
            options: CreateOptionsWithTextDocs());

        var doc = JsonDocument.Parse(capturedBody!);
        var messages = doc.RootElement.GetProperty("messages");
        var lastMessage = messages[messages.GetArrayLength() - 1];
        var content = lastMessage.GetProperty("content");

        var firstDocBlock = content.EnumerateArray()
            .First(b => b.GetProperty("type").GetString() == "document");

        var source = firstDocBlock.GetProperty("source");
        Assert.Multiple(() =>
        {
            Assert.That(source.GetProperty("type").GetString(), Is.EqualTo("text"));
            Assert.That(source.GetProperty("media_type").GetString(), Is.EqualTo("text/plain"));
            Assert.That(source.GetProperty("data").GetString(), Is.EqualTo("Paris is the capital of France."));
        });
    }

    [Test]
    public async Task GroundedChat_MapsKeyValueDocument_ToCustomContentSource()
    {
        var (_, capturedBody) = await ExecuteGroundedChat(
            GroundedResponseWithCitations,
            options: CreateOptionsWithKeyValueDocs());

        var doc = JsonDocument.Parse(capturedBody!);
        var messages = doc.RootElement.GetProperty("messages");
        var lastMessage = messages[messages.GetArrayLength() - 1];
        var content = lastMessage.GetProperty("content");

        var firstDocBlock = content.EnumerateArray()
            .First(b => b.GetProperty("type").GetString() == "document");

        var source = firstDocBlock.GetProperty("source");
        Assert.That(source.GetProperty("type").GetString(), Is.EqualTo("custom_content"));

        var sourceContent = source.GetProperty("content");
        Assert.That(sourceContent.GetArrayLength(), Is.EqualTo(2));

        var texts = sourceContent.EnumerateArray()
            .Select(b => b.GetProperty("text").GetString())
            .ToList();
        Assert.That(texts, Does.Contain("title: France"));
        Assert.That(texts, Does.Contain("snippet: Paris is the capital of France."));
    }

    [Test]
    public async Task GroundedChat_SetsDocumentTitle_FromDocumentChunkId()
    {
        var (_, capturedBody) = await ExecuteGroundedChat(
            GroundedResponseWithCitations,
            options: CreateOptionsWithTextDocs());

        var doc = JsonDocument.Parse(capturedBody!);
        var messages = doc.RootElement.GetProperty("messages");
        var lastMessage = messages[messages.GetArrayLength() - 1];
        var content = lastMessage.GetProperty("content");

        var firstDocBlock = content.EnumerateArray()
            .First(b => b.GetProperty("type").GetString() == "document");

        Assert.That(firstDocBlock.GetProperty("title").GetString(), Is.EqualTo("doc-1"));
    }

    [Test]
    public async Task GroundedChat_OmitsDocumentTitle_WhenIdIsNull()
    {
        var options = new GroundedChatOptions(
            Documents: [new DocumentChunk(Text: "Some text")]);

        var (_, capturedBody) = await ExecuteGroundedChat(
            GroundedResponseNoCitations,
            options: options);

        var doc = JsonDocument.Parse(capturedBody!);
        var messages = doc.RootElement.GetProperty("messages");
        var lastMessage = messages[messages.GetArrayLength() - 1];
        var content = lastMessage.GetProperty("content");

        var docBlock = content.EnumerateArray()
            .First(b => b.GetProperty("type").GetString() == "document");

        Assert.That(docBlock.TryGetProperty("title", out _), Is.False);
    }

    [Test]
    public async Task GroundedChat_SetsCitationsEnabled_OnEachDocumentBlock()
    {
        var (_, capturedBody) = await ExecuteGroundedChat(GroundedResponseWithCitations);

        var doc = JsonDocument.Parse(capturedBody!);
        var messages = doc.RootElement.GetProperty("messages");
        var lastMessage = messages[messages.GetArrayLength() - 1];
        var content = lastMessage.GetProperty("content");

        var documentBlocks = content.EnumerateArray()
            .Where(b => b.GetProperty("type").GetString() == "document")
            .ToList();

        foreach (var block in documentBlocks)
        {
            var citations = block.GetProperty("citations");
            Assert.That(citations.GetProperty("enabled").GetBoolean(), Is.True);
        }
    }

    [Test]
    public async Task GroundedChat_PostsToMessagesEndpoint()
    {
        var handler = new MockHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(GroundedResponseWithCitations, System.Text.Encoding.UTF8, "application/json")
            }));

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.anthropic.com/v1/") };
        var client = new AnthropicChatCompletionClient(httpClient, new AnthropicClientOptions());

        await client.GetGroundedChatCompletionAsync(CreateRequest(), CreateOptionsWithTextDocs());

        Assert.That(handler.LastRequest?.RequestUri?.AbsolutePath, Does.EndWith("/messages"));
    }

    [Test]
    public async Task GroundedChat_PreservesUserMessageText_AfterDocumentBlocks()
    {
        var (_, capturedBody) = await ExecuteGroundedChat(GroundedResponseWithCitations);

        var doc = JsonDocument.Parse(capturedBody!);
        var messages = doc.RootElement.GetProperty("messages");
        var lastMessage = messages[messages.GetArrayLength() - 1];
        var content = lastMessage.GetProperty("content");

        var textBlocks = content.EnumerateArray()
            .Where(b => b.GetProperty("type").GetString() == "text")
            .ToList();

        Assert.That(textBlocks, Has.Count.EqualTo(1));
        Assert.That(textBlocks[0].GetProperty("text").GetString(),
            Is.EqualTo("What is the capital of France?"));
    }

    #endregion

    #region Response Mapping Tests

    [Test]
    public async Task GroundedChat_MapsCitationsFromResponse()
    {
        var (response, _) = await ExecuteGroundedChat(GroundedResponseWithCitations);

        Assert.Multiple(() =>
        {
            Assert.That(response.IsSuccess, Is.True);
            Assert.That(response.Citations, Has.Count.EqualTo(1));
            Assert.That(response.Citations[0].Text, Is.EqualTo("The capital of France is Paris."));
        });
    }

    [Test]
    public async Task GroundedChat_CitationStartEnd_AreResponseContentOffsets()
    {
        var (response, _) = await ExecuteGroundedChat(GroundedResponseWithCitations);

        Assert.Multiple(() =>
        {
            Assert.That(response.Citations[0].Start, Is.EqualTo(0));
            Assert.That(response.Citations[0].End, Is.EqualTo("The capital of France is Paris.".Length));
        });
    }

    [Test]
    public async Task GroundedChat_MultipleTextBlocks_ComputesCumulativeOffsets()
    {
        var docs = new GroundedChatOptions(Documents:
        [
            new DocumentChunk(Id: "doc-1", Text: "Paris is the capital of France."),
            new DocumentChunk(Id: "doc-2", Text: "Berlin is the capital of Germany.")
        ]);

        var (response, _) = await ExecuteGroundedChat(GroundedResponseMultipleBlocks, options: docs);

        var firstBlockLen = "Paris is the capital. ".Length;
        var secondBlockLen = "Berlin is the capital of Germany.".Length;
        Assert.Multiple(() =>
        {
            Assert.That(response.Content, Is.EqualTo("Paris is the capital. Berlin is the capital of Germany."));
            Assert.That(response.Citations, Has.Count.EqualTo(2));

            Assert.That(response.Citations[0].Start, Is.EqualTo(0));
            Assert.That(response.Citations[0].End, Is.EqualTo(firstBlockLen));

            Assert.That(response.Citations[1].Start, Is.EqualTo(firstBlockLen));
            Assert.That(response.Citations[1].End, Is.EqualTo(firstBlockLen + secondBlockLen));
        });
    }

    [Test]
    public async Task GroundedChat_MapsCitationSource_WithDocumentId()
    {
        var (response, _) = await ExecuteGroundedChat(GroundedResponseWithCitations);

        var sources = response.Citations[0].Sources;
        Assert.Multiple(() =>
        {
            Assert.That(sources, Has.Count.EqualTo(1));
            Assert.That(sources[0].Id, Is.EqualTo("doc-1"));
        });
    }

    [Test]
    public async Task GroundedChat_MapsCitationSource_WithCitedText()
    {
        var (response, _) = await ExecuteGroundedChat(GroundedResponseWithCitations);

        Assert.That(response.Citations[0].Sources[0].CitedText,
            Is.EqualTo("Paris is the capital of France."));
    }

    [Test]
    public async Task GroundedChat_MapsCitationType()
    {
        var (response, _) = await ExecuteGroundedChat(GroundedResponseWithCitations);

        Assert.That(response.Citations[0].Type, Is.EqualTo("char_location"));
    }

    [Test]
    public async Task GroundedChat_IdRoundTrip_ViaDocumentTitle()
    {
        var docs = new GroundedChatOptions(Documents:
        [
            new DocumentChunk(Id: "my-custom-id", Text: "Paris is the capital of France.")
        ]);

        var (response, _) = await ExecuteGroundedChat(GroundedResponseWithCitations, options: docs);

        Assert.That(response.Citations[0].Sources[0].Id, Is.EqualTo("doc-1"));
    }

    [Test]
    public async Task GroundedChat_NoCitations_ReturnsEmptyList()
    {
        var (response, _) = await ExecuteGroundedChat(GroundedResponseNoCitations);

        Assert.Multiple(() =>
        {
            Assert.That(response.IsSuccess, Is.True);
            Assert.That(response.Citations, Is.Empty);
        });
    }

    [Test]
    public async Task GroundedChat_MapsContentCorrectly()
    {
        var (response, _) = await ExecuteGroundedChat(GroundedResponseWithCitations);

        Assert.That(response.Content, Is.EqualTo("The capital of France is Paris."));
    }

    [Test]
    public async Task GroundedChat_MapsTokenUsage()
    {
        var (response, _) = await ExecuteGroundedChat(GroundedResponseWithCitations);

        Assert.Multiple(() =>
        {
            Assert.That(response.ChatCompletion.PromptTokens, Is.EqualTo(50));
            Assert.That(response.ChatCompletion.CompletionTokens, Is.EqualTo(15));
        });
    }

    [Test]
    public async Task GroundedChat_MapsModel()
    {
        var (response, _) = await ExecuteGroundedChat(GroundedResponseWithCitations);

        Assert.That(response.ChatCompletion.Model, Is.EqualTo("claude-sonnet-4-20250514"));
    }

    [Test]
    public async Task GroundedChat_WithRawResponse_IncludesRawJson()
    {
        var request = new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "What is the capital of France?")],
            Model: "claude-sonnet-4-20250514",
            IncludeRawResponse: true);

        var (response, _) = await ExecuteGroundedChat(
            GroundedResponseWithCitations,
            request: request);

        Assert.Multiple(() =>
        {
            Assert.That(response.ChatCompletion.RawResponseJson, Is.Not.Null.And.Not.Empty);
            Assert.That(response.ChatCompletion.RawRequestJson, Is.Not.Null.And.Not.Empty);
        });
    }

    [Test]
    public async Task GroundedChat_KeyValueDocs_PreservesDataOnCitationSource()
    {
        const string responseWithIndex0 = """
            {
                "model": "claude-sonnet-4-20250514",
                "content": [
                    {
                        "type": "text",
                        "text": "The capital of France is Paris.",
                        "citations": [
                            {
                                "type": "char_location",
                                "cited_text": "title: France\nsnippet: Paris is the capital.",
                                "document_index": 0,
                                "document_title": "doc-1",
                                "start_char_index": 0,
                                "end_char_index": 45
                            }
                        ]
                    }
                ],
                "usage": { "input_tokens": 50, "output_tokens": 15 },
                "stop_reason": "end_turn"
            }
            """;

        var (response, _) = await ExecuteGroundedChat(responseWithIndex0, options: CreateOptionsWithKeyValueDocs());

        var data = response.Citations[0].Sources[0].Data;
        Assert.Multiple(() =>
        {
            Assert.That(data, Is.Not.Null);
            Assert.That(data!["title"], Is.EqualTo("France"));
            Assert.That(data["snippet"], Is.EqualTo("Paris is the capital of France."));
        });
    }

    #endregion

    #region Citation Mode Tests

    [Test]
    public async Task GroundedChat_CitationModeEnabled_Works()
    {
        var options = new GroundedChatOptions(
            Documents: [new DocumentChunk(Text: "Some text")],
            CitationMode: CitationMode.Enabled);

        var (response, _) = await ExecuteGroundedChat(GroundedResponseNoCitations, options: options);

        Assert.That(response.IsSuccess, Is.True);
    }

    [Test]
    public async Task GroundedChat_CitationModeFast_TreatedAsEnabled()
    {
        var options = new GroundedChatOptions(
            Documents: [new DocumentChunk(Text: "Some text")],
            CitationMode: CitationMode.Fast);

        var (response, capturedBody) = await ExecuteGroundedChat(GroundedResponseNoCitations, options: options);

        Assert.That(response.IsSuccess, Is.True);
        var doc = JsonDocument.Parse(capturedBody!);
        var messages = doc.RootElement.GetProperty("messages");
        var lastMessage = messages[messages.GetArrayLength() - 1];
        var content = lastMessage.GetProperty("content");
        var docBlock = content.EnumerateArray()
            .First(b => b.GetProperty("type").GetString() == "document");
        Assert.That(docBlock.GetProperty("citations").GetProperty("enabled").GetBoolean(), Is.True);
    }

    [Test]
    public async Task GroundedChat_CitationModeAccurate_TreatedAsEnabled()
    {
        var options = new GroundedChatOptions(
            Documents: [new DocumentChunk(Text: "Some text")],
            CitationMode: CitationMode.Accurate);

        var (response, capturedBody) = await ExecuteGroundedChat(GroundedResponseNoCitations, options: options);

        Assert.That(response.IsSuccess, Is.True);
        var doc = JsonDocument.Parse(capturedBody!);
        var messages = doc.RootElement.GetProperty("messages");
        var lastMessage = messages[messages.GetArrayLength() - 1];
        var content = lastMessage.GetProperty("content");
        var docBlock = content.EnumerateArray()
            .First(b => b.GetProperty("type").GetString() == "document");
        Assert.That(docBlock.GetProperty("citations").GetProperty("enabled").GetBoolean(), Is.True);
    }

    #endregion

    #region Error Handling Tests

    [Test]
    public async Task GroundedChat_HttpError_ReturnsError()
    {
        var handler = new MockHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)
            {
                Content = new StringContent("""{"error":{"message":"Internal error"}}""",
                    System.Text.Encoding.UTF8, "application/json")
            }));

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.anthropic.com/v1/") };
        var client = new AnthropicChatCompletionClient(httpClient, new AnthropicClientOptions());

        var response = await client.GetGroundedChatCompletionAsync(
            CreateRequest(), CreateOptionsWithTextDocs());

        Assert.Multiple(() =>
        {
            Assert.That(response.IsSuccess, Is.False);
            Assert.That(response.ErrorMessage, Is.Not.Null.And.Not.Empty);
            Assert.That(response.Citations, Is.Empty);
        });
    }

    [Test]
    public void GroundedChat_EmptyDocuments_ReturnsError()
    {
        using var httpClient = new HttpClient { BaseAddress = new Uri("https://api.anthropic.com/v1/") };
        var client = new AnthropicChatCompletionClient(httpClient, new AnthropicClientOptions());

        var options = new GroundedChatOptions(Documents: []);

        var response = client.GetGroundedChatCompletionAsync(CreateRequest(), options).Result;
        Assert.That(response.IsSuccess, Is.False);
    }

    [Test]
    public void GroundedChat_InvalidDocument_ReturnsError()
    {
        using var httpClient = new HttpClient { BaseAddress = new Uri("https://api.anthropic.com/v1/") };
        var client = new AnthropicChatCompletionClient(httpClient, new AnthropicClientOptions());

        var options = new GroundedChatOptions(
            Documents: [new DocumentChunk(Id: "doc-1")]);

        var response = client.GetGroundedChatCompletionAsync(CreateRequest(), options).Result;
        Assert.That(response.IsSuccess, Is.False);
    }

    #endregion

    #region Feature Discovery Tests

    [Test]
    public void Features_GetGroundedChatFeature_ReturnsSelf()
    {
        using var httpClient = new HttpClient { BaseAddress = new Uri("https://api.anthropic.com/v1/") };
        var client = new AnthropicChatCompletionClient(httpClient, new AnthropicClientOptions());

        var feature = client.Features.Get<IGroundedChatFeature>();

        Assert.Multiple(() =>
        {
            Assert.That(feature, Is.Not.Null);
            Assert.That(feature, Is.SameAs(client));
        });
    }

    #endregion
}
