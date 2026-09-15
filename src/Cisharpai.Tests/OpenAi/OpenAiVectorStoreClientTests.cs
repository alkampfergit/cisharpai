using System.Net;
using System.Text.Json;
using Cisharpai.OpenAi;
using Cisharpai.OpenAi.Models;

namespace Cisharpai.Tests.OpenAi;

public sealed class OpenAiVectorStoreClientTests
{
    #region Response Fixtures

    private const string CreateStoreResponse = """
        {
            "id": "vs_abc123",
            "object": "vector_store",
            "name": "Test Store",
            "status": "completed",
            "file_counts": { "in_progress": 0, "completed": 2, "failed": 0, "cancelled": 0, "total": 2 },
            "metadata": {},
            "created_at": 1700000000
        }
        """;

    private const string ListStoresResponse = """
        {
            "data": [
                { "id": "vs_1", "object": "vector_store", "name": "Store 1", "status": "completed", "file_counts": { "in_progress": 0, "completed": 1, "failed": 0, "cancelled": 0, "total": 1 }, "created_at": 1700000000 },
                { "id": "vs_2", "object": "vector_store", "name": "Store 2", "status": "completed", "file_counts": { "in_progress": 0, "completed": 3, "failed": 0, "cancelled": 0, "total": 3 }, "created_at": 1700000001 }
            ],
            "has_more": false
        }
        """;

    private const string DeleteResponse = """
        { "id": "vs_abc123", "deleted": true }
        """;

    private const string FileUploadResponse = """
        {
            "id": "file-xyz789",
            "object": "file",
            "bytes": 1024,
            "filename": "test.pdf",
            "purpose": "assistants",
            "created_at": 1700000010
        }
        """;

    private const string FileInStoreInProgressResponse = """
        {
            "id": "file-xyz789",
            "object": "vector_store.file",
            "vector_store_id": "vs_abc123",
            "status": "in_progress",
            "created_at": 1700000010
        }
        """;

    private const string FileInStoreCompletedResponse = """
        {
            "id": "file-xyz789",
            "object": "vector_store.file",
            "vector_store_id": "vs_abc123",
            "status": "completed",
            "created_at": 1700000010
        }
        """;

    private const string FileInStoreFailedResponse = """
        {
            "id": "file-xyz789",
            "object": "vector_store.file",
            "vector_store_id": "vs_abc123",
            "status": "failed",
            "last_error": { "code": "unsupported_file", "message": "File type not supported." },
            "created_at": 1700000010
        }
        """;

    #endregion

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

    #endregion

    #region CreateStore

