using System.Net;
using System.Text.Json;
using Cisharpai.Features.Chat;
using Cisharpai.Models;
using Cisharpai.Anthropic;

namespace Cisharpai.Tests.Anthropic;

public sealed class AnthropicWebSearchTests
{
    #region Response Fixtures

    private const string WebSearchResponseWithCitations = """
        {
            "model": "claude-sonnet-4-20250514",
            "content": [
                {
                    "type": "text",
                    "text": "The capital of France is Paris.",
                    "citations": [
                        {
                            "type": "web_search_result_location",
                            "cited_text": "Paris is the capital and most populous city of France.",
                            "url": "https://en.wikipedia.org/wiki/Paris",
                            "title": "Paris - Wikipedia",
                            "start_char_index": 0,
                            "end_char_index": 54
                        }
                    ]
                }
            ],
            "usage": {
                "input_tokens": 50,
                "output_tokens": 15,
                "server_tool_use": {
                    "web_search_requests": 1
                }
            },
            "stop_reason": "end_turn"
        }
        """;

    private const string WebSearchResponseNoCitations = """
        {
            "model": "claude-sonnet-4-20250514",
            "content": [
                {
                    "type": "text",
                    "text": "I can answer that from my training data."
                }
            ],
            "usage": {
                "input_tokens": 30,
                "output_tokens": 10,
                "server_tool_use": {
                    "web_search_requests": 0
                }
            },
            "stop_reason": "end_turn"
        }
        """;

    private const string WebSearchResponseMultipleSearches = """
        {
            "model": "claude-sonnet-4-20250514",
            "content": [
                {
                    "type": "text",
                    "text": "Paris is the capital of France. Berlin is the capital of Germany.",
                    "citations": [
                        {
                            "type": "web_search_result_location",
                            "cited_text": "Paris is the capital of France.",
                            "url": "https://example.com/france",
                            "title": "France",
                            "start_char_index": 0,
                            "end_char_index": 31
                        },
                        {
                            "type": "web_search_result_location",
                            "cited_text": "Berlin is the capital of Germany.",
                            "url": "https://example.com/germany",
                            "title": "Germany",
                            "start_char_index": 32,
                            "end_char_index": 65
                        }
                    ]
                }
            ],
            "usage": {
                "input_tokens": 80,
                "output_tokens": 25,
                "server_tool_use": {
                    "web_search_requests": 2
                }
            },
            "stop_reason": "end_turn"
        }
        """;

    private const string WebSearchResponseNoServerToolUse = """
        {
            "model": "claude-sonnet-4-20250514",
            "content": [
                {
                    "type": "text",
                    "text": "Answer without search."
                }
            ],
            "usage": {
                "input_tokens": 20,
                "output_tokens": 5
            },
            "stop_reason": "end_turn"
        }
        """;

    #endregion

    private static ChatCompletionRequest CreateRequest() =>
        new(Messages: [new LlmMessage(LlmRole.User, "What is the capital of France?")],
            Model: "claude-sonnet-4-20250514");

    private static async Task<(GroundedChatCompletionResponse response, string? capturedBody)> ExecuteWebSearch(
        string responseJson,
        ChatCompletionRequest? request = null,
        AnthropicClientOptions? options = null)
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
        var client = new AnthropicChatCompletionClient(httpClient, options ?? new AnthropicClientOptions());

