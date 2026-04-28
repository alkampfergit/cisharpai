using System.Runtime.CompilerServices;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cisharpai;

public sealed class LlmHttpClient
{
    private static readonly Action<ILogger, string, string, string, Exception?> RequestStarted = LoggerMessage.Define<string, string, string>(
        LogLevel.Information,
        new EventId(1000, nameof(RequestStarted)),
        "Sending {HttpMethod} request to {RequestUri} with body {RequestBody}");

    private static readonly Action<ILogger, string, string, int, double, string, Exception?> RequestCompleted = LoggerMessage.Define<string, string, int, double, string>(
        LogLevel.Information,
        new EventId(1001, nameof(RequestCompleted)),
        "{HttpMethod} request to {RequestUri} completed with status {StatusCode} in {ElapsedMilliseconds} ms and body {ResponseBody}");

    private static readonly Action<ILogger, string, string, int, double, string, Exception?> RequestFailed = LoggerMessage.Define<string, string, int, double, string>(
        LogLevel.Warning,
        new EventId(1002, nameof(RequestFailed)),
        "{HttpMethod} request to {RequestUri} failed with status {StatusCode} in {ElapsedMilliseconds} ms and body {ResponseBody}");

    private static readonly Action<ILogger, string, string, int, double, Exception?> StreamStarted = LoggerMessage.Define<string, string, int, double>(
        LogLevel.Information,
        new EventId(1003, nameof(StreamStarted)),
        "{HttpMethod} streaming request to {RequestUri} started with status {StatusCode} in {ElapsedMilliseconds} ms");

    private static readonly Action<ILogger, string, string, int, string, Exception?> StreamChunkReceived = LoggerMessage.Define<string, string, int, string>(
        LogLevel.Debug,
        new EventId(1004, nameof(StreamChunkReceived)),
        "{HttpMethod} streaming response from {RequestUri} yielded chunk {ChunkIndex}: {ResponseChunk}");

    private static readonly Action<ILogger, string, string, int, double, string, Exception?> StreamCompleted = LoggerMessage.Define<string, string, int, double, string>(
        LogLevel.Information,
        new EventId(1005, nameof(StreamCompleted)),
        "{HttpMethod} streaming request to {RequestUri} completed after {ChunkCount} chunks in {ElapsedMilliseconds} ms with completion {CompletionKind}");

    private static readonly JsonSerializerOptions DefaultSerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient _httpClient;
    private readonly ILogger<LlmHttpClient> _logger;
    private readonly JsonSerializerOptions _serializerOptions;

    public LlmHttpClient(
        HttpClient httpClient,
        JsonSerializerOptions? serializerOptions = null,
        ILogger<LlmHttpClient>? logger = null)
    {
        _httpClient = httpClient;
        _serializerOptions = serializerOptions ?? DefaultSerializerOptions;
        _logger = logger ?? NullLogger<LlmHttpClient>.Instance;
    }

