using Cisharpai.Models;
using Cisharpai.Rag.Packing;

namespace Cisharpai.Rag.Pipeline;

/// <summary>
/// The outcome of a RAG pipeline execution.
/// </summary>
public sealed record RagResult
{
    public required string Answer { get; init; }
    public IReadOnlyList<Citation> Citations { get; init; } = [];
    public bool IsSuccess { get; init; } = true;
    public string? ErrorMessage { get; init; }

    public IReadOnlyList<ScoredChunk> RetrievedChunks { get; init; } = [];
    public IReadOnlyList<ScoredChunk> PackedChunks { get; init; } = [];
    public IReadOnlyList<DroppedChunk> DroppedChunks { get; init; } = [];
    public string? RewrittenQuery { get; init; }
    public IReadOnlyList<string>? ExpandedQueries { get; init; }

    public static RagResult Error(string errorMessage) =>
        new()
        {
            Answer = string.Empty,
            IsSuccess = false,
            ErrorMessage = errorMessage
        };
}
