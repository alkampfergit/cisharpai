namespace Cisharpai.Rag;

/// <summary>
/// Optional feature for provider-hosted vector retrieval.
/// The provider manages the vector store, chunking, and search — the caller
/// supplies only a store identifier and a query. Discovered via
/// <c>IHasFeatures.Features.Get&lt;IHostedRetrievalFeature&gt;()</c>.
/// <para>
/// This feature is a factory: call <see cref="ForStore"/> to obtain an
/// <see cref="IRetriever"/> bound to a specific vector store. The returned
/// retriever is safe for concurrent use and carries no mutable state.
/// </para>
/// </summary>
public interface IHostedRetrievalFeature
{
    /// <summary>
    /// Returns an <see cref="IRetriever"/> that queries the specified provider-hosted vector store.
    /// Each call creates an independent retriever; two concurrent retrievals against different
    /// stores do not interfere.
    /// </summary>
    /// <param name="vectorStoreId">The provider-specific identifier for the vector store to query.</param>
    /// <returns>An <see cref="IRetriever"/> bound to <paramref name="vectorStoreId"/>.</returns>
    IRetriever ForStore(string vectorStoreId);
}
