using Cisharpai.Models;
using Cisharpai.Rag.Models;
using Cisharpai.Rag.Packing;

namespace Cisharpai.Rag;

/// <summary>
/// A brute-force in-memory retriever intended for demos and tests.
/// Embeds the query at retrieval time via <see cref="IEmbeddingClient"/> and scores
/// each stored chunk by cosine similarity. Not suitable for production workloads —
/// use a purpose-built store behind <see cref="IRetriever"/> instead.
/// </summary>
public sealed class InMemoryRetriever : IRetriever
{
    private readonly IEmbeddingClient _embeddingClient;
    private readonly string? _model;
    private readonly List<(TextChunk Chunk, float[] Vector)> _store = new();

    /// <param name="embeddingClient">Client used to embed the query at retrieval time.</param>
    /// <param name="model">Optional model name passed to the embedding client. When null, the provider's default is used.</param>
    public InMemoryRetriever(IEmbeddingClient embeddingClient, string? model = null)
    {
        ArgumentNullException.ThrowIfNull(embeddingClient);
        _embeddingClient = embeddingClient;
        _model = model;
    }

    /// <summary>
    /// Adds a chunk and its pre-computed embedding to the store.
    /// </summary>
    public void Add(TextChunk chunk, float[] vector)
    {
        ArgumentNullException.ThrowIfNull(chunk);
        ArgumentNullException.ThrowIfNull(vector);
        _store.Add((chunk, vector));
    }

    /// <summary>
    /// Adds multiple chunks with their pre-computed embeddings to the store.
    /// </summary>
    public void AddRange(IEnumerable<(TextChunk Chunk, float[] Vector)> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        foreach (var (chunk, vector) in items)
            Add(chunk, vector);
    }

    /// <summary>Number of chunks currently stored.</summary>
    public int Count => _store.Count;

    /// <inheritdoc />
    public Task<IReadOnlyList<ScoredChunk>> RetrieveAsync(
        string query,
        int topK,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (topK <= 0)
            throw new ArgumentOutOfRangeException(nameof(topK), topK, "topK must be positive.");

        return RetrieveCoreAsync(query, topK, cancellationToken);
    }

    private async Task<IReadOnlyList<ScoredChunk>> RetrieveCoreAsync(
        string query,
        int topK,
        CancellationToken cancellationToken)
    {
        if (_store.Count == 0)
            return Array.Empty<ScoredChunk>();

        var embeddingResponse = await _embeddingClient.GetEmbeddingsAsync(
            new EmbeddingRequest([query], _model),
            cancellationToken);

        if (!embeddingResponse.IsSuccess)
            return Array.Empty<ScoredChunk>();

        var queryVector = embeddingResponse.Embeddings[0];
        var candidates = new float[_store.Count][];
        for (var i = 0; i < _store.Count; i++)
            candidates[i] = _store[i].Vector;

        var topResults = VectorMath.TopK(
            (ReadOnlySpan<float>)queryVector,
            (ReadOnlySpan<float[]>)candidates,
            topK);

        var results = new ScoredChunk[topResults.Length];
        for (var i = 0; i < topResults.Length; i++)
            results[i] = new ScoredChunk(_store[topResults[i].Index].Chunk, topResults[i].Score);

        return results;
    }
}
