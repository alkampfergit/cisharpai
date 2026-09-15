using System.Net;
using System.Text.Json;
using Cisharpai.OpenAi;
using Cisharpai.OpenAi.Models;

namespace Cisharpai.Tests.OpenAi;

/// <summary>
/// Tests for review-round fixes on <see cref="OpenAiVectorStoreClient"/>:
/// cancellation propagation, deadline-bounded poll, URL-encoded cursors,
/// ExtraParameters deep merge, and raw request/response payloads.
/// </summary>
public sealed class OpenAiVectorStoreClientReviewFixTests
{
    #region Helpers

    private static OpenAiVectorStoreClient CreateClient(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
    {
        var mockHandler = new MockHttpMessageHandler(handler);
        var httpClient = new HttpClient(mockHandler)
        {
            BaseAddress = new Uri("https://api.openai.com/v1/")
        };
        return new OpenAiVectorStoreClient(httpClient);
    }

    private static OpenAiVectorStoreClient CreateClient(string responseJson, HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        return CreateClient((_, _) =>
            Task.FromResult(new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(responseJson, System.Text.Encoding.UTF8, "application/json")
            }));
    }

    private const string StoreResponse = """
        {
            "id": "vs_abc123",
            "object": "vector_store",
            "name": "Test",
            "status": "completed",
            "file_counts": { "in_progress": 0, "completed": 0, "failed": 0, "cancelled": 0, "total": 0 },
            "created_at": 1700000000
        }
        """;

    private const string InProgressFileResponse = """
        {
            "id": "file-xyz789",
            "object": "vector_store.file",
            "vector_store_id": "vs_abc123",
            "status": "in_progress",
            "created_at": 1700000010
        }
        """;

    #endregion

    #region Cancellation propagation

