using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Cisharpai;

public sealed class LlmHttpClient
{
    private readonly HttpClient _httpClient;
    private readonly JsonSerializerOptions _serializerOptions;

    public LlmHttpClient(
        HttpClient httpClient,
        JsonSerializerOptions? serializerOptions = null)
    {
        _httpClient = httpClient;
        _serializerOptions = serializerOptions ?? new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
    }

    public async Task<TResponse> PostAsync<TRequest, TResponse>(
        string uri,
        TRequest payload,
        CancellationToken cancellationToken = default,
        JsonElement? extraParameters = null)
    {
        var json = SerializeAndMerge(payload, extraParameters);

        using var request = new HttpRequestMessage(HttpMethod.Post, uri)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            string? responseBody = null;
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

            throw new LlmHttpRequestException(response.StatusCode, responseBody);
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken)
            .ConfigureAwait(false);
        var result = await JsonSerializer.DeserializeAsync<TResponse>(
            stream, _serializerOptions, cancellationToken)
            .ConfigureAwait(false);

        if (result is null)
            throw new InvalidOperationException("Response body was empty or invalid.");

        return result;
    }

    public async Task<(TResponse Result, string RawResponseJson, string RawRequestJson)> PostWithRawAsync<TRequest, TResponse>(
        string uri,
        TRequest payload,
        CancellationToken cancellationToken = default,
        JsonElement? extraParameters = null)
    {
        var requestJson = SerializeAndMerge(payload, extraParameters);

        using var request = new HttpRequestMessage(HttpMethod.Post, uri)
        {
            Content = new StringContent(requestJson, Encoding.UTF8, "application/json")
        };

        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            string? responseBody = null;
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

            throw new LlmHttpRequestException(response.StatusCode, responseBody);
        }

        var rawResponseJson = await response.Content.ReadAsStringAsync(cancellationToken)
            .ConfigureAwait(false);

        var result = JsonSerializer.Deserialize<TResponse>(rawResponseJson, _serializerOptions);

        if (result is null)
            throw new InvalidOperationException("Response body was empty or invalid.");

        return (result, rawResponseJson, requestJson);
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
}
