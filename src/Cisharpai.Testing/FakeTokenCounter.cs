namespace Cisharpai.Testing;

/// <summary>
/// A fake implementation of <see cref="ITokenCounter"/> for unit testing.
/// Supports canned responses via a queue and a default, and captures all received texts.
/// </summary>
public sealed class FakeTokenCounter : ITokenCounter
{
    private readonly Queue<int> _responses = new();
    private readonly List<string> _receivedTexts = new();

    // --- Response configuration ---

    public int? DefaultCount { get; set; }

    public void EnqueueCount(int tokenCount) => _responses.Enqueue(tokenCount);

    // --- Request capture ---

    public IReadOnlyList<string> ReceivedTexts => _receivedTexts;

    public int CallCount => _receivedTexts.Count;

    public void Reset()
    {
        _responses.Clear();
        _receivedTexts.Clear();
    }

    // --- Interface implementation ---

    public ValueTask<int> CountAsync(string text, CancellationToken cancellationToken = default)
    {
        _receivedTexts.Add(text);

        if (_responses.Count > 0)
            return new ValueTask<int>(_responses.Dequeue());

        if (DefaultCount is not null)
            return new ValueTask<int>(DefaultCount.Value);

        throw new InvalidOperationException(
            $"No queued count and no default configured for {nameof(FakeTokenCounter)}. " +
            "Enqueue a count or set a default before calling the counter.");
    }
}
