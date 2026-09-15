using System.Collections.ObjectModel;
using Cisharpai.OpenAi.Models;
using Cisharpai.Rag;
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

    public async Task<IReadOnlyList<ScoredChunk>> RetrieveAsync(
        string query,
        int topK,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var model = _options.DefaultModel
            ?? throw new InvalidOperationException(
                "DefaultModel must be set on OpenAiClientOptions to use hosted retrieval.");

        try
        {
            var request = new OpenAiResponsesApiRequest
            {
                Model = model,
                Input =
                [
                    new OpenAiChatMessage { Role = "user", Content = query }
                ],
                Tools =
                [
                    new OpenAiResponsesApiTool
                    {
                        Type = "file_search",
                        VectorStoreIds = [_vectorStoreId],
                        MaxNumResults = topK
                    }
                ],
                Include = ["file_search_call.results"]
            };

            var response = await _client.PostAsync<OpenAiResponsesApiRequest, OpenAiResponsesApiResponse>(
                "responses", request, cancellationToken: cancellationToken);

            return MapFileSearchResults(response);
        }
        catch (LlmHttpRequestException ex)
        {
            _logger?.LogWarning(ex, "File search retrieval failed for store {StoreId}: HTTP {StatusCode}",
                _vectorStoreId, ex.StatusCode);
            return [];
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger?.LogWarning(ex, "File search retrieval failed for store {StoreId}", _vectorStoreId);
            return [];
        }
    }

    private IReadOnlyList<ScoredChunk> MapFileSearchResults(OpenAiResponsesApiResponse response)
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
            .Select(MapToScoredChunk)
            .ToList();

        return results;
    }

    private static ScoredChunk MapToScoredChunk(OpenAiFileSearchResult result)
    {
        var metadata = new Dictionary<string, object?>
        {
            ["file_id"] = result.FileId,
            ["filename"] = result.Filename
        };

        if (result.Attributes is not null)
        {
            foreach (var (key, value) in result.Attributes)
                metadata[$"attr_{key}"] = value;
        }

        var chunk = new TextChunk(
            DocumentId: result.FileId,
            Index: 0,
            StartOffset: 0,
            EndOffset: result.Text.Length,
            Text: result.Text,
            Metadata: new ReadOnlyDictionary<string, object?>(metadata));

        return new ScoredChunk(chunk, result.Score);
    }
}
