using System.Net;
using Cisharpai.OpenAi;
using Cisharpai.OpenAi.Models;

namespace Cisharpai.Tests.OpenAi;

public sealed class OpenAiVectorStoreClientAdditionalTests
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

    #endregion

    #region ListStores

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

    #endregion

    #region DeleteStore

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

    #region UploadFile - exception paths

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

    #endregion

    #region PollFileUntilProcessed - error mid-poll

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

    #region CreateStoreAsync - exception paths

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

    #endregion

    #region Static Create factory

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

    #region Deserialization failure

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

    #endregion
}
