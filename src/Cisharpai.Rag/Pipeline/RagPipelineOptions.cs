using Cisharpai.Models;
using Cisharpai.Rag.Packing;

namespace Cisharpai.Rag.Pipeline;

/// <summary>
/// Per-call options for the RAG pipeline.
/// </summary>
public sealed record RagPipelineOptions
{
    public IReadOnlyList<LlmMessage>? ConversationHistory { get; init; }
    public int TopK { get; init; } = 10;
    public RetrievalOptions? Retrieval { get; init; }
    public int? RerankerTopN { get; init; }
    public ContextPackingOptions? PackingOptions { get; init; }
    public string? SystemPrompt { get; init; }
    public string? Model { get; init; }
    public double? Temperature { get; init; }
    public CitationMode CitationMode { get; init; } = CitationMode.Fast;

    /// <summary>
    /// Pre-packed chunks for chat-only pipelines that skip retrieval.
    /// When set and no retriever is configured, these chunks are passed directly
    /// to the context packer and chat stages.
    /// </summary>
    public IReadOnlyList<ScoredChunk>? PrePackedChunks { get; init; }

    internal void Validate()
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(TopK);
        ValidateRetrievalTopK(Retrieval?.TopK);

        if (RerankerTopN is not null)
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(RerankerTopN.Value);
    }

    private static void ValidateRetrievalTopK(int? retrievalTopK)
    {
        if (retrievalTopK is <= 0)
            throw new ArgumentOutOfRangeException(nameof(retrievalTopK), retrievalTopK, "Retrieval.TopK must be positive when set.");
    }
}
