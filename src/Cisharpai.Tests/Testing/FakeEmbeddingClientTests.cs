using Cisharpai.Features.Embeddings;
using Cisharpai.Models;
using Cisharpai.Testing;

namespace Cisharpai.Tests.Testing;

[TestFixture]
public class FakeEmbeddingClientTests
{
    [Test]
    public async Task GetEmbeddingsAsync_ReturnsDefaultResponse()
    {
        var fake = new FakeEmbeddingClient
        {
            DefaultResponse = FakeResponses.Embedding()
        };

        var result = await fake.GetEmbeddingsAsync(new EmbeddingRequest(["hello"]));

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Embeddings, Has.Count.EqualTo(1));
        Assert.That(result.Embeddings[0], Is.EqualTo(new[] { 0.1f, 0.2f, 0.3f }));
    }

    [Test]
    public async Task GetEmbeddingsAsync_ReturnsQueuedResponseFirst()
    {
        var fake = new FakeEmbeddingClient
        {
            DefaultResponse = FakeResponses.Embedding([0.9f])
        };
        fake.EnqueueResponse(FakeResponses.Embedding([0.5f]));

        var first = await fake.GetEmbeddingsAsync(new EmbeddingRequest(["a"]));
        var second = await fake.GetEmbeddingsAsync(new EmbeddingRequest(["b"]));

        Assert.That(first.Embeddings[0], Is.EqualTo(new[] { 0.5f }));
        Assert.That(second.Embeddings[0], Is.EqualTo(new[] { 0.9f }));
    }

    [Test]
    public void GetEmbeddingsAsync_ThrowsWhenNoResponseConfigured()
    {
        var fake = new FakeEmbeddingClient();

        Assert.ThrowsAsync<InvalidOperationException>(() =>
            fake.GetEmbeddingsAsync(new EmbeddingRequest(["hello"])));
    }

    [Test]
    public async Task CapturesReceivedRequests()
    {
        var fake = new FakeEmbeddingClient
        {
            DefaultResponse = FakeResponses.Embedding()
        };

        await fake.GetEmbeddingsAsync(new EmbeddingRequest(["hello", "world"]));

        Assert.That(fake.ReceivedRequests, Has.Count.EqualTo(1));
        Assert.That(fake.ReceivedRequests[0].Input, Is.EqualTo(new[] { "hello", "world" }));
    }

    [Test]
    public async Task ImageEmbedding_CapturesRequest()
    {
        var fake = new FakeEmbeddingClient
        {
            DefaultImageResponse = FakeResponses.Embedding([0.4f, 0.5f])
        };

        var result = await fake.GetImageEmbeddingAsync("/path/to/image.png", "model-v1");

        Assert.That(result.Embeddings[0], Is.EqualTo(new[] { 0.4f, 0.5f }));
        Assert.That(fake.ReceivedImageRequests, Has.Count.EqualTo(1));
        Assert.That(fake.ReceivedImageRequests[0].ImagePath, Is.EqualTo("/path/to/image.png"));
        Assert.That(fake.ReceivedImageRequests[0].Model, Is.EqualTo("model-v1"));
    }

    [Test]
    public async Task ImageEmbedding_FallsBackToDefaultResponse()
    {
        var fake = new FakeEmbeddingClient
        {
            DefaultResponse = FakeResponses.Embedding([0.1f])
        };

        var result = await fake.GetImageEmbeddingAsync("/img.png", "model");

        Assert.That(result.Embeddings[0], Is.EqualTo(new[] { 0.1f }));
    }

    [Test]
    public async Task MultimodalEmbedding_CapturesRequest()
    {
        var fake = new FakeEmbeddingClient
        {
            DefaultMultimodalResponse = FakeResponses.Embedding([0.7f])
        };

        var inputs = new List<MultimodalEmbeddingInput>
        {
            new([new TextEmbeddingContent("hello")])
        };

        var result = await fake.GetMultimodalEmbeddingsAsync(inputs, "model");

        Assert.That(result.Embeddings[0], Is.EqualTo(new[] { 0.7f }));
        Assert.That(fake.ReceivedMultimodalRequests, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task CallCount_TracksAllMethods()
    {
        var fake = new FakeEmbeddingClient
        {
            DefaultResponse = FakeResponses.Embedding()
        };

        await fake.GetEmbeddingsAsync(new EmbeddingRequest(["a"]));
        await fake.GetImageEmbeddingAsync("/img.png", "model");
        await fake.GetMultimodalEmbeddingsAsync(
            [new MultimodalEmbeddingInput([new TextEmbeddingContent("b")])], "model");

        Assert.That(fake.CallCount, Is.EqualTo(3));
    }

    [Test]
    public void Features_AllRegisteredByDefault()
    {
        var fake = new FakeEmbeddingClient();

        Assert.That(fake.Features.Get<IImageEmbeddingFeature>(), Is.Not.Null);
        Assert.That(fake.Features.Get<IMultimodalEmbeddingFeature>(), Is.Not.Null);
    }

    [Test]
    public void Features_CanBeSelective()
    {
        var fake = new FakeEmbeddingClient(FakeEmbeddingFeatures.ImageEmbedding);

        Assert.That(fake.Features.Get<IImageEmbeddingFeature>(), Is.Not.Null);
        Assert.That(fake.Features.Get<IMultimodalEmbeddingFeature>(), Is.Null);
    }

    [Test]
    public void Features_None()
    {
        var fake = new FakeEmbeddingClient(FakeEmbeddingFeatures.None);

        Assert.That(fake.Features.Get<IImageEmbeddingFeature>(), Is.Null);
        Assert.That(fake.Features.Get<IMultimodalEmbeddingFeature>(), Is.Null);
    }

    [Test]
    public async Task Reset_ClearsEverything()
    {
        var fake = new FakeEmbeddingClient
        {
            DefaultResponse = FakeResponses.Embedding()
        };
        fake.EnqueueResponse(FakeResponses.Embedding([0.5f]));
        await fake.GetEmbeddingsAsync(new EmbeddingRequest(["test"]));

        fake.Reset();

        Assert.That(fake.ReceivedRequests, Is.Empty);
        Assert.That(fake.CallCount, Is.EqualTo(0));
    }
}
