using System.Text.Json;
using Cisharpai.Models;
using Cisharpai.Azure.AzureAiInference.Models;

namespace Cisharpai.Azure.AzureAiInference;

/// <summary>
/// Azure AI Inference embedding client using HttpClient.
/// Supports Azure AI model-as-a-service offerings for text embeddings.
/// </summary>
public sealed class AzureAiInferenceEmbeddingClient : IEmbeddingClient
{
    private readonly LlmHttpClient _client;
    private readonly AzureAiInferenceClientOptions _options;

    public AzureAiInferenceEmbeddingClient(
        HttpClient httpClient,
        AzureAiInferenceClientOptions options)
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
            var providerRequest = new AzureAiInferenceEmbeddingRequest
            {
                Model = !string.IsNullOrWhiteSpace(request.Model)
                    ? request.Model
                    : _options.ModelId,
                Input = request.Input.Count == 1 ? request.Input[0] : request.Input,
                Dimensions = request.Dimensions,
                EncodingFormat = request.EncodingFormat,
                InputType = MapInputType(request.InputType)
            };

            // Azure AI Inference endpoint format:
            // POST /models/embeddings?api-version=2024-05-01-preview
            var uri = $"models/embeddings?api-version={_options.ApiVersion}";

            string? rawResponseJson = null;
            string? rawRequestJson = null;
            AzureAiInferenceEmbeddingResponse raw;

            if (request.IncludeRawResponse)
            {
                (raw, rawResponseJson, rawRequestJson) = await _client.PostWithRawAsync<
                    AzureAiInferenceEmbeddingRequest,
                    AzureAiInferenceEmbeddingResponse>(
                    uri, providerRequest, cancellationToken, request.ExtraParameters);
            }
            else
            {
                raw = await _client.PostAsync<
                    AzureAiInferenceEmbeddingRequest,
                    AzureAiInferenceEmbeddingResponse>(
                    uri, providerRequest, cancellationToken, request.ExtraParameters);
            }

            return MapResponse(raw, request.EncodingFormat, rawResponseJson, rawRequestJson);
        }
        catch (LlmHttpRequestException ex)
        {
            return EmbeddingResponse.Error(ex.Message, ex.ResponseBody);
        }
        catch (Exception ex)
        {
            return EmbeddingResponse.Error($"Unexpected error: {ex.Message}");
        }
    }

    private static string? MapInputType(EmbeddingInputType? inputType) => inputType switch
    {
        EmbeddingInputType.Query => "query",
        EmbeddingInputType.Document => "document",
        EmbeddingInputType.Classification => "classification",
        EmbeddingInputType.Clustering => "clustering",
        _ => null
    };

    private EmbeddingResponse MapResponse(
        AzureAiInferenceEmbeddingResponse raw,
        string? encodingFormat,
        string? rawResponseJson = null,
        string? rawRequestJson = null)
    {
        var embeddings = new List<float[]>();
        var base64Embeddings = new List<string>();
        int? dimensions = null;

        foreach (var item in raw.Data.OrderBy(d => d.Index))
        {
            if (encodingFormat == "base64" &&
                item.Embedding.ValueKind == JsonValueKind.String)
            {
                base64Embeddings.Add(item.Embedding.GetString() ?? string.Empty);
            }
            else if (item.Embedding.ValueKind == JsonValueKind.Array)
            {
                var vector = item.Embedding
                    .EnumerateArray()
                    .Select(e => e.GetSingle())
                    .ToArray();
                embeddings.Add(vector);

                if (!dimensions.HasValue && vector.Length > 0)
                {
                    dimensions = vector.Length;
                }
            }
        }

        return new EmbeddingResponse(
            Embeddings: embeddings,
            Base64Embeddings: base64Embeddings.Count > 0 ? base64Embeddings : null,
            Model: raw.Model ?? _options.ModelId,
            TotalTokens: raw.Usage?.TotalTokens ?? raw.Usage?.PromptTokens ?? 0,
            Dimensions: dimensions,
            RawResponseJson: rawResponseJson,
            RawRequestJson: rawRequestJson,
            IsSuccess: true,
            ErrorMessage: null);
    }
}
