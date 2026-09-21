using System.Runtime.CompilerServices;
using System.Text;
using Cisharpai.Features.Chat;
using Cisharpai.Helpers;
using Cisharpai.Models;
using Cisharpai.Rag.Packing;
using Cisharpai.Rag.QueryTransformation;

namespace Cisharpai.Rag.Pipeline;

/// <summary>
/// Default RAG pipeline: rewrite → transform → retrieve → fuse → rerank → pack → grounded chat.
/// </summary>
internal sealed class ConversationalRagPipeline : IRagPipeline
{
    private static readonly ContextPackingOptions DefaultPackingOptions = new()
    {
        TokenBudget = 4096,
        ReservedTokens = 0
    };

    private const string DefaultSystemPrompt =
        "You are a helpful assistant. Answer the user's question using only the provided context. " +
        "If the context does not contain enough information to answer, say so.";

    private readonly IReadOnlyList<IRetriever> _retrievers;
    private readonly IReadOnlyList<IQueryTransformer> _queryTransformers;
    private readonly ConversationQueryRewriter? _conversationRewriter;
    private readonly IRerankerClient? _reranker;
    private readonly IContextPacker? _contextPacker;
    private readonly IChatCompletionClient? _chatClient;
    private readonly double _rankFusionK;

    internal ConversationalRagPipeline(
        IReadOnlyList<IRetriever> retrievers,
        IReadOnlyList<IQueryTransformer> queryTransformers,
        ConversationQueryRewriter? conversationRewriter,
        IRerankerClient? reranker,
        IContextPacker? contextPacker,
        IChatCompletionClient? chatClient,
        double rankFusionK)
    {
        _retrievers = retrievers;
        _queryTransformers = queryTransformers;
        _conversationRewriter = conversationRewriter;
        _reranker = reranker;
        _contextPacker = contextPacker;
        _chatClient = chatClient;
        _rankFusionK = rankFusionK;
    }

    public async Task<RagResult> AskAsync(
        string query,
        RagPipelineOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);
        options ??= new RagPipelineOptions();

        var (searchQueries, rewrittenQuery) = await TransformQueryAsync(query, options, cancellationToken)
            .ConfigureAwait(false);

        var retrieved = await RetrieveAsync(searchQueries, options.TopK, cancellationToken)
            .ConfigureAwait(false);

        var reranked = await RerankAsync(retrieved, searchQueries[0], options, cancellationToken)
            .ConfigureAwait(false);

        var (packed, dropped) = await PackAsync(reranked, options, cancellationToken)
            .ConfigureAwait(false);

        if (_chatClient is null)
        {
            return new RagResult
            {
                Answer = string.Empty,
                RetrievedChunks = retrieved,
                PackedChunks = packed,
                DroppedChunks = dropped,
                RewrittenQuery = rewrittenQuery,
                ExpandedQueries = searchQueries.Count > 1 ? searchQueries : null
            };
        }

        var (answer, citations) = await ChatAsync(query, packed, options, cancellationToken)
            .ConfigureAwait(false);

        if (answer is null)
        {
            return RagResult.Error("Chat completion failed.") with
            {
                RetrievedChunks = retrieved,
                PackedChunks = packed,
                DroppedChunks = dropped,
                RewrittenQuery = rewrittenQuery,
                ExpandedQueries = searchQueries.Count > 1 ? searchQueries : null
            };
        }

