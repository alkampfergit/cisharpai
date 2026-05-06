using System.Net;
using System.Text.Json;
using Cisharpai.Features.Chat;
using Cisharpai.Models;
using Cisharpai.Cohere;

namespace Cisharpai.Tests.Cohere;

public sealed class CohereGroundedChatTests
{
    #region Response Fixtures

    private const string GroundedResponseWithCitations = """
        {
            "id": "abc-123",
            "finish_reason": "COMPLETE",
            "message": {
                "role": "assistant",
                "content": [
                    {
                        "type": "text",
                        "text": "The capital of France is Paris."
                    }
                ],
                "citations": [
                    {
                        "start": 27,
                        "end": 32,
                        "text": "Paris",
                        "sources": [
                            {
                                "type": "document",
                                "id": "doc-1",
                                "document": {
                                    "title": "France",
                                    "snippet": "Paris is the capital of France."
                                }
                            }
                        ],
                        "type": "TEXT_CONTENT"
                    }
                ]
            },
            "usage": {
                "billed_units": {
                    "input_tokens": 50,
                    "output_tokens": 15
                },
                "tokens": {
                    "input_tokens": 300,
                    "output_tokens": 15
                }
            }
        }
        """;

    private const string GroundedResponseNoCitations = """
        {
            "id": "abc-456",
            "finish_reason": "COMPLETE",
            "message": {
                "role": "assistant",
                "content": [
                    {
                        "type": "text",
                        "text": "I don't have information about that."
                    }
                ]
            },
            "usage": {
                "billed_units": {
                    "input_tokens": 30,
                    "output_tokens": 10
                },
                "tokens": {
                    "input_tokens": 200,
                    "output_tokens": 10
                }
            }
        }
        """;

    #endregion

