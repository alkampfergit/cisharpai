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

    #endregion
}
