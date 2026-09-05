namespace Cisharpai.Rag.Chunking;

/// <summary>Fixed-size chunking settings. Sizes count Unicode scalar values, not tokens.</summary>
public sealed class FixedSizeChunkerOptions
{
    public int ChunkSize { get; set; } = 1024;
    public int Overlap { get; set; } = 128;

    internal FixedSizeChunkerOptions Snapshot()
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(ChunkSize);
        if (Overlap < 0 || Overlap >= ChunkSize)
            throw new ArgumentOutOfRangeException(nameof(Overlap), "Overlap must be nonnegative and smaller than ChunkSize.");
        return new() { ChunkSize = ChunkSize, Overlap = Overlap };
    }
}
