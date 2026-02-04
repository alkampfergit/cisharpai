using Cisharpai.Models;
using Cisharpai.Cohere.Models;

namespace Cisharpai.Cohere;

public sealed class CohereEmbeddingClient : IEmbeddingClient
{
    private const string EmbedEndpoint = "embed";

    private readonly LlmHttpClient _client;

    public CohereEmbeddingClient(HttpClient httpClient)
    {
        _client = new LlmHttpClient(httpClient, new System.Text.Json.JsonSerializerOptions
        {
            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.SnakeCaseLower,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        });
    }

    public async Task<EmbeddingResponse> GetEmbeddingsAsync(
        EmbeddingRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var providerRequest = new CohereEmbedRequest
            {
                Model = request.Model,
                Texts = request.Input.ToList(),
                InputType = MapInputType(request.InputType),
                EmbeddingTypes = ["float"]
            };

            if (request.IncludeRawResponse)
            {
                var (raw, rawResponseJson, rawRequestJson) = await _client.PostWithRawAsync<CohereEmbedRequest, CohereEmbedResponse>(
                    EmbedEndpoint, providerRequest, cancellationToken, request.ExtraParameters);
                return MapResponse(raw, rawResponseJson, rawRequestJson);
            }

            return MapResponse(
                await _client.PostAsync<CohereEmbedRequest, CohereEmbedResponse>(
                    EmbedEndpoint, providerRequest, cancellationToken, request.ExtraParameters));
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

    private static string MapInputType(EmbeddingInputType? inputType) => inputType switch
    {
        EmbeddingInputType.Query => "search_query",
        EmbeddingInputType.Document => "search_document",
        EmbeddingInputType.Classification => "classification",
        EmbeddingInputType.Clustering => "clustering",
        null => "search_document",
        _ => "search_document"
    };

    private static EmbeddingResponse MapResponse(
        CohereEmbedResponse raw,
        string? rawResponseJson = null,
        string? rawRequestJson = null)
    {
        var floatEmbeddings = raw.Embeddings.Float;

        IReadOnlyList<float[]> embeddings = floatEmbeddings is not null
            ? floatEmbeddings.Select(e => e.ToArray()).ToList()
            : [];

        var dimensions = embeddings.Count > 0
            ? embeddings[0].Length
            : (int?)null;

        return new EmbeddingResponse(
            Embeddings: embeddings,
            Base64Embeddings: null,
            Model: raw.Id,
            TotalTokens: raw.Meta.BilledUnits.InputTokens,
            Dimensions: dimensions,
            RawResponseJson: rawResponseJson,
            RawRequestJson: rawRequestJson);
    }
}
