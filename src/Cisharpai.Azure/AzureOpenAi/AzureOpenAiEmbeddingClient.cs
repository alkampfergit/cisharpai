using Cisharpai.Features;
using Cisharpai.Helpers;
using Cisharpai.Models;
using Cisharpai.Azure.AzureOpenAi.Models;
using Microsoft.Extensions.Logging;

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
        AzureOpenAiClientOptions options,
        ILoggerFactory? loggerFactory = null)
    {
        _client = new LlmHttpClient(httpClient, logger: loggerFactory?.CreateLogger<LlmHttpClient>());
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
        var orderedEmbeddings = raw.Data.OrderBy(d => d.Index)
            .Select(d => d.Embedding)
            .ToList();

        return EmbeddingHelper.MapEmbeddingResponse(
            orderedEmbeddings, encodingFormat, raw.Model, raw.Usage.TotalTokens,
            rawResponseJson, rawRequestJson);
    }
}
