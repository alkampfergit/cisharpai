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
    public int? RerankerTopN { get; init; }
    public ContextPackingOptions? PackingOptions { get; init; }
    public string? SystemPrompt { get; init; }
    public string? Model { get; init; }
    public double? Temperature { get; init; }
    public CitationMode CitationMode { get; init; } = CitationMode.Fast;
}