    public async Task<TResponse> PostAsync<TRequest, TResponse>(
        string uri,
        TRequest payload,
        JsonElement? extraParameters = null,
        CancellationToken cancellationToken = default)
    {
        var json = SerializeAndMerge(payload, extraParameters);
        var requestUri = ResolveRequestUri(uri);
        using var activity = StartHttpActivity(HttpMethod.Post.Method, requestUri);
        var stopwatch = Stopwatch.StartNew();

        RequestStarted(_logger, HttpMethod.Post.Method, requestUri, json, null);

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, uri)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };

            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);

            activity?.SetTag("http.response.status_code", (int)response.StatusCode);

            var responseBody = await EnsureSuccessOrThrowAsync(
                    response,
                    requestUri,
                    HttpMethod.Post.Method,
                    stopwatch,
                    activity,
                    cancellationToken)
                .ConfigureAwait(false);

            RequestCompleted(
                _logger,
                HttpMethod.Post.Method,
                requestUri,
                (int)response.StatusCode,
                stopwatch.Elapsed.TotalMilliseconds,
                responseBody,
                null);

            var result = JsonSerializer.Deserialize<TResponse>(responseBody, _serializerOptions);

            if (result is null)
                throw new InvalidOperationException("Response body was empty or invalid.");

            activity?.SetStatus(ActivityStatusCode.Ok);
            return result;
        }
        catch (Exception ex) when (activity is not null)
        {
            activity.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
    }

    public async Task<(TResponse Result, string RawResponseJson, string RawRequestJson)> PostWithRawAsync<TRequest, TResponse>(
        string uri,
        TRequest payload,
        JsonElement? extraParameters = null,
        CancellationToken cancellationToken = default)
    {
        var requestJson = SerializeAndMerge(payload, extraParameters);
        var requestUri = ResolveRequestUri(uri);
        using var activity = StartHttpActivity(HttpMethod.Post.Method, requestUri);
        var stopwatch = Stopwatch.StartNew();

        RequestStarted(_logger, HttpMethod.Post.Method, requestUri, requestJson, null);

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, uri)
            {
                Content = new StringContent(requestJson, Encoding.UTF8, "application/json")
            };

            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);

            activity?.SetTag("http.response.status_code", (int)response.StatusCode);

            var rawResponseJson = await EnsureSuccessOrThrowAsync(
                    response,
                    requestUri,
                    HttpMethod.Post.Method,
                    stopwatch,
                    activity,
                    cancellationToken)
                .ConfigureAwait(false);

            RequestCompleted(
                _logger,
                HttpMethod.Post.Method,
                requestUri,
                (int)response.StatusCode,
                stopwatch.Elapsed.TotalMilliseconds,
                rawResponseJson,
                null);

            var result = JsonSerializer.Deserialize<TResponse>(rawResponseJson, _serializerOptions);

            if (result is null)
                throw new InvalidOperationException("Response body was empty or invalid.");

            activity?.SetStatus(ActivityStatusCode.Ok);
            return (result, rawResponseJson, requestJson);
        }
        catch (Exception ex) when (activity is not null)
        {
            activity.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Sends a POST request and yields raw JSON data strings from a Server-Sent Events (SSE) stream.
    /// Each yielded string is the JSON payload from a "data: ..." SSE line.
    /// </summary>
    public async IAsyncEnumerable<string> PostStreamAsync<TRequest>(
        string uri,
        TRequest payload,
        JsonElement? extraParameters = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var json = SerializeAndMerge(payload, extraParameters);
        var requestUri = ResolveRequestUri(uri);
        using var activity = StartHttpActivity(HttpMethod.Post.Method, requestUri);
        activity?.SetTag("cisharpai.stream", true);
        var stopwatch = Stopwatch.StartNew();

        RequestStarted(_logger, HttpMethod.Post.Method, requestUri, json, null);

        using var request = new HttpRequestMessage(HttpMethod.Post, uri)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

        HttpResponseMessage? response = null;
        var chunkCount = 0;
        var completionKind = "incomplete";
        try
        {
            response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);

            activity?.SetTag("http.response.status_code", (int)response.StatusCode);

            if (!response.IsSuccessStatusCode)
            {
                await EnsureSuccessOrThrowAsync(
                        response,
                        requestUri,
                        HttpMethod.Post.Method,
                        stopwatch,
                        activity,
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            StreamStarted(
                _logger,
                HttpMethod.Post.Method,
                requestUri,
                (int)response.StatusCode,
                stopwatch.Elapsed.TotalMilliseconds,
                null);

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken)
                .ConfigureAwait(false);
            using var reader = new StreamReader(stream);

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);

                if (line is null) break;                         // end of stream
                if (string.IsNullOrEmpty(line)) continue;       // blank lines between events
                if (line.StartsWith("event:", StringComparison.Ordinal)) continue;  // named event lines (Anthropic/Cohere)
                if (line == "data: [DONE]")
                {
                    completionKind = "done";
                    StreamCompleted(
                        _logger,
                        HttpMethod.Post.Method,
                        requestUri,
                        chunkCount,
                        stopwatch.Elapsed.TotalMilliseconds,
                        completionKind,
                        null);
                    yield break;
                }
                if (!line.StartsWith("data: ", StringComparison.Ordinal)) continue; // skip non-data lines

                var dataJson = line.Substring(6); // strip "data: " prefix
                chunkCount++;
                StreamChunkReceived(_logger, HttpMethod.Post.Method, requestUri, chunkCount, dataJson, null);
                yield return dataJson;
            }

            completionKind = "end_of_stream";
            StreamCompleted(
                _logger,
                HttpMethod.Post.Method,
                requestUri,
                chunkCount,
                stopwatch.Elapsed.TotalMilliseconds,
                completionKind,
                null);
        }
        finally
        {
            if (activity is not null)
            {
                activity.SetTag("cisharpai.stream.chunks", chunkCount);
                activity.SetTag("cisharpai.stream.completion_kind", completionKind);
                if (activity.Status == ActivityStatusCode.Unset && completionKind != "incomplete")
                    activity.SetStatus(ActivityStatusCode.Ok);
            }
            response?.Dispose();
        }
    }

    private async Task<string> EnsureSuccessOrThrowAsync(
        HttpResponseMessage response,
        string requestUri,
        string httpMethod,
        Stopwatch stopwatch,
        Activity? activity,
        CancellationToken cancellationToken)
    {
        string responseBody;
        try
        {
            responseBody = await response.Content.ReadAsStringAsync(cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            var detail = ex.InnerException?.Message ?? ex.Message;
            responseBody = $"[Failed to read response body: {detail}]";
        }

        if (response.IsSuccessStatusCode)
            return responseBody;

        activity?.SetStatus(ActivityStatusCode.Error, $"HTTP {(int)response.StatusCode}");

        RequestFailed(
            _logger,
            httpMethod,
            requestUri,
            (int)response.StatusCode,
            stopwatch.Elapsed.TotalMilliseconds,
            responseBody,
            null);

        throw new LlmHttpRequestException(response.StatusCode, responseBody);
    }

    private static Activity? StartHttpActivity(string httpMethod, string requestUri)
    {
        if (!CisharpaiTelemetry.ActivitySource.HasListeners())
            return null;

        var spanName = $"{httpMethod} {ExtractPath(requestUri)}";
        var activity = CisharpaiTelemetry.ActivitySource.StartActivity(spanName, ActivityKind.Client);
        if (activity is null)
            return null;

        activity.SetTag("http.request.method", httpMethod);
        activity.SetTag("url.full", requestUri);
        if (Uri.TryCreate(requestUri, UriKind.Absolute, out var parsed))
            activity.SetTag("server.address", parsed.Host);

        return activity;
    }

    private static string ExtractPath(string requestUri)
    {
        if (Uri.TryCreate(requestUri, UriKind.Absolute, out var parsed))
            return parsed.AbsolutePath.TrimStart('/');
        return requestUri;
    }

    private string SerializeAndMerge<TRequest>(TRequest payload, JsonElement? extraParameters)
    {
        var json = JsonSerializer.Serialize(payload, _serializerOptions);

        if (extraParameters.HasValue && extraParameters.Value.ValueKind == JsonValueKind.Object)
        {
            json = JsonDeepMerge.Merge(json, extraParameters.Value);
        }

        return json;
    }

    private string ResolveRequestUri(string uri)
    {
        if (Uri.TryCreate(uri, UriKind.Absolute, out var absoluteUri))
            return absoluteUri.ToString();

        return _httpClient.BaseAddress is null
            ? uri
            : new Uri(_httpClient.BaseAddress, uri).ToString();
    }
}
