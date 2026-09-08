namespace Cisharpai.Rag.Chunking;

/// <summary>Fixed-size chunking settings. Sizes count Unicode scalar values, not tokens.</summary>
public sealed class FixedSizeChunkerOptions
{
    public int ChunkSize { get; set; } = 1024;
    public int Overlap { get; set; } = 128;

    internal FixedSizeChunkerOptions Snapshot() => ValidateAndClone(ChunkSize, Overlap);

    private static FixedSizeChunkerOptions ValidateAndClone(int chunkSize, int overlap)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(chunkSize);
        ArgumentOutOfRangeException.ThrowIfNegative(overlap);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(overlap, chunkSize);
        return new() { ChunkSize = chunkSize, Overlap = overlap };
    }
}
