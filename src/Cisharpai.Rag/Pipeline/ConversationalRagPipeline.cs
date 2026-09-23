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

    private readonly record struct PipelineStageResults(
        IReadOnlyList<ScoredChunk> Retrieved,
        IReadOnlyList<ScoredChunk> Packed,
        IReadOnlyList<DroppedChunk> Dropped,
        string? RewrittenQuery,
        IReadOnlyList<string> SearchQueries);

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
        options.Validate();

        var (searchQueries, rewrittenQuery) = await TransformQueryAsync(query, options, cancellationToken)
            .ConfigureAwait(false);

        var retrieved = await RetrieveAsync(searchQueries, options, cancellationToken)
            .ConfigureAwait(false);

        var reranked = await RerankAsync(retrieved, searchQueries[0], options, cancellationToken)
            .ConfigureAwait(false);

        var (packed, dropped) = await PackAsync(reranked, options, cancellationToken)
            .ConfigureAwait(false);

        var stages = new PipelineStageResults(retrieved, packed, dropped, rewrittenQuery, searchQueries);

        if (_chatClient is null)
        {
            return BuildRagResult(string.Empty, [], stages);
        }

        var (answer, citations, errorMessage) = await ChatAsync(query, packed, options, cancellationToken)
            .ConfigureAwait(false);

        if (answer is null)
        {
            return BuildRagResult(
                string.Empty, [], stages,
                isSuccess: false, errorMessage: errorMessage ?? "Chat completion failed.");
        }

        return BuildRagResult(answer, citations, stages);
    }

    public async IAsyncEnumerable<RagStreamingChunk> AskStreamingAsync(
        string query,
        RagPipelineOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);
        options ??= new RagPipelineOptions();
        options.Validate();

        var (searchQueries, rewrittenQuery) = await TransformQueryAsync(query, options, cancellationToken)
            .ConfigureAwait(false);

        var retrieved = await RetrieveAsync(searchQueries, options, cancellationToken)
            .ConfigureAwait(false);

        var reranked = await RerankAsync(retrieved, searchQueries[0], options, cancellationToken)
            .ConfigureAwait(false);

        var (packed, dropped) = await PackAsync(reranked, options, cancellationToken)
            .ConfigureAwait(false);

        var stages = new PipelineStageResults(retrieved, packed, dropped, rewrittenQuery, searchQueries);

        if (_chatClient is null)
        {
            yield return new RagStreamingChunk
            {
                FinishReason = "stop",
                FinalResult = BuildRagResult(string.Empty, [], stages)
            };
            yield break;
        }

        var streamingFeature = _chatClient.Features.Get<IStreamingChatFeature>();
        if (streamingFeature is null)
        {
            yield return await CreateNonStreamingFallbackChunkAsync(
                query, options, stages, cancellationToken)
                .ConfigureAwait(false);
            yield break;
        }

        var usesFallbackMarkers = packed.Count > 0
                                  && _chatClient.Features.Get<IGroundedChatFeature>() is null;
        var documents = usesFallbackMarkers ? ChunksToDocuments(packed) : [];
        var streamRequest = BuildStreamingChatRequest(query, packed, options);
        var fullContent = new StringBuilder();

        await foreach (var chunk in streamingFeature.GetChatCompletionStreamAsync(streamRequest, cancellationToken)
                           .ConfigureAwait(false))
        {
            if (string.IsNullOrEmpty(chunk.Content) && chunk.FinishReason is null)
                continue;

            fullContent.Append(chunk.Content);

            if (chunk.FinishReason is not null)
            {
                var rawAnswer = fullContent.ToString();
                string finalAnswer;
                IReadOnlyList<Citation> citations;

                if (usesFallbackMarkers)
                    (finalAnswer, citations) = GroundedChatFallbackHelper
                        .ParseAndStripMarkers(rawAnswer, documents);
                else
                    (finalAnswer, citations) = (rawAnswer, []);

                yield return new RagStreamingChunk
                {
                    ContentDelta = chunk.Content,
                    FinishReason = chunk.FinishReason,
                    FinalResult = BuildRagResult(finalAnswer, citations, stages)
                };
            }
            else
            {
                yield return new RagStreamingChunk { ContentDelta = chunk.Content };
            }
        }
    }

    private async Task<RagStreamingChunk> CreateNonStreamingFallbackChunkAsync(
        string query,
        RagPipelineOptions options,
        PipelineStageResults stages,
        CancellationToken cancellationToken)
    {
        var (answer, citations, errorMessage) = await ChatAsync(query, stages.Packed, options, cancellationToken)
            .ConfigureAwait(false);

        return new RagStreamingChunk
        {
            ContentDelta = answer ?? string.Empty,
            FinishReason = "stop",
            FinalResult = BuildRagResult(
                answer ?? string.Empty, citations, stages,
                isSuccess: answer is not null,
                errorMessage: answer is null ? (errorMessage ?? "Chat completion failed.") : null)
        };
    }

    private static RagResult BuildRagResult(
        string answer,
        IReadOnlyList<Citation> citations,
        PipelineStageResults stages,
        bool isSuccess = true,
        string? errorMessage = null)
    {
        return new RagResult
        {
            Answer = answer,
            Citations = citations,
            IsSuccess = isSuccess,
            ErrorMessage = errorMessage,
            RetrievedChunks = stages.Retrieved,
            PackedChunks = stages.Packed,
            DroppedChunks = stages.Dropped,
            RewrittenQuery = stages.RewrittenQuery,
            ExpandedQueries = stages.SearchQueries.Count > 1 ? stages.SearchQueries : null
        };
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
                if (results.Count > 0)
                    expanded.AddRange(results);
                else
                    expanded.Add(q);
            }
            queries = expanded.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        return (queries, rewrittenQuery);
    }

    private async Task<IReadOnlyList<ScoredChunk>> RetrieveAsync(
        IReadOnlyList<string> searchQueries,
        RagPipelineOptions options,
        CancellationToken cancellationToken)
    {
        if (_retrievers.Count == 0)
            return options.PrePackedChunks ?? [];

        var allLists = new List<IReadOnlyList<ScoredChunk>>();

        foreach (var searchQuery in searchQueries)
        {
            foreach (var retriever in _retrievers)
            {
                RetrievalOptions retrievalOptions;
                if (options.Retrieval is null)
                {
                    retrievalOptions = new RetrievalOptions { TopK = options.TopK };
                }
                else if (options.Retrieval.TopK is null)
                {
                    retrievalOptions = options.Retrieval with { TopK = options.TopK };
                }
                else
                {
                    retrievalOptions = options.Retrieval;
                }

                var results = await retriever.RetrieveAsync(searchQuery, retrievalOptions, cancellationToken)
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

    private async Task<(string? Answer, IReadOnlyList<Citation> Citations, string? ErrorMessage)> ChatAsync(
        string query,
        IReadOnlyList<ScoredChunk> packedChunks,
        RagPipelineOptions options,
        CancellationToken cancellationToken)
    {
        var documents = ChunksToDocuments(packedChunks);
        var groundedFeature = _chatClient!.Features.Get<IGroundedChatFeature>();

        if (groundedFeature is not null && documents.Count > 0)
        {
            var request = BuildPlainChatRequest(query, options);
            var groundedOptions = new GroundedChatOptions(documents, options.CitationMode);

            var response = await groundedFeature
                .GetGroundedChatCompletionAsync(request, groundedOptions, cancellationToken)
                .ConfigureAwait(false);

            return response.IsSuccess
                ? (response.Content, response.Citations, null)
                : (null, [], response.ErrorMessage);
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
                return (null, [], response.ErrorMessage);

            var (cleanContent, citations) = GroundedChatFallbackHelper
                .ParseAndStripMarkers(response.Content, documents);

            return (cleanContent, citations, null);
        }

        return await PlainChatAsync(query, options, cancellationToken).ConfigureAwait(false);
    }

    private async Task<(string? Answer, IReadOnlyList<Citation> Citations, string? ErrorMessage)> PlainChatAsync(
        string query,
        RagPipelineOptions options,
        CancellationToken cancellationToken)
    {
        var request = BuildPlainChatRequest(query, options);
        var response = await _chatClient!.GetChatCompletionAsync(request, cancellationToken)
            .ConfigureAwait(false);

        return response.IsSuccess
            ? (response.Content, [], null)
            : (null, [], response.ErrorMessage);
    }

    private static ChatCompletionRequest BuildPlainChatRequest(
        string query,
        RagPipelineOptions options)
    {
        var messages = BuildPlainMessages(query, options);
        return new ChatCompletionRequest(
            Messages: messages,
            Model: options.Model,
            Temperature: options.Temperature);
    }

    private ChatCompletionRequest BuildStreamingChatRequest(
        string query,
        IReadOnlyList<ScoredChunk> packedChunks,
        RagPipelineOptions options)
    {
        var hasGroundedChat = _chatClient!.Features.Get<IGroundedChatFeature>() is not null;

        if (hasGroundedChat || packedChunks.Count == 0)
            return BuildPlainChatRequest(query, options);

        var messages = BuildMessagesWithContext(query, packedChunks, options);
        return new ChatCompletionRequest(
            Messages: messages,
            Model: options.Model,
            Temperature: options.Temperature);
    }

    private static List<LlmMessage> BuildPlainMessages(
        string query,
        RagPipelineOptions options)
    {
        var messages = new List<LlmMessage>();
        messages.Add(new LlmMessage(LlmRole.System, options.SystemPrompt ?? DefaultSystemPrompt));

        if (options.ConversationHistory is { Count: > 0 })
            messages.AddRange(options.ConversationHistory);

        messages.Add(new LlmMessage(LlmRole.User, query));
        return messages;
    }

    private static List<LlmMessage> BuildMessagesWithContext(
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

    private static List<DocumentChunk> ChunksToDocuments(IReadOnlyList<ScoredChunk> chunks)
    {
        return chunks
            .Select(c =>
            {
                var id = $"{c.Chunk.DocumentId}_{c.Chunk.Index}";
                var source = c.Chunk.Metadata.TryGetValue("source", out var src) && src is string s
                    ? s
                    : c.Chunk.DocumentId;
                var title = c.Chunk.Metadata.TryGetValue("title", out var t) && t is string ts
                    ? ts
                    : null;

                return new DocumentChunk(Id: id, Text: c.Chunk.Text) { Source = source, Title = title };
            })
            .ToList();
    }
}
