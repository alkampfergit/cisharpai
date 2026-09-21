using Cisharpai.Rag.QueryTransformation;

namespace Cisharpai.Testing;

/// <summary>
/// A fake implementation of <see cref="IQueryTransformer"/> for unit testing.
/// Supports canned responses via a queue and a default, and captures all received queries.
/// </summary>
public sealed class FakeQueryTransformer : IQueryTransformer
{
    private readonly Queue<IReadOnlyList<string>> _responses = new();
    private readonly List<string> _receivedQueries = new();

    // --- Response configuration ---

    public IReadOnlyList<string>? DefaultResponse { get; set; }

    public void EnqueueResponse(IReadOnlyList<string> response) => _responses.Enqueue(response);

    // --- Request capture ---

    public IReadOnlyList<string> ReceivedQueries => _receivedQueries;
    public int CallCount => _receivedQueries.Count;

    public void Reset()
    {
        _responses.Clear();
        _receivedQueries.Clear();
    }

    // --- Interface implementation ---

    public Task<IReadOnlyList<string>> TransformAsync(
        string query,
        CancellationToken cancellationToken = default)
    {
        _receivedQueries.Add(query);

        if (_responses.Count > 0)
            return Task.FromResult(_responses.Dequeue());

        if (DefaultResponse is not null)
            return Task.FromResult(DefaultResponse);

        throw new InvalidOperationException(
            $"No queued response and no default configured for {nameof(FakeQueryTransformer)}. " +
            "Enqueue a response or set a default before calling the transformer.");
    }
}
