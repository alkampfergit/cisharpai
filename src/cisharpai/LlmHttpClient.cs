using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace cisharpai;

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
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken)
            .ConfigureAwait(false);
        var result = await JsonSerializer.DeserializeAsync<TResponse>(
            stream, _serializerOptions, cancellationToken)
            .ConfigureAwait(false);

        if (result is null)
            throw new InvalidOperationException("Response body was empty or invalid.");

        return result;
    }
}
