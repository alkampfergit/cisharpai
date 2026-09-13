namespace Cisharpai.Rag.Tokenization;

/// <summary>
/// Adapter extensions for bridging <see cref="TiktokenCounter"/> to the
/// <c>Func&lt;string, int&gt;</c> seam used by <see cref="Embeddings.BulkEmbeddingOptions.TokenEstimator"/>.
/// </summary>
public static class TokenCounterExtensions
{
    /// <summary>
    /// Returns a <c>Func&lt;string, int&gt;</c> that delegates to <see cref="TiktokenCounter.CountTokens"/>.
    /// Safe for use inside <see cref="Embeddings.BulkEmbeddingOptions.TokenEstimator"/> because
    /// the local tokenizer is synchronous and cheap. This extension is intentionally not available
    /// on <see cref="ITokenCounter"/> to prevent callers from blocking on an async/remote counter.
    /// </summary>
    public static Func<string, int> ToTokenEstimator(this TiktokenCounter counter)
    {
        ArgumentNullException.ThrowIfNull(counter);
        return counter.CountTokens;
    }

    /// <summary>
    /// Returns a <c>Func&lt;string, int, int&gt;</c> that delegates to
    /// <see cref="TiktokenCounter.GetIndexByTokenCount"/>. Use as
    /// <see cref="Chunking.RecursiveChunkerOptions.TokenSlicerFromStart"/> for O(n)
    /// token-boundary hard cuts in the recursive chunker.
    /// </summary>
    public static Func<string, int, int> ToTokenSlicerFromStart(this TiktokenCounter counter)
    {
        ArgumentNullException.ThrowIfNull(counter);
        return counter.GetIndexByTokenCount;
    }

    /// <summary>
    /// Returns a <c>Func&lt;string, int, int&gt;</c> that delegates to
    /// <see cref="TiktokenCounter.GetIndexByTokenCountFromEnd"/>. Use as
    /// <see cref="Chunking.RecursiveChunkerOptions.TokenSlicerFromEnd"/> for overlap
    /// computation in the recursive chunker's token mode.
    /// </summary>
    public static Func<string, int, int> ToTokenSlicerFromEnd(this TiktokenCounter counter)
    {
        ArgumentNullException.ThrowIfNull(counter);
        return counter.GetIndexByTokenCountFromEnd;
    }
}
