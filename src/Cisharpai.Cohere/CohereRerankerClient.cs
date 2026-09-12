using System.Text.Json;
using System.Text.Json.Serialization;
using Cisharpai.Features;
using Cisharpai.Models;
using Cisharpai.Cohere.Models;
using Cisharpai;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Logging;

namespace Cisharpai.Cohere;

public sealed class CohereRerankerClient : IRerankerClient
{
    private const string RerankEndpoint = "rerank";

    private readonly LlmHttpClient _client;
    private readonly CohereClientOptions _options;

    public IFeatureCollection Features { get; }

    public CohereRerankerClient(HttpClient httpClient, CohereClientOptions options, ILoggerFactory? loggerFactory = null)
    {
        _client = new LlmHttpClient(httpClient, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        }, loggerFactory?.CreateLogger<LlmHttpClient>());
        _options = options;

        Features = new FeatureCollection();
    }

    public static CohereRerankerClient Create(
        IHttpMessageHandlerFactory handlerFactory,
        CohereClientOptions options,
        string handlerName = "cisharpai",
        ILoggerFactory? loggerFactory = null)
    {
        var http = new HttpClient(new CohereAuthenticationHandler(options) { InnerHandler = handlerFactory.CreateHandler(handlerName) })
        {
            BaseAddress = new Uri(options.BaseUrl),
            Timeout = TimeSpan.FromMinutes(2)
        };
        return new CohereRerankerClient(http, options, loggerFactory);
    }

    public async Task<RerankResponse> RerankAsync(
        RerankRequest request,
        CancellationToken cancellationToken = default)
    {
        var model = request.Model
            ?? _options.DefaultModel
            ?? throw new InvalidOperationException(
                "Model must be specified either in the request or via DefaultModel in options.");

        try
        {
            var providerRequest = new CohereRerankRequest
            {
                Model = model,
                Query = request.Query,
                Documents = request.Documents.ToList(),
                TopN = request.TopN,
                MaxTokensPerDoc = request.MaxTokensPerDocument
            };

            if (request.IncludeRawResponse)
            {
                var (raw, rawResponseJson, rawRequestJson) = await _client.PostWithRawAsync<CohereRerankRequest, CohereRerankResponse>(
                    RerankEndpoint, providerRequest, request.ExtraParameters, cancellationToken);
                return MapResponse(raw, model, rawResponseJson, rawRequestJson);
            }

            return MapResponse(
                await _client.PostAsync<CohereRerankRequest, CohereRerankResponse>(
                    RerankEndpoint, providerRequest, request.ExtraParameters, cancellationToken),
                model);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (LlmHttpRequestException ex)
        {
            return RerankResponse.Error(ex.Message, ex.ResponseBody);
        }
        catch (HttpRequestException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return RerankResponse.Error(ex.Message);
        }
    }

    private static RerankResponse MapResponse(
        CohereRerankResponse raw,
        string model,
        string? rawResponseJson = null,
        string? rawRequestJson = null)
    {
        // Cohere's rerank response carries a request id in "id", not a model name, so the
        // resolved request model is what we surface to callers.
        var results = raw.Results
            .Select(r => new RerankResult(r.Index, r.RelevanceScore))
            .ToList();

        return new RerankResponse(
            Results: results,
            Model: model,
            SearchUnits: raw.Meta.BilledUnits.SearchUnits,
            InputTokens: raw.Meta.BilledUnits.InputTokens,
            RawResponseJson: rawResponseJson,
            RawRequestJson: rawRequestJson);
    }
}
