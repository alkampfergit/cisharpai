using System.Net;
using System.Text.Json;
using Cisharpai.Models;
using Cisharpai.OpenAi;
using Cisharpai.Rag;

namespace Cisharpai.Tests.OpenAi;

public sealed class OpenAiFileSearchRetrievalTests
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
                        },
                        {
                            "file_id": "file-def456",
                            "filename": "notes.txt",
                            "score": 0.78,
                            "text": "Another relevant passage.",
                            "attributes": {}
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

    private const string FileSearchNoResultsResponse = """
        {
            "id": "resp_fs_002",
            "model": "gpt-5-0",
            "status": "completed",
            "output": [
                {
                    "type": "file_search_call",
                    "id": "fs_call_002",
                    "status": "completed",
                    "queries": ["obscure query"],
                    "results": []
                },
                {
                    "type": "message",
                    "role": "assistant",
                    "content": [
                        {
                            "type": "output_text",
                            "text": "I could not find relevant information."
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

    private const string FileSearchWithCitationsResponse = """
        {
            "id": "resp_fs_005",
            "model": "gpt-5-0",
            "status": "completed",
            "output": [
                {
                    "type": "file_search_call",
                    "id": "fs_call_005",
                    "status": "completed",
                    "queries": ["query"],
                    "results": [
                        {
                            "file_id": "file-abc",
                            "filename": "doc.pdf",
                            "score": 0.90,
                            "text": "The answer is 42."
                        }
                    ]
                },
                {
                    "type": "message",
                    "role": "assistant",
                    "content": [
                        {
                            "type": "output_text",
                            "text": "The answer is 42.",
                            "annotations": [
                                {
                                    "type": "file_citation",
                                    "file_id": "file-abc",
                                    "filename": "doc.pdf",
                                    "start_index": 0,
                                    "end_index": 17
                                }
                            ]
                        }
                    ]
                }
            ],
            "usage": {
                "input_tokens": 100,
                "output_tokens": 10
            }
        }
        """;

    #endregion

    #region Helpers

    private static OpenAiChatCompletionClient CreateClient(
        string responseJson,
        out Func<string?> getCapturedBody)
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

        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.openai.com/v1/") };
        getCapturedBody = () => capturedBody;
        return new OpenAiChatCompletionClient(httpClient, new OpenAiClientOptions { DefaultModel = "gpt-5-0" });
    }

    private static OpenAiChatCompletionClient CreateClientWithStatus(
        HttpStatusCode statusCode,
        string responseBody = "error")
    {
        var handler = new MockHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(responseBody, System.Text.Encoding.UTF8, "application/json")
            }));

        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.openai.com/v1/") };
        return new OpenAiChatCompletionClient(httpClient, new OpenAiClientOptions { DefaultModel = "gpt-5-0" });
    }

    #endregion

    #region IHostedRetrievalFeature Discovery

    [Test]
    public void HostedRetrievalFeature_IsDiscoverable()
    {
        var client = CreateClient(FileSearchSuccessResponse, out _);
        var feature = client.Features.Get<IHostedRetrievalFeature>();
        Assert.That(feature, Is.Not.Null);
    }

    #endregion

    #region ForStore

    [Test]
    public void ForStore_ReturnsRetriever()
    {
        var client = CreateClient(FileSearchSuccessResponse, out _);
        var feature = client.Features.Get<IHostedRetrievalFeature>()!;
        var retriever = feature.ForStore("vs_test");
        Assert.That(retriever, Is.Not.Null);
    }

    [Test]
    public void ForStore_NullStoreId_Throws()
    {
        var client = CreateClient(FileSearchSuccessResponse, out _);
        var feature = client.Features.Get<IHostedRetrievalFeature>()!;
        Assert.Throws<ArgumentNullException>(() => feature.ForStore(null!));
    }

    [Test]
    public void ForStore_EmptyStoreId_Throws()
    {
        var client = CreateClient(FileSearchSuccessResponse, out _);
        var feature = client.Features.Get<IHostedRetrievalFeature>()!;
        Assert.Throws<ArgumentException>(() => feature.ForStore(""));
    }

    #endregion

    #region Request Shape

    [Test]
    public async Task Retrieve_SendsFileSearchTool_WithVectorStoreId()
    {
        var client = CreateClient(FileSearchSuccessResponse, out var getCapturedBody);
        var retriever = client.Features.Get<IHostedRetrievalFeature>()!.ForStore("vs_my_store");

        await retriever.RetrieveAsync("test query", 5);

        var body = getCapturedBody()!;
        var doc = JsonDocument.Parse(body);
        var tools = doc.RootElement.GetProperty("tools");
        Assert.That(tools.GetArrayLength(), Is.EqualTo(1));

        var tool = tools[0];
        Assert.Multiple(() =>
        {
            Assert.That(tool.GetProperty("type").GetString(), Is.EqualTo("file_search"));
            Assert.That(tool.GetProperty("vector_store_ids")[0].GetString(), Is.EqualTo("vs_my_store"));
            Assert.That(tool.GetProperty("max_num_results").GetInt32(), Is.EqualTo(5));
        });
    }

    [Test]
    public async Task Retrieve_SetsIncludeFileSearchCallResults()
    {
        var client = CreateClient(FileSearchSuccessResponse, out var getCapturedBody);
        var retriever = client.Features.Get<IHostedRetrievalFeature>()!.ForStore("vs_test");

        await retriever.RetrieveAsync("query", 10);

        var body = getCapturedBody()!;
        var doc = JsonDocument.Parse(body);
        var include = doc.RootElement.GetProperty("include");
        Assert.That(include.GetArrayLength(), Is.EqualTo(1));
        Assert.That(include[0].GetString(), Is.EqualTo("file_search_call.results"));
    }

    [Test]
    public async Task Retrieve_PostsToResponsesEndpoint()
    {
        var handler = new MockHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(FileSearchSuccessResponse, System.Text.Encoding.UTF8, "application/json")
            }));

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.openai.com/v1/") };
        var client = new OpenAiChatCompletionClient(httpClient, new OpenAiClientOptions { DefaultModel = "gpt-5-0" });
        var retriever = client.Features.Get<IHostedRetrievalFeature>()!.ForStore("vs_test");

        await retriever.RetrieveAsync("query", 5);

        Assert.That(handler.LastRequest?.RequestUri?.AbsolutePath, Does.EndWith("/responses"));
    }

    [Test]
    public async Task Retrieve_SendsQueryAsUserMessage()
    {
        var client = CreateClient(FileSearchSuccessResponse, out var getCapturedBody);
        var retriever = client.Features.Get<IHostedRetrievalFeature>()!.ForStore("vs_test");

        await retriever.RetrieveAsync("my search query", 5);

        var body = getCapturedBody()!;
        var doc = JsonDocument.Parse(body);
        var input = doc.RootElement.GetProperty("input");
        Assert.That(input.GetArrayLength(), Is.EqualTo(1));

        var msg = input[0];
        Assert.Multiple(() =>
        {
            Assert.That(msg.GetProperty("role").GetString(), Is.EqualTo("user"));
            Assert.That(msg.GetProperty("content").GetString(), Is.EqualTo("my search query"));
        });
    }

    #endregion

    #region Result Mapping

    [Test]
    public async Task Retrieve_MapsResultsToScoredChunks()
    {
        var client = CreateClient(FileSearchSuccessResponse, out _);
        var retriever = client.Features.Get<IHostedRetrievalFeature>()!.ForStore("vs_test");

        var results = await retriever.RetrieveAsync("query", 10);

        Assert.That(results, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task Retrieve_FirstResult_HasCorrectScore()
    {
        var client = CreateClient(FileSearchSuccessResponse, out _);
        var retriever = client.Features.Get<IHostedRetrievalFeature>()!.ForStore("vs_test");

        var results = await retriever.RetrieveAsync("query", 10);

        Assert.That(results[0].Score, Is.EqualTo(0.92).Within(0.001));
    }

    [Test]
    public async Task Retrieve_MapsDocumentIdToFileId()
    {
        var client = CreateClient(FileSearchSuccessResponse, out _);
        var retriever = client.Features.Get<IHostedRetrievalFeature>()!.ForStore("vs_test");

        var results = await retriever.RetrieveAsync("query", 10);

        Assert.That(results[0].Chunk.DocumentId, Is.EqualTo("file-abc123"));
    }

    [Test]
    public async Task Retrieve_MapsIndexToZero()
    {
        var client = CreateClient(FileSearchSuccessResponse, out _);
        var retriever = client.Features.Get<IHostedRetrievalFeature>()!.ForStore("vs_test");

        var results = await retriever.RetrieveAsync("query", 10);

        Assert.That(results[0].Chunk.Index, Is.EqualTo(0));
    }

    [Test]
    public async Task Retrieve_MapsOffsetsAsPassageRelative()
    {
        var client = CreateClient(FileSearchSuccessResponse, out _);
        var retriever = client.Features.Get<IHostedRetrievalFeature>()!.ForStore("vs_test");

        var results = await retriever.RetrieveAsync("query", 10);

        Assert.Multiple(() =>
        {
            Assert.That(results[0].Chunk.StartOffset, Is.EqualTo(0));
            Assert.That(results[0].Chunk.EndOffset, Is.EqualTo(results[0].Chunk.Text.Length));
        });
    }

    [Test]
    public async Task Retrieve_MapsText()
    {
        var client = CreateClient(FileSearchSuccessResponse, out _);
        var retriever = client.Features.Get<IHostedRetrievalFeature>()!.ForStore("vs_test");

        var results = await retriever.RetrieveAsync("query", 10);

        Assert.That(results[0].Chunk.Text, Is.EqualTo("This is the relevant passage from the document."));
    }

    [Test]
    public async Task Retrieve_MapsMetadata_IncludesFileIdAndFilename()
    {
        var client = CreateClient(FileSearchSuccessResponse, out _);
        var retriever = client.Features.Get<IHostedRetrievalFeature>()!.ForStore("vs_test");

        var results = await retriever.RetrieveAsync("query", 10);

        Assert.Multiple(() =>
        {
            Assert.That(results[0].Chunk.Metadata["file_id"], Is.EqualTo("file-abc123"));
            Assert.That(results[0].Chunk.Metadata["filename"], Is.EqualTo("document.pdf"));
        });
    }

    [Test]
    public async Task Retrieve_NoResults_ReturnsEmptyList()
    {
        var client = CreateClient(FileSearchNoResultsResponse, out _);
        var retriever = client.Features.Get<IHostedRetrievalFeature>()!.ForStore("vs_test");

        var results = await retriever.RetrieveAsync("obscure", 10);

        Assert.That(results, Is.Empty);
    }

    #endregion

    #region Error Handling

    [Test]
    public async Task Retrieve_HttpError_ReturnsEmptyList_DoesNotThrow()
    {
        var client = CreateClientWithStatus(HttpStatusCode.InternalServerError);
        var retriever = client.Features.Get<IHostedRetrievalFeature>()!.ForStore("vs_test");

        var results = await retriever.RetrieveAsync("query", 5);

        Assert.That(results, Is.Empty);
    }

    [Test]
    public async Task Retrieve_FailedFileSearchCall_ReturnsEmptyList()
    {
        var client = CreateClient(FileSearchFailedCallResponse, out _);
        var retriever = client.Features.Get<IHostedRetrievalFeature>()!.ForStore("vs_test");

        var results = await retriever.RetrieveAsync("query", 5);

        Assert.That(results, Is.Empty);
    }

    [Test]
    public async Task Retrieve_MixedStatus_ReturnsOnlyCompletedResults()
    {
        var client = CreateClient(FileSearchMixedStatusResponse, out _);
        var retriever = client.Features.Get<IHostedRetrievalFeature>()!.ForStore("vs_test");

        var results = await retriever.RetrieveAsync("query", 10);

        Assert.That(results, Has.Count.EqualTo(1));
        Assert.That(results[0].Chunk.DocumentId, Is.EqualTo("file-ok"));
    }

    [Test]
    public void Retrieve_CancellationRequested_ThrowsOperationCanceled()
    {
        var client = CreateClient(FileSearchSuccessResponse, out _);
        var retriever = client.Features.Get<IHostedRetrievalFeature>()!.ForStore("vs_test");

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Assert.ThrowsAsync<OperationCanceledException>(
            () => retriever.RetrieveAsync("query", 5, cts.Token));
    }

    [Test]
    public void Retrieve_NoDefaultModel_Throws()
    {
        var handler = new MockHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.openai.com/v1/") };
        var client = new OpenAiChatCompletionClient(httpClient, new OpenAiClientOptions());
        var retriever = client.Features.Get<IHostedRetrievalFeature>()!.ForStore("vs_test");

        Assert.ThrowsAsync<InvalidOperationException>(
            () => retriever.RetrieveAsync("query", 5));
    }

    #endregion

    #region Chat Path — file_search_call failure sets IsSuccess=false

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

    #endregion
}
