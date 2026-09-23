using Cisharpai.Rag.Packing;

namespace Cisharpai.Rag;

/// <summary>
/// Retrieves the most relevant chunks for a natural-language query.
/// Implementations may use any retrieval strategy — dense embeddings, sparse/lexical search
/// (e.g. BM25), hybrid fusion, SQL full-text, or a hosted provider store.
/// All strategies are first-class citizens of this contract.
/// </summary>
public interface IRetriever
{
    /// <summary>
    /// Returns chunks ranked by relevance to <paramref name="query"/> using the provided <paramref name="options"/>.
    /// </summary>
    /// <param name="query">The natural-language query string.</param>
    /// <param name="options">Portable retrieval options with optional provider query extension.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Ranked chunks in descending relevance order.</returns>
    Task<IReadOnlyList<ScoredChunk>> RetrieveAsync(
        string query,
        RetrievalOptions options,
        CancellationToken cancellationToken = default);
}