    private static OpenAiVectorStoreClient CreateCancellationAwareClient(string responseJson)
    {
        return CreateClient((_, ct) =>
        {
            ct.ThrowIfCancellationRequested();
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson, System.Text.Encoding.UTF8, "application/json")
            });
        });
    }

    [Test]
    public void CreateStore_CancellationRequested_ThrowsOperationCanceled()
    {
        var client = CreateCancellationAwareClient(StoreResponse);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Assert.That(async () =>
            await client.CreateStoreAsync(new OpenAiVectorStoreCreateRequest { Name = "Test" }, cancellationToken: cts.Token),
            Throws.InstanceOf<OperationCanceledException>());
    }

    [Test]
    public void GetStore_CancellationRequested_ThrowsOperationCanceled()
    {
        var client = CreateCancellationAwareClient(StoreResponse);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Assert.That(async () => await client.GetStoreAsync("vs_abc123", cts.Token),
            Throws.InstanceOf<OperationCanceledException>());
    }

    [Test]
    public void DeleteStore_CancellationRequested_ThrowsOperationCanceled()
    {
        var client = CreateCancellationAwareClient("""{ "id": "vs_abc123", "deleted": true }""");
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Assert.That(async () => await client.DeleteStoreAsync("vs_abc123", cts.Token),
            Throws.InstanceOf<OperationCanceledException>());
    }

    [Test]
    public void UploadFile_CancellationRequested_ThrowsOperationCanceled()
    {
        var client = CreateCancellationAwareClient("""{ "id": "file-1", "object": "file", "bytes": 10, "filename": "f", "purpose": "assistants", "created_at": 0 }""");
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Assert.That(async () => await client.UploadFileAsync(new MemoryStream([1, 2, 3]), "f.pdf", cts.Token),
            Throws.InstanceOf<OperationCanceledException>());
    }

    #endregion

    #region Poll deadline actually bounds

    [Test]
    public async Task PollFile_ShortTimeout_DoesNotOvershootByFullInterval()
    {
        var requestCount = 0;
        var client = CreateClient((_, _) =>
        {
            Interlocked.Increment(ref requestCount);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(InProgressFileResponse, System.Text.Encoding.UTF8, "application/json")
            });
        });

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var result = await client.PollFileUntilProcessedAsync(
            "vs_abc123", "file-xyz789",
            timeout: TimeSpan.FromMilliseconds(200),
            pollInterval: TimeSpan.FromSeconds(10));
        sw.Stop();

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.ErrorMessage, Does.Contain("Timeout"));
            Assert.That(sw.Elapsed.TotalSeconds, Is.LessThan(5),
                "Poll should not wait a full 10s interval when timeout is 200ms");
        });
    }

    [Test]
    public async Task PollFile_TimeoutCancelsInFlightRequest()
    {
        var requestCancelled = false;
        var client = CreateClient(async (_, ct) =>
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(30), ct);
            }
            catch (OperationCanceledException)
            {
                requestCancelled = true;
                throw;
            }
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(InProgressFileResponse, System.Text.Encoding.UTF8, "application/json")
            };
        });

        var result = await client.PollFileUntilProcessedAsync(
            "vs_abc123", "file-xyz789",
            timeout: TimeSpan.FromMilliseconds(100),
            pollInterval: TimeSpan.FromMilliseconds(10));

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.ErrorMessage, Does.Contain("Timeout"));
            Assert.That(requestCancelled, Is.True, "In-flight request should be cancelled when deadline passes");
        });
    }

    [Test]
    public void PollFile_UserCancellation_PropagatesNotTimeout()
    {
        var client = CreateClient(InProgressFileResponse);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Assert.ThrowsAsync<OperationCanceledException>(() =>
            client.PollFileUntilProcessedAsync("vs_abc123", "file-xyz789",
                pollInterval: TimeSpan.FromMilliseconds(10),
                cancellationToken: cts.Token));
    }

    #endregion

    #region URL-encoded cursors

    [Test]
    public async Task ListStores_CursorWithSpecialChars_IsUrlEncoded()
    {
        string? capturedUri = null;
        var client = CreateClient(async (req, _) =>
        {
            capturedUri = req.RequestUri?.ToString();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{ "data": [], "has_more": false }""",
                    System.Text.Encoding.UTF8, "application/json")
            };
        });

        await client.ListStoresAsync(after: "cur+sor&id=bad");

        Assert.That(capturedUri, Does.Not.Contain("cur+sor&id=bad"));
        Assert.That(capturedUri, Does.Contain(Uri.EscapeDataString("cur+sor&id=bad")));
    }

    [Test]
    public async Task ListFilesInStore_CursorWithSpecialChars_IsUrlEncoded()
    {
        string? capturedUri = null;
        var client = CreateClient(async (req, _) =>
        {
            capturedUri = req.RequestUri?.ToString();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{ "data": [], "has_more": false }""",
                    System.Text.Encoding.UTF8, "application/json")
            };
        });

        await client.ListFilesInStoreAsync("vs_abc", after: "file+cursor=x");

        Assert.That(capturedUri, Does.Not.Contain("file+cursor=x"));
        Assert.That(capturedUri, Does.Contain(Uri.EscapeDataString("file+cursor=x")));
    }

    #endregion

    #region ExtraParameters deep merge

    [Test]
    public async Task CreateStore_ExtraParameters_AreMergedIntoRequest()
    {
        string? capturedBody = null;
        var client = CreateClient(async (req, _) =>
        {
            capturedBody = await req.Content!.ReadAsStringAsync(CancellationToken.None);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(StoreResponse, System.Text.Encoding.UTF8, "application/json")
            };
        });

        using var extra = JsonDocument.Parse("""{"chunking_strategy":{"type":"static","static":{"max_chunk_size_tokens":800}}}""");
        await client.CreateStoreAsync(
            new OpenAiVectorStoreCreateRequest { Name = "Test" },
            extraParameters: extra.RootElement.Clone());

        Assert.That(capturedBody, Is.Not.Null);
        var doc = JsonDocument.Parse(capturedBody!);
        Assert.Multiple(() =>
        {
            Assert.That(doc.RootElement.GetProperty("name").GetString(), Is.EqualTo("Test"));
            Assert.That(doc.RootElement.GetProperty("chunking_strategy").GetProperty("type").GetString(),
                Is.EqualTo("static"));
        });
    }

    [Test]
    public async Task AddFileToStore_ExtraParameters_AreMergedIntoRequest()
    {
        string? capturedBody = null;
        var client = CreateClient(async (req, _) =>
        {
            capturedBody = await req.Content!.ReadAsStringAsync(CancellationToken.None);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""
                    { "id": "file-1", "object": "vector_store.file", "vector_store_id": "vs_abc", "status": "in_progress", "created_at": 0 }
                    """, System.Text.Encoding.UTF8, "application/json")
            };
        });

        using var extra = JsonDocument.Parse("""{"chunking_strategy":{"type":"auto"}}""");
        await client.AddFileToStoreAsync("vs_abc", "file-1", extraParameters: extra.RootElement.Clone());

        Assert.That(capturedBody, Is.Not.Null);
        var doc = JsonDocument.Parse(capturedBody!);
        Assert.Multiple(() =>
        {
            Assert.That(doc.RootElement.GetProperty("file_id").GetString(), Is.EqualTo("file-1"));
            Assert.That(doc.RootElement.GetProperty("chunking_strategy").GetProperty("type").GetString(), Is.EqualTo("auto"));
        });
    }

    #endregion

    #region Raw request/response payloads

    [Test]
    public async Task CreateStore_Success_CarriesRawPayloads()
    {
        var client = CreateClient(StoreResponse);
        var result = await client.CreateStoreAsync(new OpenAiVectorStoreCreateRequest { Name = "Test" });

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.RawResponseJson, Is.Not.Null);
            Assert.That(result.RawResponseJson, Does.Contain("vs_abc123"));
            Assert.That(result.RawRequestJson, Is.Not.Null);
            Assert.That(result.RawRequestJson, Does.Contain("Test"));
        });
    }

    [Test]
    public async Task CreateStore_ApiError_CarriesRawPayloads()
    {
        var client = CreateClient("error", HttpStatusCode.BadRequest);
        var result = await client.CreateStoreAsync(new OpenAiVectorStoreCreateRequest { Name = "Test" });

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.RawResponseJson, Is.Not.Null);
            Assert.That(result.RawRequestJson, Is.Not.Null);
        });
    }

    [Test]
    public async Task GetStore_Success_CarriesRawResponseJson()
    {
        var client = CreateClient(StoreResponse);
        var result = await client.GetStoreAsync("vs_abc123");

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.RawResponseJson, Is.Not.Null);
            Assert.That(result.RawResponseJson, Does.Contain("vs_abc123"));
            Assert.That(result.RawRequestJson, Is.Null, "GET requests have no request body");
        });
    }

    #endregion
}
