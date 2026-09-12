using Cisharpai.Features;
using Cisharpai.Models;

namespace Cisharpai.Testing;

/// <summary>
/// A fake implementation of <see cref="IRerankerClient"/> for unit testing.
/// Supports canned responses via a queue and a default, and captures all received requests.
/// </summary>
public sealed class FakeRerankerClient : IRerankerClient
{
    private readonly Queue<RerankResponse> _responses = new();
    private readonly List<RerankRequest> _receivedRequests = new();

    public FakeRerankerClient()
    {
        Features = new FeatureCollection();
    }

    public IFeatureCollection Features { get; }

    // --- Response configuration ---

    public RerankResponse? DefaultResponse { get; set; }

    public void EnqueueResponse(RerankResponse response) => _responses.Enqueue(response);

    // --- Request capture ---

    public IReadOnlyList<RerankRequest> ReceivedRequests => _receivedRequests;

    public int CallCount => _receivedRequests.Count;

    public void Reset()
    {
        _responses.Clear();
        _receivedRequests.Clear();
    }

    // --- Interface implementation ---

    public Task<RerankResponse> RerankAsync(
        RerankRequest request,
        CancellationToken cancellationToken = default)
    {
        _receivedRequests.Add(request);

        if (_responses.Count > 0)
            return Task.FromResult(_responses.Dequeue());

        if (DefaultResponse is not null)
            return Task.FromResult(DefaultResponse);

        throw new InvalidOperationException(
            $"No queued response and no default configured for {nameof(RerankResponse)}. " +
            "Enqueue a response or set a default before calling the client.");
    }
}
