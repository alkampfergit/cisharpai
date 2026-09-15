using System.Net;
using System.Text.Json;
using Cisharpai.Features.Chat;
using Cisharpai.Models;
using Cisharpai.OpenAi;

namespace Cisharpai.Tests.OpenAi;

public sealed class OpenAiWebSearchTests
{
    #region Response Fixtures

    private const string WebSearchResponseWithCitations = """
        {
            "id": "resp_abc123",
            "model": "gpt-5-0",
            "status": "completed",
            "output": [
                {
                    "type": "web_search_call",
                    "id": "ws_abc123",
                    "status": "completed"
                },
                {
                    "type": "message",
                    "role": "assistant",
                    "content": [
                        {
                            "type": "output_text",
                            "text": "The capital of France is Paris.",
                            "annotations": [
                                {
                                    "type": "url_citation",
                                    "url": "https://en.wikipedia.org/wiki/Paris",
                                    "title": "Paris - Wikipedia",
                                    "start_index": 0,
                                    "end_index": 30
                                }
                            ]
                        }
                    ]
                }
            ],
            "usage": {
                "input_tokens": 50,
                "output_tokens": 15
            }
        }
        """;

    private const string WebSearchResponseNoCitations = """
        {
            "id": "resp_abc456",
            "model": "gpt-5-0",
            "status": "completed",
            "output": [
                {
                    "type": "message",
                    "role": "assistant",
                    "content": [
                        {
                            "type": "output_text",
                            "text": "I can answer that from my training data."
                        }
                    ]
                }
            ],
            "usage": {
                "input_tokens": 30,
                "output_tokens": 10
            }
        }
        """;

    private const string WebSearchResponseMultipleSearches = """
        {
            "id": "resp_abc789",
            "model": "gpt-5-0",
            "status": "completed",
            "output": [
                {
                    "type": "web_search_call",
                    "id": "ws_1",
                    "status": "completed"
                },
                {
                    "type": "web_search_call",
                    "id": "ws_2",
                    "status": "completed"
                },
                {
                    "type": "message",
                    "role": "assistant",
                    "content": [
                        {
                            "type": "output_text",
                            "text": "Paris is the capital of France. Berlin is the capital of Germany.",
                            "annotations": [
                                {
                                    "type": "url_citation",
                                    "url": "https://example.com/france",
                                    "title": "France",
                                    "start_index": 0,
                                    "end_index": 30
                                },
                                {
                                    "type": "url_citation",
                                    "url": "https://example.com/germany",
                                    "title": "Germany",
                                    "start_index": 31,
                                    "end_index": 63
                                }
                            ]
                        }
                    ]
                }
            ],
            "usage": {
                "input_tokens": 80,
                "output_tokens": 25
            }
        }
        """;

    #endregion

    private static ChatCompletionRequest CreateGpt5Request() =>
        new(Messages: [new LlmMessage(LlmRole.User, "What is the capital of France?")],
            Model: "gpt-5-0");

    private static async Task<(GroundedChatCompletionResponse response, string? capturedBody)> ExecuteWebSearch(
        string responseJson,
        ChatCompletionRequest? request = null,
        WebSearchOptions? webSearchOptions = null)
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

        var response = await client.GetChatCompletionWithWebSearchAsync(
            request ?? CreateGpt5Request(),
            webSearchOptions ?? new WebSearchOptions());