    private static ChatCompletionRequest CreateRequest() =>
        new(Messages: [new LlmMessage(LlmRole.User, "What is the capital of France?")],
            Model: "command-a-03-2025");

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
            new DocumentChunk(Text: "Berlin is the capital of Germany.")
        ]);

    private (MockHttpMessageHandler handler, CohereChatCompletionClient client) CreateClientWithResponse(string responseJson)
    {
        string? capturedBody = null;
        var handler = new MockHttpMessageHandler(async (request, _) =>
        {
            capturedBody = await request.Content!.ReadAsStringAsync(CancellationToken.None);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson, System.Text.Encoding.UTF8, "application/json")
            };
        });
        CapturedBody = null;
        _capturedBodyAccessor = () => capturedBody;

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.cohere.com/v2/") };
        var client = new CohereChatCompletionClient(httpClient, new CohereClientOptions());
        return (handler, client);
    }

    private Func<string?>? _capturedBodyAccessor;
    private string? CapturedBody { get; set; }

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

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.cohere.com/v2/") };
        var client = new CohereChatCompletionClient(httpClient, new CohereClientOptions());

        var response = await client.GetGroundedChatCompletionAsync(
            request ?? CreateRequest(),
            options ?? CreateOptionsWithKeyValueDocs());

        return (response, capturedBody);
    }

    #region Request Building Tests

    [Test]
    public async Task GroundedChat_SetsDocumentsArray()
    {
        var (_, capturedBody) = await ExecuteGroundedChat(GroundedResponseWithCitations);

        Assert.That(capturedBody, Is.Not.Null);
        var doc = JsonDocument.Parse(capturedBody!);
        Assert.Multiple(() =>
        {
            Assert.That(doc.RootElement.TryGetProperty("documents", out var documents), Is.True);
            Assert.That(documents.ValueKind, Is.EqualTo(JsonValueKind.Array));
            Assert.That(documents.GetArrayLength(), Is.EqualTo(2));
        });
    }

    [Test]
    public async Task GroundedChat_MapsKeyValueDocumentCorrectly()
    {
        var (_, capturedBody) = await ExecuteGroundedChat(GroundedResponseWithCitations);

        var doc = JsonDocument.Parse(capturedBody!);
        var firstDoc = doc.RootElement.GetProperty("documents")[0];
        Assert.That(firstDoc.GetProperty("id").GetString(), Is.EqualTo("doc-1"));

        var data = firstDoc.GetProperty("data");
        Assert.Multiple(() =>
        {
            Assert.That(data.ValueKind, Is.EqualTo(JsonValueKind.Object));
            Assert.That(data.GetProperty("title").GetString(), Is.EqualTo("France"));
            Assert.That(data.GetProperty("snippet").GetString(), Is.EqualTo("Paris is the capital of France."));
        });
    }

    [Test]
    public async Task GroundedChat_MapsPlainTextDocumentCorrectly()
    {
        var (_, capturedBody) = await ExecuteGroundedChat(
            GroundedResponseWithCitations,
            options: CreateOptionsWithTextDocs());

        var doc = JsonDocument.Parse(capturedBody!);
        var firstDoc = doc.RootElement.GetProperty("documents")[0];
        Assert.That(firstDoc.GetProperty("id").GetString(), Is.EqualTo("doc-1"));

        var data = firstDoc.GetProperty("data");
        Assert.Multiple(() =>
        {
            Assert.That(data.ValueKind, Is.EqualTo(JsonValueKind.String));
            Assert.That(data.GetString(), Is.EqualTo("Paris is the capital of France."));
        });
    }

    [Test]
    public async Task GroundedChat_OmitsDocumentId_WhenNull()
    {
        var options = new GroundedChatOptions(
            Documents: [new DocumentChunk(Text: "Some text")]);

        var (_, capturedBody) = await ExecuteGroundedChat(
            GroundedResponseNoCitations,
            options: options);

        var doc = JsonDocument.Parse(capturedBody!);
        var firstDoc = doc.RootElement.GetProperty("documents")[0];
        Assert.That(firstDoc.TryGetProperty("id", out _), Is.False);
    }

    [Test]
    public async Task GroundedChat_SetsCitationOptionsMode_Accurate()
    {
        var (_, capturedBody) = await ExecuteGroundedChat(
            GroundedResponseWithCitations,
            options: new GroundedChatOptions(
                Documents: [new DocumentChunk(Text: "Some text")],
                CitationMode: CitationMode.Accurate));

        var doc = JsonDocument.Parse(capturedBody!);
        var citationOptions = doc.RootElement.GetProperty("citation_options");
        Assert.That(citationOptions.GetProperty("mode").GetString(), Is.EqualTo("ACCURATE"));
    }

    [Test]
    public async Task GroundedChat_SetsCitationOptionsMode_Fast()
    {
        var (_, capturedBody) = await ExecuteGroundedChat(
            GroundedResponseWithCitations,
            options: new GroundedChatOptions(
                Documents: [new DocumentChunk(Text: "Some text")],
                CitationMode: CitationMode.Fast));

        var doc = JsonDocument.Parse(capturedBody!);
        var citationOptions = doc.RootElement.GetProperty("citation_options");
        Assert.That(citationOptions.GetProperty("mode").GetString(), Is.EqualTo("FAST"));
    }

    [Test]
    public async Task GroundedChat_SetsCitationOptionsMode_Enabled()
    {
        var (_, capturedBody) = await ExecuteGroundedChat(
            GroundedResponseWithCitations,
            options: new GroundedChatOptions(
                Documents: [new DocumentChunk(Text: "Some text")],
                CitationMode: CitationMode.Enabled));

        var doc = JsonDocument.Parse(capturedBody!);
        var citationOptions = doc.RootElement.GetProperty("citation_options");
        Assert.That(citationOptions.GetProperty("mode").GetString(), Is.EqualTo("ENABLED"));
    }

    [Test]
    public async Task GroundedChat_DoesNotSetResponseFormat()
    {
        var (_, capturedBody) = await ExecuteGroundedChat(GroundedResponseWithCitations);

        var doc = JsonDocument.Parse(capturedBody!);
        Assert.That(doc.RootElement.TryGetProperty("response_format", out _), Is.False);
    }

    [Test]
    public async Task GroundedChat_UsesSnakeCaseNaming()
    {
        var (_, capturedBody) = await ExecuteGroundedChat(GroundedResponseWithCitations);

        var doc = JsonDocument.Parse(capturedBody!);
        // Verify snake_case: citation_options not citationOptions
        Assert.Multiple(() =>
        {
            Assert.That(doc.RootElement.TryGetProperty("citation_options", out _), Is.True);
            Assert.That(doc.RootElement.TryGetProperty("citationOptions", out _), Is.False);
            // max_tokens not maxTokens
            Assert.That(doc.RootElement.TryGetProperty("maxTokens", out _), Is.False);
        });
    }

    [Test]
    public async Task GroundedChat_PostsToChatEndpoint()
    {
        var handler = new MockHttpMessageHandler(async (req, _) =>
        {
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(GroundedResponseWithCitations, System.Text.Encoding.UTF8, "application/json")
            };
        });

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.cohere.com/v2/") };
        var client = new CohereChatCompletionClient(httpClient, new CohereClientOptions());

        await client.GetGroundedChatCompletionAsync(CreateRequest(), CreateOptionsWithKeyValueDocs());

        Assert.That(handler.LastRequest?.RequestUri?.AbsolutePath, Does.EndWith("/chat"));
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
            Assert.That(response.Citations[0].Start, Is.EqualTo(27));
            Assert.That(response.Citations[0].End, Is.EqualTo(32));
            Assert.That(response.Citations[0].Text, Is.EqualTo("Paris"));
        });
    }

    [Test]
    public async Task GroundedChat_MapsCitationSources()
    {
        var (response, _) = await ExecuteGroundedChat(GroundedResponseWithCitations);

        var sources = response.Citations[0].Sources;
        Assert.Multiple(() =>
        {
            Assert.That(sources, Has.Count.EqualTo(1));
            Assert.That(sources[0].Id, Is.EqualTo("doc-1"));
            Assert.That(sources[0].Data, Is.Not.Null);
            Assert.That(sources[0].Data!["title"], Is.EqualTo("France"));
            Assert.That(sources[0].Data!["snippet"], Is.EqualTo("Paris is the capital of France."));
        });
    }

    [Test]
    public async Task GroundedChat_MapsCitationType()
    {
        var (response, _) = await ExecuteGroundedChat(GroundedResponseWithCitations);

        Assert.That(response.Citations[0].Type, Is.EqualTo("TEXT_CONTENT"));
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
            Assert.That(response.ChatCompletion.PromptTokens, Is.EqualTo(300));
            Assert.That(response.ChatCompletion.CompletionTokens, Is.EqualTo(15));
        });
    }

    [Test]
    public async Task GroundedChat_MapsModelFromRequest()
    {
        var (response, _) = await ExecuteGroundedChat(GroundedResponseWithCitations);

        Assert.That(response.ChatCompletion.Model, Is.EqualTo("command-a-03-2025"));
    }

    [Test]
    public async Task GroundedChat_WithRawResponse_IncludesRawJson()
    {
        var request = new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "What is the capital of France?")],
            Model: "command-a-03-2025",
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

    #endregion

    #region Error Handling Tests

    [Test]
    public async Task GroundedChat_HttpError_ReturnsError()
    {
        var handler = new MockHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)
            {
                Content = new StringContent("""{"message":"Internal error"}""", System.Text.Encoding.UTF8, "application/json")
            }));

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.cohere.com/v2/") };
        var client = new CohereChatCompletionClient(httpClient, new CohereClientOptions());

        var response = await client.GetGroundedChatCompletionAsync(
            CreateRequest(), CreateOptionsWithKeyValueDocs());

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
        using var httpClient = new HttpClient { BaseAddress = new Uri("https://api.cohere.com/v2/") };
        var client = new CohereChatCompletionClient(httpClient, new CohereClientOptions());

        var options = new GroundedChatOptions(Documents: []);

        // The ArgumentException is caught internally and returned as an error response
        var response = client.GetGroundedChatCompletionAsync(CreateRequest(), options).Result;
        Assert.That(response.IsSuccess, Is.False);
    }

    [Test]
    public void GroundedChat_InvalidDocument_ReturnsError()
    {
        using var httpClient = new HttpClient { BaseAddress = new Uri("https://api.cohere.com/v2/") };
        var client = new CohereChatCompletionClient(httpClient, new CohereClientOptions());

        var options = new GroundedChatOptions(
            Documents: [new DocumentChunk(Id: "doc-1")]); // No Data or Text

        var response = client.GetGroundedChatCompletionAsync(CreateRequest(), options).Result;
        Assert.That(response.IsSuccess, Is.False);
    }

    #endregion

    #region Feature Discovery Tests

    [Test]
    public void Features_GetGroundedChatFeature_ReturnsSelf()
    {
        using var httpClient = new HttpClient { BaseAddress = new Uri("https://api.cohere.com/v2/") };
        var client = new CohereChatCompletionClient(httpClient, new CohereClientOptions());

        var feature = client.Features.Get<IGroundedChatFeature>();

        Assert.Multiple(() =>
        {
            Assert.That(feature, Is.Not.Null);
            Assert.That(feature, Is.SameAs(client));
        });
    }

    #endregion

    #region Multiple Citations Tests

    [Test]
    public async Task GroundedChat_CitationTypeNull_WhenNotProvided()
    {
        const string responseWithoutCitationType = """
            {
                "id": "abc-no-type",
                "finish_reason": "COMPLETE",
                "message": {
                    "role": "assistant",
                    "content": [{ "type": "text", "text": "Paris is the capital." }],
                    "citations": [
                        {
                            "start": 0,
                            "end": 5,
                            "text": "Paris",
                            "sources": [{ "type": "document", "id": "doc-1" }]
                        }
                    ]
                },
                "usage": {
                    "billed_units": { "input_tokens": 10, "output_tokens": 5 },
                    "tokens": { "input_tokens": 10, "output_tokens": 5 }
                }
            }
            """;

        var (response, _) = await ExecuteGroundedChat(responseWithoutCitationType);

        Assert.That(response.Citations[0].Type, Is.Null);
    }

    [Test]
    public async Task GroundedChat_MultipleCitations_AllMapped()
    {
        const string responseWithMultipleCitations = """
            {
                "id": "abc-789",
                "finish_reason": "COMPLETE",
                "message": {
                    "role": "assistant",
                    "content": [
                        {
                            "type": "text",
                            "text": "Paris is in France and Berlin is in Germany."
                        }
                    ],
                    "citations": [
                        {
                            "start": 0,
                            "end": 5,
                            "text": "Paris",
                            "sources": [
                                {
                                    "type": "document",
                                    "id": "doc-1",
                                    "document": {
                                        "title": "France"
                                    }
                                }
                            ],
                            "type": "TEXT_CONTENT"
                        },
                        {
                            "start": 25,
                            "end": 31,
                            "text": "Berlin",
                            "sources": [
                                {
                                    "type": "document",
                                    "id": "doc-2",
                                    "document": {
                                        "title": "Germany"
                                    }
                                }
                            ],
                            "type": "TEXT_CONTENT"
                        }
                    ]
                },
                "usage": {
                    "billed_units": { "input_tokens": 50, "output_tokens": 20 },
                    "tokens": { "input_tokens": 300, "output_tokens": 20 }
                }
            }
            """;

        var (response, _) = await ExecuteGroundedChat(responseWithMultipleCitations);

        Assert.Multiple(() =>
        {
            Assert.That(response.Citations, Has.Count.EqualTo(2));
            Assert.That(response.Citations[0].Text, Is.EqualTo("Paris"));
            Assert.That(response.Citations[0].Sources[0].Id, Is.EqualTo("doc-1"));
            Assert.That(response.Citations[1].Text, Is.EqualTo("Berlin"));
            Assert.That(response.Citations[1].Sources[0].Id, Is.EqualTo("doc-2"));
        });
    }

    #endregion
}
