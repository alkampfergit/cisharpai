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
}
