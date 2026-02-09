using System.Text.Json;
using System.Text.Json.Serialization;
using Cisharpai.Features;
using Cisharpai.Features.Embeddings;
using Cisharpai.Models;
using Cisharpai.Cohere.Models;

namespace Cisharpai.Cohere;

public sealed class CohereEmbeddingClient : IEmbeddingClient, IImageEmbeddingFeature, IMultimodalEmbeddingFeature
{
    private const string EmbedEndpoint = "embed";

    private readonly LlmHttpClient _client;
    private readonly CohereClientOptions _options;

    public IFeatureCollection Features { get; }

    public CohereEmbeddingClient(HttpClient httpClient, CohereClientOptions options)
    {
        _client = new LlmHttpClient(httpClient, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        });
        _options = options;

        var features = new FeatureCollection();
        features.Set<IImageEmbeddingFeature>(this);
        features.Set<IMultimodalEmbeddingFeature>(this);
        Features = features;
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
            var providerRequest = new CohereEmbedRequest
            {
                Model = model,
                Texts = request.Input.ToList(),
                InputType = MapInputType(request.InputType),
                EmbeddingTypes = ["float"],
                OutputDimension = request.Dimensions
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

    public async Task<EmbeddingResponse> GetImageEmbeddingAsync(
        string imagePath,
        string model,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(imagePath))
            throw new ArgumentException("Image path is required.", nameof(imagePath));

        try
        {
            var dataUri = await ImageDataUriHelper.ToDataUriAsync(imagePath, cancellationToken);

            var providerRequest = new CohereEmbedRequest
            {
                Model = model,
                Images = [dataUri],
                InputType = "image",
                EmbeddingTypes = ["float"]
            };

            return MapResponse(
                await _client.PostAsync<CohereEmbedRequest, CohereEmbedResponse>(
                    EmbedEndpoint, providerRequest, cancellationToken));
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

    public async Task<EmbeddingResponse> GetMultimodalEmbeddingsAsync(
        IReadOnlyList<MultimodalEmbeddingInput> inputs,
        string model,
        EmbeddingInputType? inputType = null,
        int? outputDimension = null,
        bool includeRawResponse = false,
        JsonElement? extraParameters = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var cohereInputs = new List<CohereEmbedInput>();

            foreach (var input in inputs)
            {
                var cohereInput = new CohereEmbedInput();
                foreach (var part in input.Content)
                {
                    switch (part)
                    {
                        case TextEmbeddingContent textPart:
                            cohereInput.Content.Add(new CohereEmbedContentPart
                            {
                                Type = "text",
                                Text = textPart.Text
                            });
                            break;

                        case ImageEmbeddingContent imagePart:
                            var dataUri = await ImageDataUriHelper.ToDataUriAsync(
                                imagePart.ImagePath, cancellationToken);
                            cohereInput.Content.Add(new CohereEmbedContentPart
                            {
                                Type = "image_url",
                                ImageUrl = new CohereImageUrl { Url = dataUri }
                            });
                            break;
                    }
                }
                cohereInputs.Add(cohereInput);
            }

            var providerRequest = new CohereEmbedRequest
            {
                Model = model,
                Inputs = cohereInputs,
                InputType = MapInputType(inputType),
                EmbeddingTypes = ["float"],
                OutputDimension = outputDimension
            };

            if (includeRawResponse)
            {
                var (raw, rawResponseJson, rawRequestJson) =
                    await _client.PostWithRawAsync<CohereEmbedRequest, CohereEmbedResponse>(
                        EmbedEndpoint, providerRequest, cancellationToken, extraParameters);
                return MapResponse(raw, rawResponseJson, rawRequestJson);
            }

            return MapResponse(
                await _client.PostAsync<CohereEmbedRequest, CohereEmbedResponse>(
                    EmbedEndpoint, providerRequest, cancellationToken, extraParameters));
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

        var totalTokens = raw.Meta.BilledUnits.InputTokens
                        + (raw.Meta.BilledUnits.ImageTokens ?? 0);

        return new EmbeddingResponse(
            Embeddings: embeddings,
            Base64Embeddings: null,
            Model: raw.Id,
            TotalTokens: totalTokens,
            Dimensions: dimensions,
            RawResponseJson: rawResponseJson,
            RawRequestJson: rawRequestJson);
    }
}