        var response = await client.GetChatCompletionWithWebSearchAsync(
            request ?? CreateRequest(),
            new WebSearchOptions());

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
        Assert.Multiple(() =>
        {
            Assert.That(tool.GetProperty("name").GetString(), Is.EqualTo("web_search"));
            Assert.That(tool.GetProperty("type").GetString(), Is.EqualTo("web_search_20260209"));
        });
    }

    [Test]
    public async Task WebSearch_UsesConfiguredToolVersion()
    {
        var options = new AnthropicClientOptions
        {
            WebSearchToolVersion = "web_search_20270101"
        };

        var (_, capturedBody) = await ExecuteWebSearch(WebSearchResponseWithCitations, options: options);

        var doc = JsonDocument.Parse(capturedBody!);
        var tools = doc.RootElement.GetProperty("tools");
        var tool = tools[0];
        Assert.That(tool.GetProperty("type").GetString(), Is.EqualTo("web_search_20270101"));
    }

    [Test]
    public async Task WebSearch_PostsToMessagesEndpoint()
    {
        var handler = new MockHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(WebSearchResponseWithCitations, System.Text.Encoding.UTF8, "application/json")
            }));

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.anthropic.com/v1/") };
        var client = new AnthropicChatCompletionClient(httpClient, new AnthropicClientOptions());

        await client.GetChatCompletionWithWebSearchAsync(CreateRequest(), new WebSearchOptions());

        Assert.That(handler.LastRequest?.RequestUri?.AbsolutePath, Does.EndWith("/messages"));
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
            Assert.That(response.Citations[0].Text, Is.EqualTo("The capital of France is Paris."));
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
    public async Task WebSearch_CitationSource_ContainsCitedText()
    {
        var (response, _) = await ExecuteWebSearch(WebSearchResponseWithCitations);

        Assert.That(response.Citations[0].Sources[0].CitedText,
            Is.EqualTo("Paris is the capital and most populous city of France."));
    }

    [Test]
    public async Task WebSearch_CitationType_IsWebSearchResultLocation()
    {
        var (response, _) = await ExecuteWebSearch(WebSearchResponseWithCitations);

        Assert.That(response.Citations[0].Type, Is.EqualTo("web_search_result_location"));
    }

    [Test]
    public async Task WebSearch_MapsContent()
    {
        var (response, _) = await ExecuteWebSearch(WebSearchResponseWithCitations);

        Assert.That(response.Content, Is.EqualTo("The capital of France is Paris."));
    }

    [Test]
    public async Task WebSearch_MapsTokenUsage()
    {
        var (response, _) = await ExecuteWebSearch(WebSearchResponseWithCitations);

        Assert.Multiple(() =>
        {
            Assert.That(response.ChatCompletion.PromptTokens, Is.EqualTo(50));
            Assert.That(response.ChatCompletion.CompletionTokens, Is.EqualTo(15));
        });
    }

    [Test]
    public async Task WebSearch_MapsWebSearchCount()
    {
        var (response, _) = await ExecuteWebSearch(WebSearchResponseWithCitations);

        Assert.That(response.ChatCompletion.WebSearchCount, Is.EqualTo(1));
    }

    [Test]
    public async Task WebSearch_MultipleSearches_MapsCount()
    {
        var (response, _) = await ExecuteWebSearch(WebSearchResponseMultipleSearches);

        Assert.That(response.ChatCompletion.WebSearchCount, Is.EqualTo(2));
    }

    [Test]
    public async Task WebSearch_ZeroSearches_WebSearchCountIsZero()
    {
        var (response, _) = await ExecuteWebSearch(WebSearchResponseNoCitations);

        Assert.That(response.ChatCompletion.WebSearchCount, Is.EqualTo(0));
    }

    [Test]
    public async Task WebSearch_NoServerToolUse_WebSearchCountIsNull()
    {
        var (response, _) = await ExecuteWebSearch(WebSearchResponseNoServerToolUse);

        Assert.That(response.ChatCompletion.WebSearchCount, Is.Null);
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
    public async Task WebSearch_GroundingKind_IsWebSearch()
    {
        var (response, _) = await ExecuteWebSearch(WebSearchResponseWithCitations);

        Assert.That(response.GroundingKind, Is.EqualTo(GroundingKind.WebSearch));
    }

    [Test]
    public async Task WebSearch_MapsModel()
    {
        var (response, _) = await ExecuteWebSearch(WebSearchResponseWithCitations);

        Assert.That(response.ChatCompletion.Model, Is.EqualTo("claude-sonnet-4-20250514"));
    }

    [Test]
    public async Task WebSearch_WithRawResponse_IncludesRawJson()
    {
        var request = new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "What is the capital of France?")],
            Model: "claude-sonnet-4-20250514",
            IncludeRawResponse: true);

        var (response, _) = await ExecuteWebSearch(
            WebSearchResponseWithCitations,
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
    public async Task WebSearch_HttpError_ReturnsError()
    {
        var handler = new MockHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)
            {
                Content = new StringContent("""{"error":{"message":"Internal error"}}""",
                    System.Text.Encoding.UTF8, "application/json")
            }));

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.anthropic.com/v1/") };
        var client = new AnthropicChatCompletionClient(httpClient, new AnthropicClientOptions());

        var response = await client.GetChatCompletionWithWebSearchAsync(
            CreateRequest(), new WebSearchOptions());

        Assert.Multiple(() =>
        {
            Assert.That(response.IsSuccess, Is.False);
            Assert.That(response.ErrorMessage, Is.Not.Null.And.Not.Empty);
            Assert.That(response.Citations, Is.Empty);
        });
    }

    [Test]
    public async Task WebSearch_MaxTokens_SetsIncompleteReason()
    {
        const string responseMaxTokens = """
            {
                "model": "claude-sonnet-4-20250514",
                "content": [
                    {
                        "type": "text",
                        "text": "The capital of France is"
                    }
                ],
                "usage": { "input_tokens": 50, "output_tokens": 5 },
                "stop_reason": "max_tokens"
            }
            """;

        var (response, _) = await ExecuteWebSearch(responseMaxTokens);

        Assert.Multiple(() =>
        {
            Assert.That(response.IsSuccess, Is.True);
            Assert.That(response.ChatCompletion.IncompleteReason, Is.EqualTo("max_tokens"));
        });
    }

    [Test]
    public async Task WebSearch_Refusal_SetsRefusalAndClearsContent()
    {
        const string responseRefusal = """
            {
                "model": "claude-sonnet-4-20250514",
                "content": [
                    {
                        "type": "text",
                        "text": "I cannot help with that request."
                    }
                ],
                "usage": { "input_tokens": 50, "output_tokens": 8 },
                "stop_reason": "refusal"
            }
            """;

        var (response, _) = await ExecuteWebSearch(responseRefusal);

        Assert.Multiple(() =>
        {
            Assert.That(response.IsSuccess, Is.True);
            Assert.That(response.ChatCompletion.Refusal, Is.EqualTo("I cannot help with that request."));
            Assert.That(response.ChatCompletion.Content, Is.Empty);
        });
    }

    [Test]
    public async Task WebSearch_MissingUrl_SkipsCitation()
    {
        const string responseWithMissingUrl = """
            {
                "model": "claude-sonnet-4-20250514",
                "content": [
                    {
                        "type": "text",
                        "text": "Some answer.",
                        "citations": [
                            {
                                "type": "web_search_result_location",
                                "cited_text": "source text",
                                "title": "Some Title"
                            }
                        ]
                    }
                ],
                "usage": { "input_tokens": 30, "output_tokens": 5 },
                "stop_reason": "end_turn"
            }
            """;

        var (response, _) = await ExecuteWebSearch(responseWithMissingUrl);

        Assert.Multiple(() =>
        {
            Assert.That(response.IsSuccess, Is.True);
            Assert.That(response.Citations, Is.Empty);
            Assert.That(response.Content, Is.EqualTo("Some answer."));
        });
    }

    [Test]
    public async Task WebSearch_NullCitationEntry_SkipsCitationGracefully()
    {
        const string responseWithNullCitation = """
            {
                "model": "claude-sonnet-4-20250514",
                "content": [
                    {
                        "type": "text",
                        "text": "Some answer.",
                        "citations": [
                            null
                        ]
                    }
                ],
                "usage": { "input_tokens": 30, "output_tokens": 5 },
                "stop_reason": "end_turn"
            }
            """;

        var (response, _) = await ExecuteWebSearch(responseWithNullCitation);

        Assert.Multiple(() =>
        {
            Assert.That(response.IsSuccess, Is.True);
            Assert.That(response.Citations, Is.Empty);
        });
    }

    #endregion

    #region Feature Discovery Tests

    [Test]
    public void Features_GetWebSearchFeature_ReturnsSelf()
    {
        using var httpClient = new HttpClient { BaseAddress = new Uri("https://api.anthropic.com/v1/") };
        var client = new AnthropicChatCompletionClient(httpClient, new AnthropicClientOptions());

        var feature = client.Features.Get<IWebSearchFeature>();

        Assert.Multiple(() =>
        {
            Assert.That(feature, Is.Not.Null);
            Assert.That(feature, Is.SameAs(client));
        });
    }

    #endregion
}
