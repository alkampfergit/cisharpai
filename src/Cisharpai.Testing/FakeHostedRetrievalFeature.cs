using Cisharpai.Rag;

namespace Cisharpai.Testing;

/// <summary>
/// A fake implementation of <see cref="IHostedRetrievalFeature"/> for unit testing.
/// Each call to <see cref="ForStore"/> returns a <see cref="FakeRetriever"/> that can be
/// pre-configured with canned responses. Unknown store IDs return an empty-response retriever.
/// </summary>
public sealed class FakeHostedRetrievalFeature : IHostedRetrievalFeature
{
    private readonly Dictionary<string, FakeRetriever> _stores = new(StringComparer.Ordinal);

    /// <summary>
    /// Registers a <see cref="FakeRetriever"/> for a specific vector store ID.
    /// </summary>
    public FakeRetriever AddStore(string vectorStoreId)
    {
        var retriever = new FakeRetriever();
        _stores[vectorStoreId] = retriever;
        return retriever;
    }

    /// <summary>
    /// Registers an existing <see cref="FakeRetriever"/> for a specific vector store ID.
    /// </summary>
    public void AddStore(string vectorStoreId, FakeRetriever retriever)
    {
        _stores[vectorStoreId] = retriever;
    }

    /// <summary>
    /// Returns the <see cref="FakeRetriever"/> registered for <paramref name="vectorStoreId"/>,
    /// or a new empty-default retriever if none was registered.
    /// </summary>
    public IRetriever ForStore(string vectorStoreId)
    {
        if (_stores.TryGetValue(vectorStoreId, out var retriever))
            return retriever;

        var empty = new FakeRetriever { DefaultResponse = [] };
        _stores[vectorStoreId] = empty;
        return empty;
    }

    /// <summary>
    /// Gets the <see cref="FakeRetriever"/> for a given store ID for assertion purposes.
    /// Returns null if no retriever was created for that store.
    /// </summary>
    public FakeRetriever? GetRetriever(string vectorStoreId) =>
        _stores.GetValueOrDefault(vectorStoreId);

    /// <summary>
    /// All store IDs that have been accessed (via <see cref="ForStore"/> or <see cref="AddStore(string)"/>).
    /// </summary>
    public IReadOnlyCollection<string> StoreIds => _stores.Keys;

    public void Reset() => _stores.Clear();
}
