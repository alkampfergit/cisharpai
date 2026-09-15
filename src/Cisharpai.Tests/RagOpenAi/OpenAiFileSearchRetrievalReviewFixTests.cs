using System.Net;
using System.Net.Sockets;
using Cisharpai.OpenAi;
using Cisharpai.Rag;
using Cisharpai.Rag.OpenAi;

namespace Cisharpai.Tests.RagOpenAi;

/// <summary>
/// Tests for review-round fixes on the retriever:
/// input validation, topK enforcement after flattening, and narrowed exception handling.
/// </summary>
public sealed class OpenAiFileSearchRetrievalReviewFixTests
{
    #region Response Fixtures

    private const string MultipleFileSearchCallsResponse = """
        {
            "id": "resp_multi",
            "model": "gpt-5-0",
            "status": "completed",
            "output": [
                {
                    "type": "file_search_call",
                    "id": "fs_call_1",
                    "status": "completed",
                    "queries": ["query"],
                    "results": [
                        { "file_id": "file-1", "filename": "a.pdf", "score": 0.95, "text": "Result 1" },
                        { "file_id": "file-2", "filename": "b.pdf", "score": 0.90, "text": "Result 2" },
                        { "file_id": "file-3", "filename": "c.pdf", "score": 0.85, "text": "Result 3" }
                    ]
                },
                {
                    "type": "file_search_call",
                    "id": "fs_call_2",
                    "status": "completed",
                    "queries": ["query"],
                    "results": [
                        { "file_id": "file-4", "filename": "d.pdf", "score": 0.80, "text": "Result 4" },
                        { "file_id": "file-5", "filename": "e.pdf", "score": 0.75, "text": "Result 5" }
                    ]
                },
                {
                    "type": "message",
                    "role": "assistant",
                    "content": [{ "type": "output_text", "text": "Results." }]
                }
            ],
            "usage": { "input_tokens": 100, "output_tokens": 10 }
        }
        """;

    private const string SimpleSuccessResponse = """
        {
            "id": "resp_simple",
            "model": "gpt-5-0",
            "status": "completed",
            "output": [
                {
                    "type": "file_search_call",
                    "id": "fs_call_1",
                    "status": "completed",
                    "queries": ["q"],
                    "results": [
                        { "file_id": "file-1", "filename": "a.pdf", "score": 0.9, "text": "Hello" }
                    ]
                },
                {
                    "type": "message",
                    "role": "assistant",
                    "content": [{ "type": "output_text", "text": "Answer." }]
                }
            ],
            "usage": { "input_tokens": 50, "output_tokens": 5 }
        }
        """;

    #endregion

    #region Helpers

    private static OpenAiHostedRetrievalFeature CreateFeature(string responseJson)
    {
        var handler = new MockHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson, System.Text.Encoding.UTF8, "application/json")
            }));

        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.openai.com/v1/") };
        return new OpenAiHostedRetrievalFeature(httpClient, new OpenAiClientOptions { DefaultModel = "gpt-5-0" });
    }

    private static OpenAiHostedRetrievalFeature CreateFeatureWithException(Exception ex)
    {
        var handler = new MockHttpMessageHandler((_, _) => throw ex);
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.openai.com/v1/") };
        return new OpenAiHostedRetrievalFeature(httpClient, new OpenAiClientOptions { DefaultModel = "gpt-5-0" });
    }

    #endregion

    #region Input validation

    [Test]
    public void Retrieve_NullQuery_ThrowsArgumentNullException()
    {
        var feature = CreateFeature(SimpleSuccessResponse);
        var retriever = feature.ForStore("vs_test");

        Assert.ThrowsAsync<ArgumentNullException>(() =>
            retriever.RetrieveAsync(null!, 5));
    }

    [Test]
    public void Retrieve_ZeroTopK_ThrowsArgumentOutOfRangeException()
    {
        var feature = CreateFeature(SimpleSuccessResponse);
        var retriever = feature.ForStore("vs_test");

        Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            retriever.RetrieveAsync("query", 0));
    }

    [Test]
    public void Retrieve_NegativeTopK_ThrowsArgumentOutOfRangeException()
    {
        var feature = CreateFeature(SimpleSuccessResponse);
        var retriever = feature.ForStore("vs_test");

        Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            retriever.RetrieveAsync("query", -1));
    }

    #endregion

    #region topK enforcement after flattening

    [Test]
    public async Task Retrieve_MultipleFileSearchCalls_EnforcesTopK()
    {
        var feature = CreateFeature(MultipleFileSearchCallsResponse);
        var retriever = feature.ForStore("vs_test");

        var results = await retriever.RetrieveAsync("query", 3);

        Assert.That(results, Has.Count.EqualTo(3),
            "topK should be enforced after flattening results from multiple file_search_call items");
    }

    [Test]
    public async Task Retrieve_MultipleFileSearchCalls_TopK1_ReturnsSingle()
    {
        var feature = CreateFeature(MultipleFileSearchCallsResponse);
        var retriever = feature.ForStore("vs_test");

        var results = await retriever.RetrieveAsync("query", 1);

        Assert.That(results, Has.Count.EqualTo(1));
        Assert.That(results[0].Chunk.DocumentId, Is.EqualTo("file-1"));
    }

    [Test]
    public async Task Retrieve_MultipleFileSearchCalls_TopKExceedsTotal_ReturnsAll()
    {
        var feature = CreateFeature(MultipleFileSearchCallsResponse);
        var retriever = feature.ForStore("vs_test");

        var results = await retriever.RetrieveAsync("query", 100);

        Assert.That(results, Has.Count.EqualTo(5));
    }

    #endregion

    #region Narrowed catch — transport faults surface

    [Test]
    public void Retrieve_DnsFailure_Propagates()
    {
        var feature = CreateFeatureWithException(
            new HttpRequestException("No such host", new SocketException()));
        var retriever = feature.ForStore("vs_test");

        Assert.ThrowsAsync<HttpRequestException>(() =>
            retriever.RetrieveAsync("query", 5));
    }

    [Test]
    public void Retrieve_ConnectionRefused_Propagates()
    {
        var feature = CreateFeatureWithException(
            new HttpRequestException("Connection refused"));
        var retriever = feature.ForStore("vs_test");

        Assert.ThrowsAsync<HttpRequestException>(() =>
            retriever.RetrieveAsync("query", 5));
    }

    [Test]
    public async Task Retrieve_ApiError_ReturnsEmptyList()
    {
        var handler = new MockHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)
            {
                Content = new StringContent("server error", System.Text.Encoding.UTF8, "application/json")
            }));

        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.openai.com/v1/") };
        var feature = new OpenAiHostedRetrievalFeature(httpClient, new OpenAiClientOptions { DefaultModel = "gpt-5-0" });
        var retriever = feature.ForStore("vs_test");

        var results = await retriever.RetrieveAsync("query", 5);
        Assert.That(results, Is.Empty);
    }

    #endregion
}
