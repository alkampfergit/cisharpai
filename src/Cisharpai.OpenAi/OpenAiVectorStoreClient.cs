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
        JsonElement? extraParameters = null,
        CancellationToken cancellationToken = default)
    {
        return await PostAsync<OpenAiVectorStoreCreateRequest, OpenAiVectorStore>(
            "vector_stores", request, extraParameters, cancellationToken);
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
        if (after is not null) query += $"&after={Uri.EscapeDataString(after)}";
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
                return VectorStoreResult<OpenAiUploadedFile>.Error(
                    $"HTTP {(int)response.StatusCode}: {body}", rawResponseJson: body);

            var result = JsonSerializer.Deserialize<OpenAiUploadedFile>(body, JsonOptions);
            return result is not null
                ? VectorStoreResult<OpenAiUploadedFile>.Success(result, rawResponseJson: body)
                : VectorStoreResult<OpenAiUploadedFile>.Error("Failed to deserialize upload response.", rawResponseJson: body);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return VectorStoreResult<OpenAiUploadedFile>.Error(ex.Message);
        }
    }

    public async Task<VectorStoreResult<OpenAiVectorStoreFile>> AddFileToStoreAsync(
        string vectorStoreId,
        string fileId,
        JsonElement? extraParameters = null,
        CancellationToken cancellationToken = default)
    {
        var request = new { file_id = fileId };
        return await PostAsync<object, OpenAiVectorStoreFile>(
            $"vector_stores/{vectorStoreId}/files", request, extraParameters, cancellationToken);
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
        if (after is not null) query += $"&after={Uri.EscapeDataString(after)}";
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
    /// Uses a deadline-linked cancellation token so that both in-flight requests and
    /// delays are cancelled when the deadline passes.
    /// </summary>
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

        using var deadlineCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadlineCts.CancelAfter(effectiveTimeout);
        var deadlineToken = deadlineCts.Token;

        try
        {
            while (true)
            {
                deadlineToken.ThrowIfCancellationRequested();

                var result = await GetFileInStoreAsync(vectorStoreId, fileId, deadlineToken);
                if (!result.IsSuccess)
                    return result;

                var status = result.Value!.Status;
                if (status is "completed" or "failed" or "cancelled")
                    return result;

                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.LogDebug("File {FileId} in store {StoreId} status: {Status}, polling again in {Interval}ms",
                        fileId, vectorStoreId, status, effectiveInterval.TotalMilliseconds);
                }

                var remaining = deadline - DateTimeOffset.UtcNow;
                if (remaining <= TimeSpan.Zero)
                    break;

                var delay = effectiveInterval < remaining ? effectiveInterval : remaining;
                await Task.Delay(delay, deadlineToken);
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // Deadline expired, not caller cancellation
        }

        return VectorStoreResult<OpenAiVectorStoreFile>.Error(
            $"Timeout after {effectiveTimeout.TotalSeconds}s waiting for file '{fileId}' in store '{vectorStoreId}' to reach terminal status.");
    }

    private async Task<VectorStoreResult<TResponse>> PostAsync<TRequest, TResponse>(
        string uri, TRequest payload, JsonElement? extraParameters, CancellationToken cancellationToken)
    {
        try
        {
            var json = JsonSerializer.Serialize(payload, JsonOptions);
            if (extraParameters.HasValue && extraParameters.Value.ValueKind == JsonValueKind.Object)
                json = JsonDeepMerge.Merge(json, extraParameters.Value);

            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            using var response = await _httpClient.PostAsync(uri, content, cancellationToken);
            return await ReadResponseAsync<TResponse>(response, json, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
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
            return await ReadResponseAsync<TResponse>(response, rawRequestJson: null, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
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
            return await ReadResponseAsync<TResponse>(response, rawRequestJson: null, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return VectorStoreResult<TResponse>.Error(ex.Message);
        }
    }

    private static async Task<VectorStoreResult<TResponse>> ReadResponseAsync<TResponse>(
        HttpResponseMessage response, string? rawRequestJson, CancellationToken cancellationToken)
    {
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
            return VectorStoreResult<TResponse>.Error(
                $"HTTP {(int)response.StatusCode}: {body}",
                rawResponseJson: body,
                rawRequestJson: rawRequestJson);

        try
        {
            var result = JsonSerializer.Deserialize<TResponse>(body, JsonOptions);
            return result is not null
                ? VectorStoreResult<TResponse>.Success(result, rawResponseJson: body, rawRequestJson: rawRequestJson)
                : VectorStoreResult<TResponse>.Error("Failed to deserialize response.", rawResponseJson: body, rawRequestJson: rawRequestJson);
        }
        catch (JsonException ex)
        {
            return VectorStoreResult<TResponse>.Error(
                $"Deserialization failed: {ex.Message}",
                rawResponseJson: body,
                rawRequestJson: rawRequestJson);
        }
    }
}
