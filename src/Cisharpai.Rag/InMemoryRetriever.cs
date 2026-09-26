using Cisharpai.Models;
using Cisharpai.Rag.Models;
using Cisharpai.Rag.Packing;

namespace Cisharpai.Rag;

/// <summary>
/// A brute-force in-memory retriever intended for demos and tests.
/// Embeds the query at retrieval time via <see cref="IEmbeddingClient"/> and scores
/// each stored chunk by cosine similarity. Not suitable for production workloads —
/// use a purpose-built store behind <see cref="IRetriever"/> instead.
/// Concurrent retrieval is safe via snapshot reads. Batch ingestion via <see cref="AddRange"/>
/// is atomic per call; concurrent Add + Retrieve may observe a partially-ingested single <see cref="Add"/> call.
/// </summary>
public sealed class InMemoryRetriever : IRetriever
{
    /// <summary>
    /// Default TopK used when <see cref="RetrievalOptions.TopK"/> is null.
    /// </summary>
    public const int DefaultTopK = 10;

    private readonly IEmbeddingClient _embeddingClient;
    private readonly string? _model;
    private readonly object _gate = new();
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
        lock (_gate)
        {
            _store.Add((chunk, (float[])vector.Clone()));
        }
    }

    /// <summary>
    /// Adds multiple chunks with their pre-computed embeddings to the store atomically.
    /// </summary>
    public void AddRange(IEnumerable<(TextChunk Chunk, float[] Vector)> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        lock (_gate)
        {
            foreach (var (chunk, vector) in items)
            {
                ArgumentNullException.ThrowIfNull(chunk);
                ArgumentNullException.ThrowIfNull(vector);
                _store.Add((chunk, (float[])vector.Clone()));
            }
        }
    }

    /// <summary>
    /// Adds multiple <see cref="ChunkEmbedding"/> items to the store atomically — the natural handoff
    /// from <see cref="Embeddings.BulkEmbeddingProcessor"/>.
    /// </summary>
    public void AddRange(IEnumerable<ChunkEmbedding> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        lock (_gate)
        {
            foreach (var item in items)
            {
                ArgumentNullException.ThrowIfNull(item.Chunk);
                ArgumentNullException.ThrowIfNull(item.Vector);
                _store.Add((item.Chunk, (float[])item.Vector.Clone()));
            }
        }
    }

    /// <summary>Number of chunks currently stored.</summary>
    public int Count
    {
        get
        {
            lock (_gate)
            {
                return _store.Count;
            }
        }
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<ScoredChunk>> RetrieveAsync(
        string query,
        RetrievalOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(options);
        if (options.TopK is <= 0)
            throw new ArgumentOutOfRangeException(nameof(options), options.TopK, "TopK must be positive when set.");
        RetrievalFiltering.ThrowIfUnsupportedProviderQuery(options, nameof(InMemoryRetriever));

        return RetrieveCoreAsync(query, options, cancellationToken);
    }

    private async Task<IReadOnlyList<ScoredChunk>> RetrieveCoreAsync(
        string query,
        RetrievalOptions options,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        (TextChunk Chunk, float[] Vector)[] snapshot;
        lock (_gate)
        {
            if (_store.Count == 0)
                return Array.Empty<ScoredChunk>();

            snapshot = _store.ToArray();
        }

        var embeddingResponse = await _embeddingClient.GetEmbeddingsAsync(
            new EmbeddingRequest([query], _model, InputType: EmbeddingInputType.Query),
            cancellationToken).ConfigureAwait(false);

        cancellationToken.ThrowIfCancellationRequested();

        if (!embeddingResponse.IsSuccess || embeddingResponse.Embeddings.Count == 0)
            return Array.Empty<ScoredChunk>();

        var queryVector = embeddingResponse.Embeddings[0];
        if (queryVector is null || queryVector.Length == 0 || queryVector.Any(v => !float.IsFinite(v)))
            return Array.Empty<ScoredChunk>();

        var scored = new List<ScoredChunk>(snapshot.Length);
        foreach (var (chunk, vector) in snapshot)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var score = VectorMath.CosineSimilarity(queryVector, vector);
            scored.Add(new ScoredChunk(chunk, score));
        }

        return RetrievalFiltering.ApplyPostFilters(scored, options, DefaultTopK);
    }
}