    [Test]
    public async Task CreateStore_Success_ReturnsStore()
    {
        var client = CreateClient(CreateStoreResponse);
        var result = await client.CreateStoreAsync(new OpenAiVectorStoreCreateRequest { Name = "Test Store" });

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value!.Id, Is.EqualTo("vs_abc123"));
            Assert.That(result.Value.Name, Is.EqualTo("Test Store"));
        });
    }

    [Test]
    public async Task CreateStore_ApiError_ReturnsFailure()
    {
        var client = CreateClient("error", HttpStatusCode.BadRequest);
        var result = await client.CreateStoreAsync(new OpenAiVectorStoreCreateRequest { Name = "Test" });

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.ErrorMessage, Does.Contain("400"));
        });
    }

    [Test]
    public async Task CreateStore_NetworkException_ReturnsFailure()
    {
        var client = CreateClient((_, _) => throw new HttpRequestException("Connection refused"));
        var result = await client.CreateStoreAsync(new OpenAiVectorStoreCreateRequest { Name = "Test" });

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.ErrorMessage, Does.Contain("Connection refused"));
        });
    }

    [Test]
    public void CreateStore_CancellationRequested_ThrowsOperationCanceled()
    {
        var client = CreateCancellationAwareClient(CreateStoreResponse);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Assert.That(async () =>
            await client.CreateStoreAsync(new OpenAiVectorStoreCreateRequest { Name = "Test" }, cancellationToken: cts.Token),
            Throws.InstanceOf<OperationCanceledException>());
    }

    [Test]
    public async Task CreateStore_ExtraParameters_AreMergedIntoRequest()
    {
        string? capturedBody = null;
        var client = CreateClient(async (req, _) =>
        {
            capturedBody = await req.Content!.ReadAsStringAsync(CancellationToken.None);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(CreateStoreResponse, System.Text.Encoding.UTF8, "application/json")
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
    public async Task CreateStore_Success_CarriesRawPayloads()
    {
        var client = CreateClient(CreateStoreResponse);
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
    public async Task CreateStore_MalformedJson2xx_CarriesRawPayloads()
    {
        const string malformedBody = "{ not valid json at all }}}";
        string? capturedRequest = null;

        var client = CreateClient(async (req, _) =>
        {
            capturedRequest = await req.Content!.ReadAsStringAsync(CancellationToken.None);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(malformedBody, System.Text.Encoding.UTF8, "application/json")
            };
        });

        var result = await client.CreateStoreAsync(new OpenAiVectorStoreCreateRequest { Name = "test" });

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.ErrorMessage, Does.Contain("Deserialization failed"));
            Assert.That(result.RawResponseJson, Is.EqualTo(malformedBody));
            Assert.That(result.RawRequestJson, Is.Not.Null);
        });
    }

    #endregion

    #region GetStore

    [Test]
    public async Task GetStore_Success_ReturnsStore()
    {
        var client = CreateClient("""
            {
                "id": "vs_abc123",
                "object": "vector_store",
                "name": "My Store",
                "status": "completed",
                "file_counts": { "in_progress": 0, "completed": 1, "failed": 0, "cancelled": 0, "total": 1 },
                "created_at": 1700000000
            }
            """);

        var result = await client.GetStoreAsync("vs_abc123");

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value!.Id, Is.EqualTo("vs_abc123"));
            Assert.That(result.Value.Name, Is.EqualTo("My Store"));
        });
    }

    [Test]
    public async Task GetStore_ApiError_ReturnsFailure()
    {
        var client = CreateClient("not found", HttpStatusCode.NotFound);
        var result = await client.GetStoreAsync("vs_nonexistent");

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.ErrorMessage, Does.Contain("404"));
        });
    }

    [Test]
    public async Task GetStore_NetworkException_ReturnsFailure()
    {
        var client = CreateClient((_, _) => throw new HttpRequestException("Connection refused"));
        var result = await client.GetStoreAsync("vs_abc123");

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.ErrorMessage, Does.Contain("Connection refused"));
        });
    }

    [Test]
    public void GetStore_CancellationRequested_ThrowsOperationCanceled()
    {
        var client = CreateCancellationAwareClient(CreateStoreResponse);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Assert.That(async () => await client.GetStoreAsync("vs_abc123", cts.Token),
            Throws.InstanceOf<OperationCanceledException>());
    }

    [Test]
    public async Task GetStore_NullDeserialization_ReturnsFailure()
    {
        var client = CreateClient("null");
        var result = await client.GetStoreAsync("vs_abc123");

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.ErrorMessage, Does.Contain("deserialize"));
        });
    }

    [Test]
    public async Task GetStore_Success_CarriesRawResponseJson()
    {
        var client = CreateClient(CreateStoreResponse);
        var result = await client.GetStoreAsync("vs_abc123");

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.RawResponseJson, Is.Not.Null);
            Assert.That(result.RawResponseJson, Does.Contain("vs_abc123"));
            Assert.That(result.RawRequestJson, Is.Null, "GET requests have no request body");
        });
    }

    [Test]
    public async Task GetStore_MalformedJson2xx_CarriesRawResponseJson()
    {
        const string malformedBody = "<<<not json>>>";
        var client = CreateClient(malformedBody);

        var result = await client.GetStoreAsync("vs_abc123");

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.ErrorMessage, Does.Contain("Deserialization failed"));
            Assert.That(result.RawResponseJson, Is.EqualTo(malformedBody));
        });
    }

    #endregion

    #region ListStores

    [Test]
    public async Task ListStores_Success_ReturnsStores()
    {
        var client = CreateClient(ListStoresResponse);
        var result = await client.ListStoresAsync();

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value!.Data, Has.Count.EqualTo(2));
            Assert.That(result.Value.HasMore, Is.False);
        });
    }

    [Test]
    public async Task ListStores_WithAfterCursor_IncludesQueryParameter()
    {
        string? capturedUri = null;
        var client = CreateClient(async (req, _) =>
        {
            capturedUri = req.RequestUri?.ToString();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{ "data": [], "has_more": false }""",
                    System.Text.Encoding.UTF8, "application/json")
            };
        });

        await client.ListStoresAsync(limit: 10, after: "vs_cursor");

        Assert.That(capturedUri, Does.Contain("after=vs_cursor"));
        Assert.That(capturedUri, Does.Contain("limit=10"));
    }

    [Test]
    public async Task ListStores_ApiError_ReturnsFailure()
    {
        var client = CreateClient("error", HttpStatusCode.InternalServerError);
        var result = await client.ListStoresAsync();

        Assert.That(result.IsSuccess, Is.False);
    }

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

    #endregion

    #region DeleteStore

    [Test]
    public async Task DeleteStore_Success_ReturnsDeleted()
    {
        var client = CreateClient(DeleteResponse);
        var result = await client.DeleteStoreAsync("vs_abc123");

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value!.Deleted, Is.True);
        });
    }

    [Test]
    public async Task DeleteStore_ApiError_ReturnsFailure()
    {
        var client = CreateClient("error", HttpStatusCode.NotFound);
        var result = await client.DeleteStoreAsync("vs_nonexistent");

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.ErrorMessage, Does.Contain("404"));
        });
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

    #endregion

    #region UploadFile

    [Test]
    public async Task UploadFile_Success_ReturnsFile()
    {
        var client = CreateClient(FileUploadResponse);
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("test content"));

        var result = await client.UploadFileAsync(stream, "test.pdf");

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value!.Id, Is.EqualTo("file-xyz789"));
            Assert.That(result.Value.Filename, Is.EqualTo("test.pdf"));
            Assert.That(result.Value.Purpose, Is.EqualTo("assistants"));
        });
    }

    [Test]
    public async Task UploadFile_ApiError_ReturnsFailure()
    {
        var client = CreateClient("error", (HttpStatusCode)413);
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("content"));

        var result = await client.UploadFileAsync(stream, "huge.bin");

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.ErrorMessage, Does.Contain("413"));
        });
    }

    [Test]
    public async Task UploadFile_NetworkException_ReturnsFailure()
    {
        var client = CreateClient((_, _) => throw new HttpRequestException("Network error"));
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("content"));

        var result = await client.UploadFileAsync(stream, "file.pdf");

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.ErrorMessage, Does.Contain("Network error"));
        });
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

    #region AddFileToStore

    [Test]
    public async Task AddFileToStore_Success_ReturnsFile()
    {
        var client = CreateClient(FileInStoreInProgressResponse);
        var result = await client.AddFileToStoreAsync("vs_abc123", "file-xyz789");

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value!.Status, Is.EqualTo("in_progress"));
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

    #region PollFileUntilProcessed

    [Test]
    public async Task PollFile_AlreadyCompleted_ReturnsImmediately()
    {
        var client = CreateClient(FileInStoreCompletedResponse);
        var result = await client.PollFileUntilProcessedAsync("vs_abc123", "file-xyz789");

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value!.Status, Is.EqualTo("completed"));
        });
    }

    [Test]
    public async Task PollFile_FailedStatus_ReturnsFailedFile()
    {
        var client = CreateClient(FileInStoreFailedResponse);
        var result = await client.PollFileUntilProcessedAsync("vs_abc123", "file-xyz789");

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value!.Status, Is.EqualTo("failed"));
            Assert.That(result.Value.LastError!.Code, Is.EqualTo("unsupported_file"));
        });
    }

    [Test]
    public async Task PollFile_TransitionsToCompleted()
    {
        var callCount = 0;
        var client = CreateClient((_, _) =>
        {
            callCount++;
            var json = callCount < 3 ? FileInStoreInProgressResponse : FileInStoreCompletedResponse;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
            });
        });

        var result = await client.PollFileUntilProcessedAsync(
            "vs_abc123", "file-xyz789",
            pollInterval: TimeSpan.FromMilliseconds(10));

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value!.Status, Is.EqualTo("completed"));
            Assert.That(callCount, Is.GreaterThanOrEqualTo(3));
        });
    }

    [Test]
    public async Task PollFile_Timeout_ReturnsError()
    {
        var client = CreateClient(FileInStoreInProgressResponse);
        var result = await client.PollFileUntilProcessedAsync(
            "vs_abc123", "file-xyz789",
            timeout: TimeSpan.FromMilliseconds(100),
            pollInterval: TimeSpan.FromMilliseconds(30));

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.ErrorMessage, Does.Contain("Timeout"));
        });
    }

    [Test]
    public void PollFile_CancellationRequested_ThrowsOperationCanceled()
    {
        var client = CreateClient(FileInStoreInProgressResponse);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Assert.ThrowsAsync<OperationCanceledException>(() =>
            client.PollFileUntilProcessedAsync("vs_abc123", "file-xyz789", cancellationToken: cts.Token));
    }

    [Test]
    public async Task PollFile_ShortTimeout_DoesNotOvershootByFullInterval()
    {
        var requestCount = 0;
        var client = CreateClient((_, _) =>
        {
            Interlocked.Increment(ref requestCount);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(FileInStoreInProgressResponse, System.Text.Encoding.UTF8, "application/json")
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
                Content = new StringContent(FileInStoreInProgressResponse, System.Text.Encoding.UTF8, "application/json")
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
        var client = CreateClient(FileInStoreInProgressResponse);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Assert.ThrowsAsync<OperationCanceledException>(() =>
            client.PollFileUntilProcessedAsync("vs_abc123", "file-xyz789",
                pollInterval: TimeSpan.FromMilliseconds(10),
                cancellationToken: cts.Token));
    }

    [Test]
    public async Task PollFile_ApiErrorMidPoll_ReturnsFailure()
    {
        var callCount = 0;
        var client = CreateClient((_, _) =>
        {
            callCount++;
            if (callCount <= 2)
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("""
                        {
                            "id": "file-xyz789",
                            "object": "vector_store.file",
                            "vector_store_id": "vs_abc123",
                            "status": "in_progress",
                            "created_at": 1700000010
                        }
                        """, System.Text.Encoding.UTF8, "application/json")
                });
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)
            {
                Content = new StringContent("server error", System.Text.Encoding.UTF8, "application/json")
            });
        });

        var result = await client.PollFileUntilProcessedAsync(
            "vs_abc123", "file-xyz789",
            pollInterval: TimeSpan.FromMilliseconds(10));

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.ErrorMessage, Does.Contain("500"));
        });
    }

    [Test]
    public async Task PollFile_CancelledStatus_ReturnsFile()
    {
        var client = CreateClient("""
            {
                "id": "file-xyz789",
                "object": "vector_store.file",
                "vector_store_id": "vs_abc123",
                "status": "cancelled",
                "created_at": 1700000010
            }
            """);

        var result = await client.PollFileUntilProcessedAsync("vs_abc123", "file-xyz789");

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value!.Status, Is.EqualTo("cancelled"));
        });
    }

    #endregion

    #region ListFilesInStore

    [Test]
    public async Task ListFilesInStore_Success_ReturnsList()
    {
        var client = CreateClient("""
            {
                "data": [
                    { "id": "file-1", "object": "vector_store.file", "vector_store_id": "vs_abc", "status": "completed", "created_at": 1700000000 },
                    { "id": "file-2", "object": "vector_store.file", "vector_store_id": "vs_abc", "status": "in_progress", "created_at": 1700000001 }
                ],
                "has_more": true
            }
            """);

        var result = await client.ListFilesInStoreAsync("vs_abc");

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value!.Data, Has.Count.EqualTo(2));
            Assert.That(result.Value.HasMore, Is.True);
        });
    }

    [Test]
    public async Task ListFilesInStore_WithAfterCursor_IncludesQueryParameter()
    {
        string? capturedUri = null;
        var client = CreateClient(async (req, _) =>
        {
            capturedUri = req.RequestUri?.ToString();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{ "data": [], "has_more": false }""",
                    System.Text.Encoding.UTF8, "application/json")
            };
        });

        await client.ListFilesInStoreAsync("vs_abc", limit: 5, after: "file_cursor");

        Assert.That(capturedUri, Does.Contain("after=file_cursor"));
        Assert.That(capturedUri, Does.Contain("limit=5"));
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

    #region GetFileInStore

    [Test]
    public async Task GetFileInStore_Success_ReturnsFile()
    {
        var client = CreateClient("""
            {
                "id": "file-xyz789",
                "object": "vector_store.file",
                "vector_store_id": "vs_abc123",
                "status": "completed",
                "created_at": 1700000010
            }
            """);

        var result = await client.GetFileInStoreAsync("vs_abc123", "file-xyz789");

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value!.Id, Is.EqualTo("file-xyz789"));
            Assert.That(result.Value.Status, Is.EqualTo("completed"));
        });
    }

    #endregion

    #region DeleteFileFromStore

    [Test]
    public async Task DeleteFileFromStore_Success_ReturnsDeleted()
    {
        var client = CreateClient("""{ "id": "file-xyz789", "deleted": true }""");
        var result = await client.DeleteFileFromStoreAsync("vs_abc123", "file-xyz789");

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value!.Deleted, Is.True);
        });
    }

    [Test]
    public async Task DeleteFileFromStore_ApiError_ReturnsFailure()
    {
        var client = CreateClient("error", HttpStatusCode.NotFound);
        var result = await client.DeleteFileFromStoreAsync("vs_abc123", "file-nonexistent");

        Assert.That(result.IsSuccess, Is.False);
    }

    #endregion

    #region DeleteFile

    [Test]
    public async Task DeleteFile_Success_ReturnsDeleted()
    {
        var client = CreateClient("""{ "id": "file-xyz789", "deleted": true }""");
        var result = await client.DeleteFileAsync("file-xyz789");

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value!.Deleted, Is.True);
        });
    }

    [Test]
    public async Task DeleteFile_ApiError_ReturnsFailure()
    {
        var client = CreateClient("error", HttpStatusCode.NotFound);
        var result = await client.DeleteFileAsync("file-nonexistent");

        Assert.That(result.IsSuccess, Is.False);
    }

    #endregion

    #region Static Create Factory

    [Test]
    public void StaticCreate_ReturnsConfiguredClient()
    {
        var options = new OpenAiClientOptions
        {
            ApiKey = "test-key",
            BaseUrl = "https://api.openai.com/v1/"
        };

        var client = OpenAiVectorStoreClient.Create(options);

        Assert.That(client, Is.Not.Null);
    }

    #endregion
}
