namespace Cisharpai.Rag.Chunking;

/// <summary>
/// Options for <see cref="RecursiveChunker"/>. The chunker tries separators in order,
/// falling through to the next when a piece still exceeds <see cref="MaxChunkSize"/>.
/// An empty string as the final separator triggers a hard character- or token-level cut,
/// guaranteeing that every emitted chunk fits the budget.
/// <para>
/// <b>Character mode (default):</b> sizes are UTF-16 code unit counts (<c>string.Length</c>).
/// No additional dependencies required.
/// </para>
/// <para>
/// <b>Token mode:</b> set <see cref="TokenCounter"/> to measure in real tokens. For the
/// hard-cut terminal case, provide <see cref="TokenSlicerFromStart"/> and
/// <see cref="TokenSlicerFromEnd"/> via <c>TiktokenCounter.ToTokenSlicerFromStart()</c> /
/// <c>TiktokenCounter.ToTokenSlicerFromEnd()</c> (in the <c>Cisharpai.Rag.Tokenizers</c>
/// package). Without slicers, the chunker throws if it encounters a single word that
/// exceeds the budget — it will not silently fall back to a counting loop.
/// </para>
/// </summary>
public sealed class RecursiveChunkerOptions
{
    /// <summary>Default separator ladder: paragraph, line, sentence, word, hard character cut.</summary>
    public static readonly IReadOnlyList<string> DefaultSeparators =
        ["\n\n", "\n", ". ", " ", ""];

    /// <summary>Maximum chunk size in characters (default) or tokens when <see cref="TokenCounter"/> is set. Must be positive.</summary>
    public int MaxChunkSize { get; set; } = 1024;

    /// <summary>
    /// Units of overlap between consecutive chunks, in the same unit as <see cref="MaxChunkSize"/>.
    /// Must be non-negative and less than <see cref="MaxChunkSize"/>. Set to 0 for no overlap.
    /// </summary>
    public int ChunkOverlap { get; set; } = 128;

    /// <summary>
    /// Ordered list of separators to try. The chunker tries the first separator, falls back to
    /// the next for pieces that still exceed the budget, and so on. An empty string as the final
    /// entry triggers a hard character-level (or token-level) cut.
    /// <para>
    /// If the list does not end with an empty string, a single atomic unit (e.g. a "word" with
    /// no spaces) that exceeds the budget will be emitted oversized rather than split.
    /// </para>
    /// </summary>
    public IReadOnlyList<string>? Separators { get; set; }

    /// <summary>
    /// When set, sizes are measured in tokens via this counter instead of characters.
    /// Local counters (<c>TiktokenCounter</c>) complete synchronously.
    /// </summary>
    public ITokenCounter? TokenCounter { get; set; }

    /// <summary>
    /// Given (text, maxTokens), returns the UTF-16 index immediately following the last character
    /// that fits within <paramref name="maxTokens"/> tokens from the start. Required for the hard-cut
    /// terminal case in token mode. Use <c>TiktokenCounter.ToTokenSlicerFromStart()</c>.
    /// </summary>
    public Func<string, int, int>? TokenSlicerFromStart { get; set; }

    /// <summary>
    /// Given (text, maxTokens), returns the UTF-16 index where the last <paramref name="maxTokens"/>
    /// tokens begin (counting from the end). Used for computing overlap in token mode.
    /// Use <c>TiktokenCounter.ToTokenSlicerFromEnd()</c>.
    /// </summary>
    public Func<string, int, int>? TokenSlicerFromEnd { get; set; }

    internal RecursiveChunkerOptions Snapshot() => ValidateAndClone(
        MaxChunkSize, ChunkOverlap, Separators, TokenCounter,
        TokenSlicerFromStart, TokenSlicerFromEnd);

    private static RecursiveChunkerOptions ValidateAndClone(
        int maxChunkSize,
        int chunkOverlap,
        IReadOnlyList<string>? separators,
        ITokenCounter? tokenCounter,
        Func<string, int, int>? tokenSlicerFromStart,
        Func<string, int, int>? tokenSlicerFromEnd)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxChunkSize);
        ArgumentOutOfRangeException.ThrowIfNegative(chunkOverlap);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(chunkOverlap, maxChunkSize);

        separators ??= DefaultSeparators;
        if (separators.Count == 0)
            throw new ArgumentException("Separators must contain at least one entry.", nameof(separators));

        separators = separators.ToList().AsReadOnly();

        return new RecursiveChunkerOptions
        {
            MaxChunkSize = maxChunkSize,
            ChunkOverlap = chunkOverlap,
            Separators = separators,
            TokenCounter = tokenCounter,
            TokenSlicerFromStart = tokenSlicerFromStart,
            TokenSlicerFromEnd = tokenSlicerFromEnd
        };
    }
}