        return (response, capturedBody);
    }

    #region Request Building Tests

    [Test]
    public async Task WebSearch_InjectsWebSearchTool_IntoRequest()
    {
        var (_, capturedBody) = await ExecuteWebSearch(WebSearchResponseWithCitations);

        Assert.That(capturedBody, Is.Not.Null);
        var doc = JsonDocument.Parse(capturedBody!);
        var tools = doc.RootElement.GetProperty("tools");
        Assert.That(tools.GetArrayLength(), Is.EqualTo(1));

        var tool = tools[0];
        Assert.That(tool.GetProperty("type").GetString(), Is.EqualTo("web_search"));
    }

    [Test]
    public async Task WebSearch_EnabledFalse_DoesNotInjectWebSearchTool()
    {
        var (_, capturedBody) = await ExecuteWebSearch(
            WebSearchResponseNoCitations,
            webSearchOptions: new WebSearchOptions { Enabled = false });

        Assert.That(capturedBody, Is.Not.Null);
        var doc = JsonDocument.Parse(capturedBody!);
        var hasTools = doc.RootElement.TryGetProperty("tools", out var tools)
            && tools.ValueKind == JsonValueKind.Array
            && tools.GetArrayLength() > 0;
        Assert.That(hasTools, Is.False,
            "tools should be null/absent when Enabled = false");
    }

    [Test]
    public async Task WebSearch_PostsToResponsesEndpoint()
    {
        var handler = new MockHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(WebSearchResponseWithCitations, System.Text.Encoding.UTF8, "application/json")
            }));

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.openai.com/v1/") };
        var client = new OpenAiChatCompletionClient(httpClient, new OpenAiClientOptions());

        await client.GetChatCompletionWithWebSearchAsync(CreateGpt5Request(), new WebSearchOptions());

        Assert.That(handler.LastRequest?.RequestUri?.AbsolutePath, Does.EndWith("/responses"));
    }

    [Test]
    public async Task WebSearch_NonGpt5Model_ReturnsError()
    {
        var request = new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "test")],
            Model: "gpt-4o");

        var handler = new MockHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.openai.com/v1/") };
        var client = new OpenAiChatCompletionClient(httpClient, new OpenAiClientOptions());

        var response = await client.GetChatCompletionWithWebSearchAsync(
            request, new WebSearchOptions());

        Assert.Multiple(() =>
        {
            Assert.That(response.IsSuccess, Is.False);
            Assert.That(response.ErrorMessage, Does.Contain("Responses API"));
            Assert.That(response.ErrorMessage, Does.Contain("GPT-5"));
        });
    }

    #endregion

    #region Response Mapping Tests

    [Test]
    public async Task WebSearch_MapsCitationsFromResponse()
    {
        var (response, _) = await ExecuteWebSearch(WebSearchResponseWithCitations);

        Assert.Multiple(() =>
        {
            Assert.That(response.IsSuccess, Is.True);
            Assert.That(response.Citations, Has.Count.EqualTo(1));
        });
    }

    [Test]
    public async Task WebSearch_CitationSourceId_IsUrl()
    {
        var (response, _) = await ExecuteWebSearch(WebSearchResponseWithCitations);

        Assert.That(response.Citations[0].Sources[0].Id,
            Is.EqualTo("https://en.wikipedia.org/wiki/Paris"));
    }

    [Test]
    public async Task WebSearch_CitationSourceData_ContainsTitle()
    {
        var (response, _) = await ExecuteWebSearch(WebSearchResponseWithCitations);

        var data = response.Citations[0].Sources[0].Data;
        Assert.Multiple(() =>
        {
            Assert.That(data, Is.Not.Null);
            Assert.That(data!["title"], Is.EqualTo("Paris - Wikipedia"));
        });
    }

    [Test]
    public async Task WebSearch_CitationType_IsUrlCitation()
    {
        var (response, _) = await ExecuteWebSearch(WebSearchResponseWithCitations);

        Assert.That(response.Citations[0].Type, Is.EqualTo("url_citation"));
    }

    [Test]
    public async Task WebSearch_MapsWebSearchCount()
    {
        var (response, _) = await ExecuteWebSearch(WebSearchResponseWithCitations);

        Assert.That(response.ChatCompletion.WebSearchCount, Is.EqualTo(1));
    }

    [Test]
    public async Task WebSearch_MultipleSearchCalls_MapsCount()
    {
        var (response, _) = await ExecuteWebSearch(WebSearchResponseMultipleSearches);

        Assert.That(response.ChatCompletion.WebSearchCount, Is.EqualTo(2));
    }

    [Test]
    public async Task WebSearch_NoSearchCalls_WebSearchCountIsNull()
    {
        var (response, _) = await ExecuteWebSearch(WebSearchResponseNoCitations);

        Assert.That(response.ChatCompletion.WebSearchCount, Is.Null);
    }

    [Test]
    public async Task WebSearch_GroundingKind_IsWebSearch()
    {
        var (response, _) = await ExecuteWebSearch(WebSearchResponseWithCitations);

        Assert.That(response.GroundingKind, Is.EqualTo(GroundingKind.WebSearch));
    }

    [Test]
    public async Task WebSearch_NoCitations_ReturnsEmptyList()
    {
        var (response, _) = await ExecuteWebSearch(WebSearchResponseNoCitations);

        Assert.Multiple(() =>
        {
            Assert.That(response.IsSuccess, Is.True);
            Assert.That(response.Citations, Is.Empty);
        });
    }

    [Test]
    public async Task WebSearch_MultipleCitations_AllMapped()
    {
        var (response, _) = await ExecuteWebSearch(WebSearchResponseMultipleSearches);

        Assert.Multiple(() =>
        {
            Assert.That(response.Citations, Has.Count.EqualTo(2));
            Assert.That(response.Citations[0].Sources[0].Id, Is.EqualTo("https://example.com/france"));
            Assert.That(response.Citations[1].Sources[0].Id, Is.EqualTo("https://example.com/germany"));
        });
    }

    [Test]
    public async Task WebSearch_MapsContent()
    {
        var (response, _) = await ExecuteWebSearch(WebSearchResponseWithCitations);

        Assert.That(response.Content, Is.EqualTo("The capital of France is Paris."));
    }

    [Test]
    public async Task WebSearch_MapsModel()
    {
        var (response, _) = await ExecuteWebSearch(WebSearchResponseWithCitations);

        Assert.That(response.ChatCompletion.Model, Is.EqualTo("gpt-5-0"));
    }

    [Test]
    public async Task WebSearch_Citation_TextSlice_MatchesStartEnd()
    {
        var (response, _) = await ExecuteWebSearch(WebSearchResponseWithCitations);

        var citation = response.Citations[0];
        var expected = "The capital of France is Paris.";
        Assert.Multiple(() =>
        {
            Assert.That(citation.Start, Is.EqualTo(0));
            Assert.That(citation.End, Is.EqualTo(30));
            Assert.That(citation.Text, Is.EqualTo(expected[..30]));
        });
    }

    [Test]
    public async Task WebSearch_InvalidAnnotationOffsets_NormalizesToEmptySpan()
    {
        const string responseWithInvalidOffsets = """
            {
                "id": "resp_invalid",
                "model": "gpt-5-0",
                "status": "completed",
                "output": [
                    {
                        "type": "web_search_call",
                        "id": "ws_1",
                        "status": "completed"
                    },
                    {
                        "type": "message",
                        "role": "assistant",
                        "content": [
                            {
                                "type": "output_text",
                                "text": "Short.",
                                "annotations": [
                                    {
                                        "type": "url_citation",
                                        "url": "https://example.com",
                                        "title": "Example",
                                        "start_index": 0,
                                        "end_index": 999
                                    }
                                ]
                            }
                        ]
                    }
                ],
                "usage": { "input_tokens": 10, "output_tokens": 5 }
            }
            """;

        var (response, _) = await ExecuteWebSearch(responseWithInvalidOffsets);

        var citation = response.Citations[0];
        Assert.Multiple(() =>
        {
            Assert.That(citation.Start, Is.EqualTo(0));
            Assert.That(citation.End, Is.EqualTo(0));
            Assert.That(citation.Text, Is.EqualTo(string.Empty));
        });
    }

    [Test]
    public async Task WebSearch_BothOffsetsNull_NormalizesToEmptySpan()
    {
        const string responseWithNullOffsets = """
            {
                "id": "resp_null_offsets",
                "model": "gpt-5-0",
                "status": "completed",
                "output": [
                    {
                        "type": "web_search_call",
                        "id": "ws_1",
                        "status": "completed"
                    },
                    {
                        "type": "message",
                        "role": "assistant",
                        "content": [
                            {
                                "type": "output_text",
                                "text": "The capital of France is Paris.",
                                "annotations": [
                                    {
                                        "type": "url_citation",
                                        "url": "https://example.com",
                                        "title": "Example"
                                    }
                                ]
                            }
                        ]
                    }
                ],
                "usage": { "input_tokens": 10, "output_tokens": 5 }
            }
            """;

        var (response, _) = await ExecuteWebSearch(responseWithNullOffsets);

        var citation = response.Citations[0];
        Assert.Multiple(() =>
        {
            Assert.That(citation.Start, Is.EqualTo(0));
            Assert.That(citation.End, Is.EqualTo(0));
            Assert.That(citation.Text, Is.EqualTo(string.Empty));
        });
    }

    [Test]
    public async Task WebSearch_FailedSearchCall_ReportsFailure()
    {
        const string responseWithFailedSearch = """
            {
                "id": "resp_mixed",
                "model": "gpt-5-0",
                "status": "completed",
                "output": [
                    {
                        "type": "web_search_call",
                        "id": "ws_ok",
                        "status": "completed"
                    },
                    {
                        "type": "web_search_call",
                        "id": "ws_fail",
                        "status": "failed"
                    },
                    {
                        "type": "message",
                        "role": "assistant",
                        "content": [
                            {
                                "type": "output_text",
                                "text": "Partial answer from the successful search.",
                                "annotations": [
                                    {
                                        "type": "url_citation",
                                        "url": "https://example.com/ok",
                                        "title": "OK Result",
                                        "start_index": 0,
                                        "end_index": 14
                                    }
                                ]
                            }
                        ]
                    }
                ],
                "usage": { "input_tokens": 50, "output_tokens": 15 }
            }
            """;

        var (response, _) = await ExecuteWebSearch(responseWithFailedSearch);

        Assert.Multiple(() =>
        {
            Assert.That(response.IsSuccess, Is.False);
            Assert.That(response.ErrorMessage, Does.Contain("1 of 2"));
            Assert.That(response.ErrorMessage, Does.Contain("ws_fail"));
            Assert.That(response.ChatCompletion.WebSearchCount, Is.EqualTo(2));
            Assert.That(response.Content, Is.EqualTo("Partial answer from the successful search."));
            Assert.That(response.Citations, Has.Count.EqualTo(1));
        });
    }

    #endregion

    #region Error Handling Tests

    [Test]
    public async Task WebSearch_HttpError_ReturnsError()
    {
        var handler = new MockHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)
            {
                Content = new StringContent("""{"error":{"message":"Internal error"}}""",
                    System.Text.Encoding.UTF8, "application/json")
            }));

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.openai.com/v1/") };
        var client = new OpenAiChatCompletionClient(httpClient, new OpenAiClientOptions());

        var response = await client.GetChatCompletionWithWebSearchAsync(
            CreateGpt5Request(), new WebSearchOptions());

        Assert.Multiple(() =>
        {
            Assert.That(response.IsSuccess, Is.False);
            Assert.That(response.ErrorMessage, Is.Not.Null.And.Not.Empty);
            Assert.That(response.Citations, Is.Empty);
        });
    }

    #endregion

    #region Feature Discovery Tests

    [Test]
    public void Features_GetWebSearchFeature_ReturnsSelf()
    {
        using var httpClient = new HttpClient { BaseAddress = new Uri("https://api.openai.com/v1/") };
        var client = new OpenAiChatCompletionClient(httpClient, new OpenAiClientOptions());

        var feature = client.Features.Get<IWebSearchFeature>();

        Assert.Multiple(() =>
        {
            Assert.That(feature, Is.Not.Null);
            Assert.That(feature, Is.SameAs(client));
        });
    }

    #endregion
}
