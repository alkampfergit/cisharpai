using System.Collections.ObjectModel;
using System.Text.Json;
using Cisharpai.OpenAi.Models;
using Cisharpai.Rag.Models;
using Cisharpai.Rag.Packing;
using Microsoft.Extensions.Logging;

namespace Cisharpai.Rag.OpenAi;

/// <summary>
/// An <see cref="IRetriever"/> that queries an OpenAI hosted vector store via the
/// Responses API <c>file_search</c> tool. Created by
/// <see cref="OpenAiHostedRetrievalFeature.ForStore"/>.
/// </summary>
internal sealed class OpenAiFileSearchRetriever : IRetriever
{
    private readonly LlmHttpClient _client;
    private readonly Cisharpai.OpenAi.OpenAiClientOptions _options;
    private readonly string _vectorStoreId;
    private readonly ILogger? _logger;

    internal OpenAiFileSearchRetriever(
        LlmHttpClient client,
        Cisharpai.OpenAi.OpenAiClientOptions options,
        string vectorStoreId,
        ILogger? logger)
    {
        _client = client;
        _options = options;
        _vectorStoreId = vectorStoreId;
        _logger = logger;
    }

    public Task<IReadOnlyList<ScoredChunk>> RetrieveAsync(
        string query,
        RetrievalOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(options);
        if (options.TopK is <= 0)
            throw new ArgumentOutOfRangeException(nameof(options), options.TopK, "TopK must be positive when set.");
        RetrievalFiltering.ThrowIfUnsupportedProviderQuery(options, nameof(OpenAiFileSearchRetriever));

        return RetrieveCoreAsync(query, options, cancellationToken);
    }

    private async Task<IReadOnlyList<ScoredChunk>> RetrieveCoreAsync(
        string query,
        RetrievalOptions options,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var model = _options.DefaultModel
            ?? throw new InvalidOperationException(
                "DefaultModel must be set on OpenAiClientOptions to use hosted retrieval.");

        try
        {
            var tool = new OpenAiResponsesApiTool
            {
                Type = "file_search",
                VectorStoreIds = [_vectorStoreId],
                MaxNumResults = options.TopK
            };

            if (options.MetadataEquals is { Count: > 0 } metadataEquals)
            {
                tool.Filters = BuildNativeFilters(metadataEquals);
            }

            if (options.MinScore is not null)
            {
                tool.RankingOptions = new OpenAiFileSearchRankingOptions
                {
                    ScoreThreshold = options.MinScore.Value
                };
            }

            var request = new OpenAiResponsesApiRequest
            {
                Model = model,
                Input =
                [
                    new OpenAiChatMessage { Role = "user", Content = query }
                ],
                Tools = [tool],
                Include = ["file_search_call.results"]
            };

            var response = await _client.PostAsync<OpenAiResponsesApiRequest, OpenAiResponsesApiResponse>(
                "responses", request, cancellationToken: cancellationToken).ConfigureAwait(false);

            return MapFileSearchResults(response, options);
        }
        catch (LlmHttpRequestException ex)
        {
            _logger?.LogWarning(ex, "File search retrieval failed for store {StoreId}: HTTP {StatusCode}",
                _vectorStoreId, ex.StatusCode);
            return [];
        }
        catch (JsonException ex)
        {
            _logger?.LogWarning(ex, "File search response parsing failed for store {StoreId}", _vectorStoreId);
            return [];
        }
        catch (InvalidOperationException ex)
        {
            _logger?.LogWarning(ex, "File search retrieval failed for store {StoreId}", _vectorStoreId);
            return [];
        }
    }

    private List<ScoredChunk> MapFileSearchResults(OpenAiResponsesApiResponse response, RetrievalOptions options)
    {
        var fileSearchCalls = response.Output
            .Where(o => o.Type == "file_search_call")
            .ToList();

        var failedCalls = fileSearchCalls
            .Where(o => o.Status is not null && o.Status != "completed")
            .ToList();

        if (failedCalls.Count > 0)
        {
            var failedIds = string.Join(", ", failedCalls.Select(f => f.Id ?? "unknown"));
            _logger?.LogWarning(
                "{FailedCount} of {TotalCount} file search call(s) failed in store {StoreId} (ids: {FailedIds})",
                failedCalls.Count, fileSearchCalls.Count, _vectorStoreId, failedIds);
        }

        var results = fileSearchCalls
            .Where(o => o.Status == "completed" && o.Results is not null)
            .SelectMany(o => o.Results!)
            .ToList();

        var perFileOrdinals = new Dictionary<string, int>();
        var scored = new List<ScoredChunk>(results.Count);
        foreach (var r in results)
        {
            perFileOrdinals.TryGetValue(r.FileId, out var ordinal);
            scored.Add(MapToScoredChunk(r, ordinal));
            perFileOrdinals[r.FileId] = ordinal + 1;
        }

        return RetrievalFiltering.ApplyPostFilters(scored, options, defaultTopK: scored.Count).ToList();
    }

    private static ScoredChunk MapToScoredChunk(OpenAiFileSearchResult result, int perFileOrdinal)
    {
        var metadata = new Dictionary<string, object?>
        {
            ["file_id"] = result.FileId,
            ["filename"] = result.Filename
        };

        if (result.Attributes is not null)
        {
            foreach (var (key, value) in result.Attributes)
                metadata[key] = value;
        }

        var chunk = new TextChunk(
            DocumentId: result.FileId,
            Index: perFileOrdinal,
            StartOffset: 0,
            EndOffset: result.Text.Length,
            Text: result.Text,
            Metadata: new ReadOnlyDictionary<string, object?>(metadata));

        return new ScoredChunk(chunk, result.Score);
    }

    private static OpenAiFileSearchFilter BuildNativeFilters(IReadOnlyDictionary<string, string> metadataEquals)
    {
        var eqFilters = metadataEquals
            .Select(kv => new OpenAiFileSearchFilter { Type = "eq", Key = kv.Key, Value = kv.Value })
            .ToList();

        if (eqFilters.Count == 1)
            return eqFilters[0];

        return new OpenAiFileSearchFilter { Type = "and", SubFilters = eqFilters };
    }
}
