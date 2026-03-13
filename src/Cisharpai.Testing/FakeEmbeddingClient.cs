using System.Text.Json;
using Cisharpai.Features;
using Cisharpai.Features.Embeddings;
using Cisharpai.Models;

namespace Cisharpai.Testing;

/// <summary>
/// A fake implementation of <see cref="IEmbeddingClient"/> for unit testing.
/// Supports canned responses via queues and defaults, and captures all received requests.
/// </summary>
public sealed class FakeEmbeddingClient :
    IEmbeddingClient,
    IImageEmbeddingFeature,
    IMultimodalEmbeddingFeature
{
    private readonly Queue<EmbeddingResponse> _responses = new();
    private readonly Queue<EmbeddingResponse> _imageResponses = new();
    private readonly Queue<EmbeddingResponse> _multimodalResponses = new();

    private readonly List<EmbeddingRequest> _receivedRequests = new();
    private readonly List<(string ImagePath, string Model)> _receivedImageRequests = new();
    private readonly List<IReadOnlyList<MultimodalEmbeddingInput>> _receivedMultimodalRequests = new();

    public FakeEmbeddingClient(FakeEmbeddingFeatures enabledFeatures = FakeEmbeddingFeatures.All)
    {
        Features = new FeatureCollection();

        if (enabledFeatures.HasFlag(FakeEmbeddingFeatures.ImageEmbedding))
            Features.Set<IImageEmbeddingFeature>(this);
        if (enabledFeatures.HasFlag(FakeEmbeddingFeatures.MultimodalEmbedding))
            Features.Set<IMultimodalEmbeddingFeature>(this);
    }

    public IFeatureCollection Features { get; }

    // --- Response configuration ---

    public EmbeddingResponse? DefaultResponse { get; set; }
    public EmbeddingResponse? DefaultImageResponse { get; set; }
    public EmbeddingResponse? DefaultMultimodalResponse { get; set; }

    public void EnqueueResponse(EmbeddingResponse response) => _responses.Enqueue(response);
    public void EnqueueImageResponse(EmbeddingResponse response) => _imageResponses.Enqueue(response);
    public void EnqueueMultimodalResponse(EmbeddingResponse response) => _multimodalResponses.Enqueue(response);

    // --- Request capture ---

    public IReadOnlyList<EmbeddingRequest> ReceivedRequests => _receivedRequests;
    public IReadOnlyList<(string ImagePath, string Model)> ReceivedImageRequests => _receivedImageRequests;
    public IReadOnlyList<IReadOnlyList<MultimodalEmbeddingInput>> ReceivedMultimodalRequests => _receivedMultimodalRequests;

    public int CallCount => _receivedRequests.Count + _receivedImageRequests.Count + _receivedMultimodalRequests.Count;

    public void Reset()
    {
        _responses.Clear();
        _imageResponses.Clear();
        _multimodalResponses.Clear();
        _receivedRequests.Clear();
        _receivedImageRequests.Clear();
        _receivedMultimodalRequests.Clear();
    }

    // --- Interface implementations ---

    public Task<EmbeddingResponse> GetEmbeddingsAsync(
        EmbeddingRequest request,
        CancellationToken cancellationToken = default)
    {
        _receivedRequests.Add(request);
        return Task.FromResult(Dequeue(_responses, DefaultResponse));
    }

    public Task<EmbeddingResponse> GetImageEmbeddingAsync(
        string imagePath,
        string model,
        CancellationToken cancellationToken = default)
    {
        _receivedImageRequests.Add((imagePath, model));
        return Task.FromResult(Dequeue(_imageResponses, DefaultImageResponse ?? DefaultResponse));
    }

    public Task<EmbeddingResponse> GetMultimodalEmbeddingsAsync(
        IReadOnlyList<MultimodalEmbeddingInput> inputs,
        string model,
        EmbeddingInputType? inputType = null,
        int? outputDimension = null,
        bool includeRawResponse = false,
        JsonElement? extraParameters = null,
        CancellationToken cancellationToken = default)
    {
        _receivedMultimodalRequests.Add(inputs);
        return Task.FromResult(Dequeue(_multimodalResponses, DefaultMultimodalResponse ?? DefaultResponse));
    }

    private static T Dequeue<T>(Queue<T> queue, T? fallback)
    {
        if (queue.Count > 0)
            return queue.Dequeue();

        if (fallback is not null)
            return fallback;

        throw new InvalidOperationException(
            $"No queued response and no default configured for {typeof(T).Name}. " +
            "Enqueue a response or set a default before calling the client.");
    }
}
