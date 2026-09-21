using System.Runtime.CompilerServices;
using Cisharpai.Rag.Pipeline;

namespace Cisharpai.Testing;

/// <summary>
/// A fake implementation of <see cref="IRagPipeline"/> for unit testing.
/// Supports canned responses via a queue and a default, and captures all received queries.
/// </summary>
public sealed class FakeRagPipeline : IRagPipeline
{
    private readonly Queue<RagResult> _responses = new();
    private readonly Queue<IReadOnlyList<RagStreamingChunk>> _streamingResponses = new();
    private readonly List<(string Query, RagPipelineOptions? Options)> _receivedQueries = new();
    private readonly List<(string Query, RagPipelineOptions? Options)> _receivedStreamingQueries = new();

    // --- Response configuration ---

    public RagResult? DefaultResponse { get; set; }
    public IReadOnlyList<RagStreamingChunk>? DefaultStreamingResponse { get; set; }

    public void EnqueueResponse(RagResult response) => _responses.Enqueue(response);
    public void EnqueueStreamingResponse(IReadOnlyList<RagStreamingChunk> chunks) => _streamingResponses.Enqueue(chunks);

    // --- Request capture ---

    public IReadOnlyList<(string Query, RagPipelineOptions? Options)> ReceivedQueries => _receivedQueries;
    public IReadOnlyList<(string Query, RagPipelineOptions? Options)> ReceivedStreamingQueries => _receivedStreamingQueries;
    public int CallCount => _receivedQueries.Count + _receivedStreamingQueries.Count;

    public void Reset()
    {
        _responses.Clear();
        _streamingResponses.Clear();
        _receivedQueries.Clear();
        _receivedStreamingQueries.Clear();
    }

    // --- Interface implementation ---

    public Task<RagResult> AskAsync(
        string query,
        RagPipelineOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        _receivedQueries.Add((query, options));

        if (_responses.Count > 0)
            return Task.FromResult(_responses.Dequeue());

        if (DefaultResponse is not null)
            return Task.FromResult(DefaultResponse);

        throw new InvalidOperationException(
            $"No queued response and no default configured for {nameof(FakeRagPipeline)}. " +
            "Enqueue a response or set a default before calling the pipeline.");
    }

    public async IAsyncEnumerable<RagStreamingChunk> AskStreamingAsync(
        string query,
        RagPipelineOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        _receivedStreamingQueries.Add((query, options));

        IReadOnlyList<RagStreamingChunk> chunks;

        if (_streamingResponses.Count > 0)
            chunks = _streamingResponses.Dequeue();
        else if (DefaultStreamingResponse is not null)
            chunks = DefaultStreamingResponse;
        else
            throw new InvalidOperationException(
                $"No queued streaming response and no default configured for {nameof(FakeRagPipeline)}. " +
                "Enqueue a streaming response or set a default before calling the pipeline.");

        foreach (var chunk in chunks)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return chunk;
            await Task.Yield();
        }
    }
}
