namespace Cisharpai.Rag.Packing;

/// <summary>
/// Selects and orders ranked chunks to fit within a token budget.
/// </summary>
public interface IContextPacker
{
    Task<ContextPackingResult> PackAsync(
        IReadOnlyList<ScoredChunk> rankedChunks,
        ContextPackingOptions options,
        CancellationToken cancellationToken = default);
}
