using Cisharpai.Features;
using Cisharpai.Helpers;
using Cisharpai.Models;
using Cisharpai.Azure.AzureOpenAi.Models;

namespace Cisharpai.Azure.AzureOpenAi;

/// <summary>
/// Azure OpenAI embedding client using HttpClient.
/// Supports text-embedding-ada-002, text-embedding-3-small, text-embedding-3-large deployments.
/// </summary>
public sealed class AzureOpenAiEmbeddingClient : IEmbeddingClient
{
    private readonly LlmHttpClient _client;
    private readonly AzureOpenAiClientOptions _options;

    public IFeatureCollection Features { get; } = new FeatureCollection();

    public AzureOpenAiEmbeddingClient(
        HttpClient httpClient,
        AzureOpenAiClientOptions options)
    {
        _client = new LlmHttpClient(httpClient);
        _options = options;
    }

    public async Task<EmbeddingResponse> GetEmbeddingsAsync(
        EmbeddingRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var providerRequest = new AzureOpenAiEmbeddingRequest
            {
                Input = request.Input.Count == 1 ? request.Input[0] : request.Input,
                EncodingFormat = request.EncodingFormat,
                Dimensions = request.Dimensions
            };

            // Azure OpenAI endpoint format:
            // POST /openai/deployments/{deployment}/embeddings?api-version={version}
            var uri = $"openai/deployments/{_options.DeploymentName}/embeddings?api-version={_options.ApiVersion}";

            string? rawResponseJson = null;
            string? rawRequestJson = null;
            AzureOpenAiEmbeddingResponse raw;

            if (request.IncludeRawResponse)
            {
                (raw, rawResponseJson, rawRequestJson) = await _client.PostWithRawAsync<
                    AzureOpenAiEmbeddingRequest,
                    AzureOpenAiEmbeddingResponse>(
                    uri, providerRequest, request.ExtraParameters, cancellationToken);
            }
            else
            {
                raw = await _client.PostAsync<
                    AzureOpenAiEmbeddingRequest,
                    AzureOpenAiEmbeddingResponse>(
                    uri, providerRequest, request.ExtraParameters, cancellationToken);
            }

            return MapResponse(raw, request.EncodingFormat, rawResponseJson, rawRequestJson);
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
        AzureOpenAiEmbeddingResponse raw,
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
