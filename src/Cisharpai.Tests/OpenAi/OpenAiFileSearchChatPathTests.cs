using System.Net;
using Cisharpai.Models;
using Cisharpai.OpenAi;

namespace Cisharpai.Tests.OpenAi;

/// <summary>
/// Tests for the <c>file_search_call</c> failure handling on the chat completion path.
/// Retrieval-specific tests (IRetriever, ForStore, result mapping) are in
/// <see cref="RagOpenAi.OpenAiFileSearchRetrievalTests"/>.
/// </summary>
public sealed class OpenAiFileSearchChatPathTests
{
    #region Response Fixtures

    private const string FileSearchSuccessResponse = """
        {
            "id": "resp_fs_001",
            "model": "gpt-5-0",
            "status": "completed",
            "output": [
                {
                    "type": "file_search_call",
                    "id": "fs_call_001",
                    "status": "completed",
                    "queries": ["search query"],
                    "results": [
                        {
                            "file_id": "file-abc123",
                            "filename": "document.pdf",
                            "score": 0.92,
                            "text": "This is the relevant passage from the document.",
                            "attributes": { "category": "technical" }
                        }
                    ]
                },
                {
                    "type": "message",
                    "role": "assistant",
                    "content": [
                        {
                            "type": "output_text",
                            "text": "Based on the documents, here is the answer."
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

    private const string FileSearchFailedCallResponse = """
        {
            "id": "resp_fs_003",
            "model": "gpt-5-0",
            "status": "completed",
            "output": [
                {
                    "type": "file_search_call",
                    "id": "fs_call_003",
                    "status": "failed"
                },
                {
                    "type": "message",
                    "role": "assistant",
                    "content": [
                        {
                            "type": "output_text",
                            "text": "I was unable to search the documents."
                        }
                    ]
                }
            ],
            "usage": {
                "input_tokens": 60,
                "output_tokens": 10
            }
        }
        """;

    private const string FileSearchMixedStatusResponse = """
        {
            "id": "resp_fs_004",
            "model": "gpt-5-0",
            "status": "completed",
            "output": [
                {
                    "type": "file_search_call",
                    "id": "fs_ok",
                    "status": "completed",
                    "queries": ["query"],
                    "results": [
                        {
                            "file_id": "file-ok",
                            "filename": "good.pdf",
                            "score": 0.85,
                            "text": "Valid result."
                        }
                    ]
                },
                {
                    "type": "file_search_call",
                    "id": "fs_fail",
                    "status": "failed"
                },
                {
                    "type": "message",
                    "role": "assistant",
                    "content": [
                        {
                            "type": "output_text",
                            "text": "Partial results."
                        }
                    ]
                }
            ],
            "usage": {
                "input_tokens": 90,
                "output_tokens": 10
            }
        }
        """;

    #endregion

    [Test]
    public async Task ChatCompletion_FailedFileSearchCall_SetsIsSuccessFalse()
    {
        var handler = new MockHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(FileSearchFailedCallResponse, System.Text.Encoding.UTF8, "application/json")
            }));

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.openai.com/v1/") };
        var client = new OpenAiChatCompletionClient(httpClient, new OpenAiClientOptions());

        var response = await client.GetChatCompletionAsync(
            new ChatCompletionRequest(
                Messages: [new LlmMessage(LlmRole.User, "test")],
                Model: "gpt-5-0"));

        Assert.Multiple(() =>
        {
            Assert.That(response.IsSuccess, Is.False);
            Assert.That(response.ErrorMessage, Does.Contain("file search"));
            Assert.That(response.ErrorMessage, Does.Contain("fs_call_003"));
        });
    }

    [Test]
    public async Task ChatCompletion_FailedFileSearchCall_PreservesContent()
    {
        var handler = new MockHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(FileSearchFailedCallResponse, System.Text.Encoding.UTF8, "application/json")
            }));

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.openai.com/v1/") };
        var client = new OpenAiChatCompletionClient(httpClient, new OpenAiClientOptions());

        var response = await client.GetChatCompletionAsync(
            new ChatCompletionRequest(
                Messages: [new LlmMessage(LlmRole.User, "test")],
                Model: "gpt-5-0"));

        Assert.That(response.Content, Is.EqualTo("I was unable to search the documents."));
    }

    [Test]
    public async Task ChatCompletion_CompletedFileSearchCall_IsSuccessTrue()
    {
        var handler = new MockHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(FileSearchSuccessResponse, System.Text.Encoding.UTF8, "application/json")
            }));

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.openai.com/v1/") };
        var client = new OpenAiChatCompletionClient(httpClient, new OpenAiClientOptions());

        var response = await client.GetChatCompletionAsync(
            new ChatCompletionRequest(
                Messages: [new LlmMessage(LlmRole.User, "test")],
                Model: "gpt-5-0"));

        Assert.That(response.IsSuccess, Is.True);
    }

    [Test]
    public async Task ChatCompletion_MixedFileSearchStatus_SetsIsSuccessFalse()
    {
        var handler = new MockHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(FileSearchMixedStatusResponse, System.Text.Encoding.UTF8, "application/json")
            }));

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.openai.com/v1/") };
        var client = new OpenAiChatCompletionClient(httpClient, new OpenAiClientOptions());

        var response = await client.GetChatCompletionAsync(
            new ChatCompletionRequest(
                Messages: [new LlmMessage(LlmRole.User, "test")],
                Model: "gpt-5-0"));

        Assert.Multiple(() =>
        {
            Assert.That(response.IsSuccess, Is.False);
            Assert.That(response.ErrorMessage, Does.Contain("fs_fail"));
            Assert.That(response.Content, Is.EqualTo("Partial results."));
        });
    }

    #region Search Diagnostics

    [Test]
    public async Task FileSearch_FailedCallPlusFailedStatus_AppendsDiagnostic()
    {
        const string response = """
            {
                "id": "resp_1",
                "model": "gpt-5-0",
                "status": "failed",
                "incomplete_details": { "reason": "content_filter" },
                "output": [
                    {
                        "type": "file_search_call",
                        "id": "fs_fail_001",
                        "status": "failed"
                    },
                    {
                        "type": "message",
                        "role": "assistant",
                        "content": [{ "type": "output_text", "text": "Partial answer." }]
                    }
                ],
                "usage": { "input_tokens": 50, "output_tokens": 10 }
            }
            """;

        var handler = new MockHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(response, System.Text.Encoding.UTF8, "application/json")
            }));

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.openai.com/v1/") };
        var client = new OpenAiChatCompletionClient(httpClient, new OpenAiClientOptions());

        var result = await client.GetChatCompletionAsync(
            new ChatCompletionRequest(
                Messages: [new LlmMessage(LlmRole.User, "test")],
                Model: "gpt-5-0"));

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.ErrorMessage, Does.Contain("content_filter"),
                "Original error from the failed status should be present");
            Assert.That(result.ErrorMessage, Does.Contain("fs_fail_001"),
                "Failed file search call ids should be appended, not dropped");
            Assert.That(result.ErrorMessage, Does.Contain("file search"),
                "File search failure detail should be appended");
        });
    }

    [Test]
    public async Task FileSearch_FailedCallOnlyNoTopLevelError_SetsDiagnostic()
    {
        const string response = """
            {
                "id": "resp_2",
                "model": "gpt-5-0",
                "status": "completed",
                "output": [
                    {
                        "type": "file_search_call",
                        "id": "fs_fail_002",
                        "status": "failed"
                    },
                    {
                        "type": "message",
                        "role": "assistant",
                        "content": [{ "type": "output_text", "text": "Answer without search." }]
                    }
                ],
                "usage": { "input_tokens": 50, "output_tokens": 10 }
            }
            """;

        var handler = new MockHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(response, System.Text.Encoding.UTF8, "application/json")
            }));

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.openai.com/v1/") };
        var client = new OpenAiChatCompletionClient(httpClient, new OpenAiClientOptions());

        var result = await client.GetChatCompletionAsync(
            new ChatCompletionRequest(
                Messages: [new LlmMessage(LlmRole.User, "test")],
                Model: "gpt-5-0"));

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.ErrorMessage, Does.Contain("fs_fail_002"));
        });
    }

    #endregion
}
