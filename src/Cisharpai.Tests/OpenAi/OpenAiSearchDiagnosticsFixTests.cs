using System.Net;
using Cisharpai.Models;
using Cisharpai.OpenAi;

namespace Cisharpai.Tests.OpenAi;

/// <summary>
/// Tests that failed file_search_call and web_search_call diagnostics are appended
/// to any existing errorMessage rather than dropped when the top-level Responses
/// status has already failed.
/// </summary>
public sealed class OpenAiSearchDiagnosticsFixTests
{
    #region File search diagnostics

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

    #region Web search diagnostics

    [Test]
    public async Task WebSearch_FailedCallPlusFailedStatus_AppendsDiagnostic()
    {
        const string response = """
            {
                "id": "resp_3",
                "model": "gpt-5-0",
                "status": "failed",
                "incomplete_details": { "reason": "content_filter" },
                "output": [
                    {
                        "type": "web_search_call",
                        "id": "ws_fail_001",
                        "status": "failed"
                    },
                    {
                        "type": "message",
                        "role": "assistant",
                        "content": [{ "type": "output_text", "text": "Partial web answer." }]
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

        var result = await client.GetChatCompletionWithWebSearchAsync(
            new ChatCompletionRequest(
                Messages: [new LlmMessage(LlmRole.User, "test")],
                Model: "gpt-5-0"),
            new WebSearchOptions());

        Assert.Multiple(() =>
        {
            Assert.That(result.ChatCompletion.IsSuccess, Is.False);
            Assert.That(result.ChatCompletion.ErrorMessage, Does.Contain("content_filter"),
                "Original error from the failed status should be present");
            Assert.That(result.ChatCompletion.ErrorMessage, Does.Contain("ws_fail_001"),
                "Failed web search call ids should be appended, not dropped");
            Assert.That(result.ChatCompletion.ErrorMessage, Does.Contain("web search"),
                "Web search failure detail should be appended");
        });
    }

    [Test]
    public async Task WebSearch_FailedCallOnlyNoTopLevelError_SetsDiagnostic()
    {
        const string response = """
            {
                "id": "resp_4",
                "model": "gpt-5-0",
                "status": "completed",
                "output": [
                    {
                        "type": "web_search_call",
                        "id": "ws_fail_002",
                        "status": "failed"
                    },
                    {
                        "type": "message",
                        "role": "assistant",
                        "content": [{ "type": "output_text", "text": "Answer." }]
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

        var result = await client.GetChatCompletionWithWebSearchAsync(
            new ChatCompletionRequest(
                Messages: [new LlmMessage(LlmRole.User, "test")],
                Model: "gpt-5-0"),
            new WebSearchOptions());

        Assert.Multiple(() =>
        {
            Assert.That(result.ChatCompletion.IsSuccess, Is.False);
            Assert.That(result.ChatCompletion.ErrorMessage, Does.Contain("ws_fail_002"));
        });
    }

    #endregion
}
