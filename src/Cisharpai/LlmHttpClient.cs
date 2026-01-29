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
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, uri)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(payload, _serializerOptions),
                Encoding.UTF8,
                "application/json")
        };

        using var response = await _httpClient.SendAsync(request, cancellationToken)
            .ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            string? responseBody = null;
            try
            {
                responseBody = await response.Content.ReadAsStringAsync(cancellationToken)
                    .ConfigureAwait(false);
            }
            catch
            {
                // If we can't read the body, we still want to throw with whatever we have.
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

    public async Task<(TResponse Result, string RawJson)> PostWithRawAsync<TRequest, TResponse>(
        string uri,
        TRequest payload,
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, uri)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(payload, _serializerOptions),
                Encoding.UTF8,
                "application/json")
        };

        using var response = await _httpClient.SendAsync(request, cancellationToken)
            .ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            string? responseBody = null;
            try
            {
                responseBody = await response.Content.ReadAsStringAsync(cancellationToken)
                    .ConfigureAwait(false);
            }
            catch
            {
                // If we can't read the body, we still want to throw with whatever we have.
            }

            throw new LlmHttpRequestException(response.StatusCode, responseBody);
        }

        var rawJson = await response.Content.ReadAsStringAsync(cancellationToken)
            .ConfigureAwait(false);

        var result = JsonSerializer.Deserialize<TResponse>(rawJson, _serializerOptions);

        if (result is null)
            throw new InvalidOperationException("Response body was empty or invalid.");

        return (result, rawJson);
    }
}