        return new RagResult
        {
            Answer = answer,
            Citations = citations,
            RetrievedChunks = retrieved,
            PackedChunks = packed,
            DroppedChunks = dropped,
            RewrittenQuery = rewrittenQuery,
            ExpandedQueries = searchQueries.Count > 1 ? searchQueries : null
        };
    }

    public async IAsyncEnumerable<RagStreamingChunk> AskStreamingAsync(
        string query,
        RagPipelineOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);
        options ??= new RagPipelineOptions();

        var (searchQueries, rewrittenQuery) = await TransformQueryAsync(query, options, cancellationToken)
            .ConfigureAwait(false);

        var retrieved = await RetrieveAsync(searchQueries, options.TopK, cancellationToken)
            .ConfigureAwait(false);

        var reranked = await RerankAsync(retrieved, searchQueries[0], options, cancellationToken)
            .ConfigureAwait(false);

        var (packed, dropped) = await PackAsync(reranked, options, cancellationToken)
            .ConfigureAwait(false);

        if (_chatClient is null)
        {
            yield return new RagStreamingChunk
            {
                FinishReason = "stop",
                FinalResult = new RagResult
                {
                    Answer = string.Empty,
                    RetrievedChunks = retrieved,
                    PackedChunks = packed,
                    DroppedChunks = dropped,
                    RewrittenQuery = rewrittenQuery,
                    ExpandedQueries = searchQueries.Count > 1 ? searchQueries : null
                }
            };
            yield break;
        }

        var streamingFeature = _chatClient.Features.Get<IStreamingChatFeature>();
        if (streamingFeature is null)
        {
            var (answer, citations) = await ChatAsync(query, packed, options, cancellationToken)
                .ConfigureAwait(false);

            yield return new RagStreamingChunk
            {
                ContentDelta = answer ?? string.Empty,
                FinishReason = "stop",
                FinalResult = new RagResult
                {
                    Answer = answer ?? string.Empty,
                    Citations = citations,
                    IsSuccess = answer is not null,
                    ErrorMessage = answer is null ? "Chat completion failed." : null,
                    RetrievedChunks = retrieved,
                    PackedChunks = packed,
                    DroppedChunks = dropped,
                    RewrittenQuery = rewrittenQuery,
                    ExpandedQueries = searchQueries.Count > 1 ? searchQueries : null
                }
            };
            yield break;
        }

        var streamRequest = BuildChatRequest(query, packed, options);
        var fullContent = new StringBuilder();

        await foreach (var chunk in streamingFeature.GetChatCompletionStreamAsync(streamRequest, cancellationToken)
                           .ConfigureAwait(false))
        {
            fullContent.Append(chunk.Content);

            if (chunk.FinishReason is not null)
            {
                yield return new RagStreamingChunk
                {
                    ContentDelta = chunk.Content,
                    FinishReason = chunk.FinishReason,
                    FinalResult = new RagResult
                    {
                        Answer = fullContent.ToString(),
                        RetrievedChunks = retrieved,
                        PackedChunks = packed,
                        DroppedChunks = dropped,
                        RewrittenQuery = rewrittenQuery,
                        ExpandedQueries = searchQueries.Count > 1 ? searchQueries : null
                    }
                };
            }
            else
            {
                yield return new RagStreamingChunk { ContentDelta = chunk.Content };
            }
        }
    }

    private async Task<(IReadOnlyList<string> SearchQueries, string? RewrittenQuery)> TransformQueryAsync(
        string query,
        RagPipelineOptions options,
        CancellationToken cancellationToken)
    {
        var currentQuery = query;
        string? rewrittenQuery = null;

        if (_conversationRewriter is not null
            && options.ConversationHistory is { Count: > 0 })
        {
            currentQuery = await _conversationRewriter
                .RewriteAsync(query, options.ConversationHistory, cancellationToken)
                .ConfigureAwait(false);
            rewrittenQuery = currentQuery;
        }

        if (_queryTransformers.Count == 0)
            return ([currentQuery], rewrittenQuery);

        var queries = new List<string> { currentQuery };
        foreach (var transformer in _queryTransformers)
        {
            var expanded = new List<string>();
            foreach (var q in queries)
            {
                var results = await transformer.TransformAsync(q, cancellationToken)
                    .ConfigureAwait(false);
                expanded.AddRange(results);
            }
            queries = expanded.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        return (queries, rewrittenQuery);
    }

    private async Task<IReadOnlyList<ScoredChunk>> RetrieveAsync(
        IReadOnlyList<string> searchQueries,
        int topK,
        CancellationToken cancellationToken)
    {
        if (_retrievers.Count == 0)
            return [];

        var allLists = new List<IReadOnlyList<ScoredChunk>>();

        foreach (var searchQuery in searchQueries)
        {
            foreach (var retriever in _retrievers)
            {
                var results = await retriever.RetrieveAsync(searchQuery, topK, cancellationToken)
                    .ConfigureAwait(false);
                allLists.Add(results);
            }
        }

        if (allLists.Count == 1)
            return allLists[0];

        return RankFusion.ReciprocalRank(allLists, _rankFusionK);
    }

    private async Task<IReadOnlyList<ScoredChunk>> RerankAsync(
        IReadOnlyList<ScoredChunk> chunks,
        string query,
        RagPipelineOptions options,
        CancellationToken cancellationToken)
    {
        if (_reranker is null || chunks.Count == 0)
            return chunks;

        var documents = chunks.Select(c => c.Chunk.Text).ToList();
        var request = new RerankRequest(
            Query: query,
            Documents: documents,
            TopN: options.RerankerTopN);

        var response = await _reranker.RerankAsync(request, cancellationToken)
            .ConfigureAwait(false);

        if (!response.IsSuccess)
            return chunks;

        return response.Results
            .Where(r => r.Index >= 0 && r.Index < chunks.Count)
            .Select(r => new ScoredChunk(chunks[r.Index].Chunk, r.RelevanceScore))
            .ToList();
    }

    private async Task<(IReadOnlyList<ScoredChunk> Packed, IReadOnlyList<DroppedChunk> Dropped)> PackAsync(
        IReadOnlyList<ScoredChunk> chunks,
        RagPipelineOptions options,
        CancellationToken cancellationToken)
    {
        if (_contextPacker is null || chunks.Count == 0)
            return (chunks, []);

        var packingOptions = options.PackingOptions ?? DefaultPackingOptions;
        var result = await _contextPacker.PackAsync(chunks, packingOptions, cancellationToken)
            .ConfigureAwait(false);

        return (result.Selected, result.Dropped);
    }

    private async Task<(string? Answer, IReadOnlyList<Citation> Citations)> ChatAsync(
        string query,
        IReadOnlyList<ScoredChunk> packedChunks,
        RagPipelineOptions options,
        CancellationToken cancellationToken)
    {
        var documents = ChunksToDocuments(packedChunks);
        var groundedFeature = _chatClient!.Features.Get<IGroundedChatFeature>();

        if (groundedFeature is not null && documents.Count > 0)
        {
            var request = BuildChatRequest(query, packedChunks, options);
            var groundedOptions = new GroundedChatOptions(documents, options.CitationMode);

            var response = await groundedFeature
                .GetGroundedChatCompletionAsync(request, groundedOptions, cancellationToken)
                .ConfigureAwait(false);

            return response.IsSuccess
                ? (response.Content, response.Citations)
                : (null, []);
        }

        if (documents.Count > 0)
        {
            var messages = BuildMessagesWithContext(query, packedChunks, options);
            var request = new ChatCompletionRequest(
                Messages: messages,
                Model: options.Model,
                Temperature: options.Temperature);

            var response = await _chatClient.GetChatCompletionAsync(request, cancellationToken)
                .ConfigureAwait(false);

            if (!response.IsSuccess)
                return (null, []);

            var (cleanContent, citations) = GroundedChatFallbackHelper
                .ParseAndStripMarkers(response.Content, documents);

            return (cleanContent, citations);
        }

        {
            var request = BuildChatRequest(query, packedChunks, options);
            var response = await _chatClient.GetChatCompletionAsync(request, cancellationToken)
                .ConfigureAwait(false);

            return response.IsSuccess
                ? (response.Content, [])
                : (null, []);
        }
    }

    private ChatCompletionRequest BuildChatRequest(
        string query,
        IReadOnlyList<ScoredChunk> packedChunks,
        RagPipelineOptions options)
    {
        var messages = BuildMessagesWithContext(query, packedChunks, options);
        return new ChatCompletionRequest(
            Messages: messages,
            Model: options.Model,
            Temperature: options.Temperature);
    }

    private IReadOnlyList<LlmMessage> BuildMessagesWithContext(
        string query,
        IReadOnlyList<ScoredChunk> packedChunks,
        RagPipelineOptions options)
    {
        var messages = new List<LlmMessage>();

        var systemPrompt = options.SystemPrompt ?? DefaultSystemPrompt;

        if (packedChunks.Count > 0)
        {
            var documents = ChunksToDocuments(packedChunks);
            var groundedMessages = GroundedChatFallbackHelper.BuildGroundingMessages(
                [new LlmMessage(LlmRole.System, systemPrompt)],
                documents);
            messages.AddRange(groundedMessages);
        }
        else
        {
            messages.Add(new LlmMessage(LlmRole.System, systemPrompt));
        }

        if (options.ConversationHistory is { Count: > 0 })
        {
            messages.AddRange(options.ConversationHistory);
        }

        messages.Add(new LlmMessage(LlmRole.User, query));

        return messages;
    }

    private static IReadOnlyList<DocumentChunk> ChunksToDocuments(IReadOnlyList<ScoredChunk> chunks)
    {
        return chunks
            .Select(c => new DocumentChunk(
                Id: $"{c.Chunk.DocumentId}_{c.Chunk.Index}",
                Text: c.Chunk.Text))
            .ToList();
    }
}
