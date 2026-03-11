using System.Text.Json;
using Cisharpai.Features;
using Cisharpai.Models;
using Cisharpai.OpenAi.Models;

namespace Cisharpai.OpenAi;

public sealed class OpenAiEmbeddingClient : IEmbeddingClient
{
    private const string EmbeddingsEndpoint = "embeddings";

    private readonly LlmHttpClient _client;
    private readonly OpenAiClientOptions _options;

    public IFeatureCollection Features { get; } = new FeatureCollection();

    public OpenAiEmbeddingClient(HttpClient httpClient, OpenAiClientOptions options)
    {
        _client = new LlmHttpClient(httpClient);
        _options = options;
    }

    public async Task<EmbeddingResponse> GetEmbeddingsAsync(
        EmbeddingRequest request,
        CancellationToken cancellationToken = default)
    {
        var model = request.Model
            ?? _options.DefaultModel
            ?? throw new InvalidOperationException(
                "Model must be specified either in the request or via DefaultModel in options.");

        try
        {
            var providerRequest = new OpenAiEmbeddingRequest
            {
                Model = model,
                Input = request.Input.Count == 1 ? request.Input[0] : (object)request.Input,
                EncodingFormat = request.EncodingFormat,
                Dimensions = request.Dimensions
            };

            if (request.IncludeRawResponse)
            {
                var (raw, rawResponseJson, rawRequestJson) = await _client.PostWithRawAsync<OpenAiEmbeddingRequest, OpenAiEmbeddingResponse>(
                    EmbeddingsEndpoint, providerRequest, cancellationToken, request.ExtraParameters);
                return MapResponse(raw, request.EncodingFormat, rawResponseJson, rawRequestJson);
            }

            return MapResponse(
                await _client.PostAsync<OpenAiEmbeddingRequest, OpenAiEmbeddingResponse>(
                    EmbeddingsEndpoint, providerRequest, cancellationToken, request.ExtraParameters),
                request.EncodingFormat);
        }
        catch (LlmHttpRequestException ex)
        {
            return EmbeddingResponse.Error(ex.Message, ex.ResponseBody);
        }
        catch (Exception ex)
        {
            return EmbeddingResponse.Error(ex.Message);
        }
    }

    private static EmbeddingResponse MapResponse(
        OpenAiEmbeddingResponse raw,
        string? encodingFormat,
        string? rawResponseJson = null,
        string? rawRequestJson = null)
    {
        var orderedData = raw.Data.OrderBy(d => d.Index).ToList();

        var isBase64 = string.Equals(encodingFormat, "base64", StringComparison.OrdinalIgnoreCase);

        List<float[]> embeddings;
        IReadOnlyList<string>? base64Embeddings = null;

        if (isBase64)
        {
            embeddings = [];
            base64Embeddings = orderedData
                .Select(d => d.Embedding.GetString() ?? string.Empty)
                .ToList();
        }
        else
        {
            embeddings = orderedData
                .Select(d => d.Embedding.EnumerateArray().Select(e => e.GetSingle()).ToArray())
                .ToList();
        }

        var dimensions = !isBase64 && embeddings.Count > 0
            ? embeddings[0].Length
            : (int?)null;

        return new EmbeddingResponse(
            Embeddings: embeddings,
            Base64Embeddings: base64Embeddings,
            Model: raw.Model,
            TotalTokens: raw.Usage.TotalTokens,
            Dimensions: dimensions,
            RawResponseJson: rawResponseJson,
            RawRequestJson: rawRequestJson);
    }
}
