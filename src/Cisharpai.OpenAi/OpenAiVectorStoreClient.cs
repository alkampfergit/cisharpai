using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Cisharpai.OpenAi.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cisharpai.OpenAi;

/// <summary>
/// Wraps the OpenAI Vector Stores and Files APIs for store and file lifecycle management.
/// This is a provider-specific client — not a generic storage abstraction. The genericity
/// constraint binds the retrieval abstraction (<c>IRetriever</c>), not this management surface.
/// <para>
/// <strong>Scope boundary:</strong> this client wraps provider-hosted file and store APIs.
/// Local file management, document parsing, and storage abstractions over third-party stores
/// are out of scope.
/// </para>
/// </summary>
public sealed class OpenAiVectorStoreClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient _httpClient;
    private readonly ILogger _logger;

    public OpenAiVectorStoreClient(HttpClient httpClient, ILoggerFactory? loggerFactory = null)
    {
        _httpClient = httpClient;
        _logger = loggerFactory?.CreateLogger<OpenAiVectorStoreClient>() ?? NullLogger<OpenAiVectorStoreClient>.Instance;
    }

    public static OpenAiVectorStoreClient Create(OpenAiClientOptions options, ILoggerFactory? loggerFactory = null)
    {
        var handler = new OpenAiAuthenticationHandler(options) { InnerHandler = new HttpClientHandler() };
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri(options.BaseUrl),
            Timeout = TimeSpan.FromMinutes(5)
        };
        return new OpenAiVectorStoreClient(httpClient, loggerFactory);
    }

    public async Task<VectorStoreResult<OpenAiVectorStore>> CreateStoreAsync(
        OpenAiVectorStoreCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        return await PostAsync<OpenAiVectorStoreCreateRequest, OpenAiVectorStore>(
            "vector_stores", request, cancellationToken);
    }

    public async Task<VectorStoreResult<OpenAiVectorStore>> GetStoreAsync(
        string vectorStoreId,
        CancellationToken cancellationToken = default)
    {
        return await GetAsync<OpenAiVectorStore>(
            $"vector_stores/{vectorStoreId}", cancellationToken);
    }

    public async Task<VectorStoreResult<OpenAiVectorStoreListResponse>> ListStoresAsync(
        int limit = 20,
        string? after = null,
        CancellationToken cancellationToken = default)
    {
        var query = $"vector_stores?limit={limit}";
        if (after is not null) query += $"&after={after}";
        return await GetAsync<OpenAiVectorStoreListResponse>(query, cancellationToken);
    }

    public async Task<VectorStoreResult<OpenAiDeleteResponse>> DeleteStoreAsync(
        string vectorStoreId,
        CancellationToken cancellationToken = default)
    {
        return await DeleteAsync<OpenAiDeleteResponse>(
            $"vector_stores/{vectorStoreId}", cancellationToken);
    }

    public async Task<VectorStoreResult<OpenAiUploadedFile>> UploadFileAsync(
        Stream fileContent,
        string filename,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var content = new MultipartFormDataContent();
            var streamContent = new StreamContent(fileContent);
            streamContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            content.Add(streamContent, "file", filename);
            content.Add(new StringContent("assistants"), "purpose");

            using var response = await _httpClient.PostAsync("files", content, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
                return VectorStoreResult<OpenAiUploadedFile>.Error($"HTTP {(int)response.StatusCode}: {body}");

            var result = JsonSerializer.Deserialize<OpenAiUploadedFile>(body, JsonOptions);
            return result is not null
                ? VectorStoreResult<OpenAiUploadedFile>.Success(result)
                : VectorStoreResult<OpenAiUploadedFile>.Error("Failed to deserialize upload response.");
        }
        catch (Exception ex)
        {
            return VectorStoreResult<OpenAiUploadedFile>.Error(ex.Message);
        }
    }

    public async Task<VectorStoreResult<OpenAiVectorStoreFile>> AddFileToStoreAsync(
        string vectorStoreId,
        string fileId,
        CancellationToken cancellationToken = default)
    {
        var request = new { file_id = fileId };
        return await PostAsync<object, OpenAiVectorStoreFile>(
            $"vector_stores/{vectorStoreId}/files", request, cancellationToken);
    }

    public async Task<VectorStoreResult<OpenAiVectorStoreFile>> GetFileInStoreAsync(
        string vectorStoreId,
        string fileId,
        CancellationToken cancellationToken = default)
    {
        return await GetAsync<OpenAiVectorStoreFile>(
            $"vector_stores/{vectorStoreId}/files/{fileId}", cancellationToken);
    }

    public async Task<VectorStoreResult<OpenAiVectorStoreFileListResponse>> ListFilesInStoreAsync(
        string vectorStoreId,
        int limit = 20,
        string? after = null,
        CancellationToken cancellationToken = default)
    {
        var query = $"vector_stores/{vectorStoreId}/files?limit={limit}";
        if (after is not null) query += $"&after={after}";
        return await GetAsync<OpenAiVectorStoreFileListResponse>(query, cancellationToken);
    }

    public async Task<VectorStoreResult<OpenAiDeleteResponse>> DeleteFileFromStoreAsync(
        string vectorStoreId,
        string fileId,
        CancellationToken cancellationToken = default)
    {
        return await DeleteAsync<OpenAiDeleteResponse>(
            $"vector_stores/{vectorStoreId}/files/{fileId}", cancellationToken);
    }

    public async Task<VectorStoreResult<OpenAiDeleteResponse>> DeleteFileAsync(
        string fileId,
        CancellationToken cancellationToken = default)
    {
        return await DeleteAsync<OpenAiDeleteResponse>($"files/{fileId}", cancellationToken);
    }

    /// <summary>
    /// Polls the file status in a vector store until it reaches a terminal state
    /// (<c>completed</c>, <c>failed</c>, or <c>cancelled</c>) or the timeout expires.
    /// </summary>
    /// <param name="vectorStoreId">The vector store ID.</param>
    /// <param name="fileId">The file ID to poll.</param>
    /// <param name="timeout">Maximum time to wait. Defaults to 5 minutes.</param>
    /// <param name="pollInterval">Interval between polls. Defaults to 1 second.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The final file status, or an error if the timeout was reached or an API call failed.</returns>
    public async Task<VectorStoreResult<OpenAiVectorStoreFile>> PollFileUntilProcessedAsync(
        string vectorStoreId,
        string fileId,
        TimeSpan? timeout = null,
        TimeSpan? pollInterval = null,
        CancellationToken cancellationToken = default)
    {
        var effectiveTimeout = timeout ?? TimeSpan.FromMinutes(5);
        var effectiveInterval = pollInterval ?? TimeSpan.FromSeconds(1);
        var deadline = DateTimeOffset.UtcNow + effectiveTimeout;

        while (DateTimeOffset.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var result = await GetFileInStoreAsync(vectorStoreId, fileId, cancellationToken);
            if (!result.IsSuccess)
                return result;

            var status = result.Value!.Status;
            if (status is "completed" or "failed" or "cancelled")
                return result;

            _logger.LogDebug("File {FileId} in store {StoreId} status: {Status}, polling again in {Interval}ms",
                fileId, vectorStoreId, status, effectiveInterval.TotalMilliseconds);

            await Task.Delay(effectiveInterval, cancellationToken);
        }

        return VectorStoreResult<OpenAiVectorStoreFile>.Error(
            $"Timeout after {effectiveTimeout.TotalSeconds}s waiting for file '{fileId}' in store '{vectorStoreId}' to reach terminal status.");
    }

    private async Task<VectorStoreResult<TResponse>> PostAsync<TRequest, TResponse>(
        string uri, TRequest payload, CancellationToken cancellationToken)
    {
        try
        {
            var json = JsonSerializer.Serialize(payload, JsonOptions);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            using var response = await _httpClient.PostAsync(uri, content, cancellationToken);
            return await ReadResponseAsync<TResponse>(response, cancellationToken);
        }
        catch (Exception ex)
        {
            return VectorStoreResult<TResponse>.Error(ex.Message);
        }
    }

    private async Task<VectorStoreResult<TResponse>> GetAsync<TResponse>(
        string uri, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await _httpClient.GetAsync(uri, cancellationToken);
            return await ReadResponseAsync<TResponse>(response, cancellationToken);
        }
        catch (Exception ex)
        {
            return VectorStoreResult<TResponse>.Error(ex.Message);
        }
    }

    private async Task<VectorStoreResult<TResponse>> DeleteAsync<TResponse>(
        string uri, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await _httpClient.DeleteAsync(uri, cancellationToken);
            return await ReadResponseAsync<TResponse>(response, cancellationToken);
        }
        catch (Exception ex)
        {
            return VectorStoreResult<TResponse>.Error(ex.Message);
        }
    }

    private static async Task<VectorStoreResult<TResponse>> ReadResponseAsync<TResponse>(
        HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
            return VectorStoreResult<TResponse>.Error($"HTTP {(int)response.StatusCode}: {body}");

        var result = JsonSerializer.Deserialize<TResponse>(body, JsonOptions);
        return result is not null
            ? VectorStoreResult<TResponse>.Success(result)
            : VectorStoreResult<TResponse>.Error("Failed to deserialize response.");
    }
}
