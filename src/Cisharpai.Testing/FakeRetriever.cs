using Cisharpai.Rag;
using Cisharpai.Rag.Packing;

namespace Cisharpai.Testing;

/// <summary>
/// A fake implementation of <see cref="IRetriever"/> for unit testing.
/// Supports canned responses via a queue and a default, and captures all received queries.
/// </summary>
public sealed class FakeRetriever : IRetriever
{
    private readonly Queue<IReadOnlyList<ScoredChunk>> _responses = new();
    private readonly List<(string Query, int TopK)> _receivedQueries = new();

    // --- Response configuration ---

    public IReadOnlyList<ScoredChunk>? DefaultResponse { get; set; }

    public void EnqueueResponse(IReadOnlyList<ScoredChunk> response) => _responses.Enqueue(response);

    // --- Request capture ---

    public IReadOnlyList<(string Query, int TopK)> ReceivedQueries => _receivedQueries;

    public int CallCount => _receivedQueries.Count;

    public void Reset()
    {
        _responses.Clear();
        _receivedQueries.Clear();
    }

    // --- Interface implementation ---

    public Task<IReadOnlyList<ScoredChunk>> RetrieveAsync(
        string query,
        int topK,
        CancellationToken cancellationToken = default)
    {
        _receivedQueries.Add((query, topK));

        if (_responses.Count > 0)
            return Task.FromResult(_responses.Dequeue());

        if (DefaultResponse is not null)
            return Task.FromResult(DefaultResponse);

        throw new InvalidOperationException(
            $"No queued response and no default configured for {nameof(FakeRetriever)}. " +
            "Enqueue a response or set a default before calling the retriever.");
    }
}
