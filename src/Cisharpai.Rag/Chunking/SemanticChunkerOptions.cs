namespace Cisharpai.Rag.Chunking;

/// <summary>
/// Options for <see cref="SemanticChunker"/>. The size-based backstop
/// (<see cref="MaxChunkCharacters"/>) prevents any chunk from exceeding
/// the embedding model's input limit; <see cref="MaxChunkSentences"/>
/// provides a secondary guard. Whichever limit trips first forces the cut.
/// </summary>
public sealed class SemanticChunkerOptions
{
    public SemanticThresholdStrategy Strategy { get; set; } = SemanticThresholdStrategy.Percentile;

    /// <summary>Bottom N-th percentile of similarity drops → boundary. Only used in <see cref="SemanticThresholdStrategy.Percentile"/> mode.</summary>
    public float BreakPercentile { get; set; } = 10f;

    /// <summary>Cosine similarity below this → boundary. Only used in <see cref="SemanticThresholdStrategy.Absolute"/> mode. Tune per model.</summary>
    public float AbsoluteThreshold { get; set; } = 0.5f;

    /// <summary>Hard backstop: force a cut when a chunk reaches this many characters. Must be positive.</summary>
    public int MaxChunkCharacters { get; set; } = 8000;

    /// <summary>Secondary backstop: force a cut after this many sentences regardless of similarity. Must be positive.</summary>
    public int MaxChunkSentences { get; set; } = 50;

    internal SemanticChunkerOptions Snapshot() =>
        ValidateAndClone(Strategy, BreakPercentile, AbsoluteThreshold, MaxChunkCharacters, MaxChunkSentences);

    private static SemanticChunkerOptions ValidateAndClone(
        SemanticThresholdStrategy strategy,
        float breakPercentile,
        float absoluteThreshold,
        int maxChunkCharacters,
        int maxChunkSentences)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxChunkCharacters);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxChunkSentences);

        if (strategy == SemanticThresholdStrategy.Percentile)
        {
            if (breakPercentile is <= 0f or > 100f)
                throw new ArgumentOutOfRangeException(nameof(breakPercentile), breakPercentile,
                    "BreakPercentile must be between 0 (exclusive) and 100 (inclusive).");
        }
        else
        {
            if (absoluteThreshold is < -1f or > 1f)
                throw new ArgumentOutOfRangeException(nameof(absoluteThreshold), absoluteThreshold,
                    "AbsoluteThreshold must be between -1 and 1.");
        }

        return new SemanticChunkerOptions
        {
            Strategy = strategy,
            BreakPercentile = breakPercentile,
            AbsoluteThreshold = absoluteThreshold,
            MaxChunkCharacters = maxChunkCharacters,
            MaxChunkSentences = maxChunkSentences
        };
    }
}
